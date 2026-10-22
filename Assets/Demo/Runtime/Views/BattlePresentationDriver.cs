using System;
using System.Collections.Generic;
using MG01.Demo.Application;
using MG01.Demo.Configuration;
using MG01.Demo.Domain;
using UnityEngine;

namespace MG01.Demo.Views
{
    /// <summary>只驱动表现和提交凭证；靠近/返回不改变任何战斗数值。</summary>
    public sealed class BattlePresentationDriver : IDisposable
    {
        private readonly BattleSceneBinding scene;
        private readonly DemoConfigCatalog catalog;
        private readonly Dictionary<string, BattleActorPlayer> players = new Dictionary<string, BattleActorPlayer>();
        private readonly Dictionary<string, float> reactions = new Dictionary<string, float>();
        private readonly HashSet<string> damageReactions = new HashSet<string>();
        private readonly Dictionary<string, float> flashRemaining = new Dictionary<string, float>();
        private readonly Dictionary<string, float> flashIntervals = new Dictionary<string, float>();
        private readonly HashSet<string> dead = new HashSet<string>();
        private ActionStep step;
        private ConfigRecord timing;
        private HitTimeline timeline;
        private float elapsed;
        private bool approaching, returning, cancelled;
        private bool heroOpening, heroOpened;
        private float heroActionElapsed;
        public bool HeroQueueReady => heroOpened && !heroOpening;
        public void PrepareHeroTurn() { heroOpened = false; heroOpening = false; }
        private readonly Action<ActionStep> commit, complete;
        private readonly Func<ActionStep, float> playEffect;
        public ActionStep Current => step;
        public int HitCount { get; private set; }
        public BattlePresentationDriver(BattleSceneBinding scene, DemoConfigCatalog catalog, Action<ActionStep> commit, Action<ActionStep> complete, Func<ActionStep, float> playEffect = null)
        {
            this.scene = scene; this.catalog = catalog; this.commit = commit; this.complete = complete;
            this.playEffect = playEffect;
            foreach (var actor in scene.actors) players.Add(actor.key, new BattleActorPlayer(actor.animator));
            Reset();
        }
        private void Idle(BattleSceneBinding.Actor actor)
        { var clip = actor.Find(actor.key == "hero" ? "Breathe_south-east" : "Idle_south-west") ?? actor.Find("Idle_south-east"); players[actor.key].Play(clip); }
        public void Reset()
        {
            step = null; cancelled = false; PrepareHeroTurn(); reactions.Clear(); damageReactions.Clear(); flashRemaining.Clear(); flashIntervals.Clear(); dead.Clear(); scene.ResetActors();
            foreach (var actor in scene.actors) Idle(actor);
        }
        public void Begin(ActionStep next)
        {
            if (cancelled) throw new InvalidOperationException("表现驱动已取消");
            if (step != null) throw new InvalidOperationException("上一表现尚未完成");
            step = next; timing = catalog.Get(ConfigSchema.Presentations, next.PresentationKey); elapsed = 0; returning = false;
            // 连击按配置链选择每一击的动作、命中点及速度。
            for (int index = 1; index < next.HitIndex; ++index)
            {
                string following = timing.Text("next_hit_presentation");
                if (following.Length == 0) break;
                timing = catalog.Get(ConfigSchema.Presentations, following);
            }
            var actor = scene.Get(step.ActorKey);
            if (step.ActorKey == "hero" && !heroOpened)
            {
                heroOpening = true;
                players[actor.key].Play(actor.Find(timing.Text("animation")), (float)timing.Decimal("speed"));
                return;
            }
            approaching = step.ActorKey != "hero" && step.Kind == "attack" && step.HitIndex == 1;
            if (approaching) players[actor.key].Play(actor.Find("Walk_south-west")); else StartAction();
        }
        private void StartAction()
        {
            approaching = false;
            var clip = scene.Get(step.ActorKey).Find(timing.Text("animation"));
            if (clip == null) throw new InvalidOperationException("动作不存在：" + step.ActorKey + "/" + timing.Text("animation"));
            heroActionElapsed = 0;
            if (step.ActorKey != "hero") players[step.ActorKey].Play(clip, (float)timing.Decimal("speed"));
            float effectDuration = playEffect?.Invoke(step) ?? 0;
            timeline = new HitTimeline(effectDuration > 0 ? effectDuration : clip.length, timing.Decimal("hit_time"), timing.Decimal("end_time"));
        }
        public void Tick(float delta)
        {
            if (cancelled) return;
            foreach (var actor in scene.actors)
            {
                if (!reactions.TryGetValue(actor.key, out float remaining)) continue;
                remaining -= delta;
                if (remaining <= 0)
                {
                    reactions.Remove(actor.key);
                    damageReactions.Remove(actor.key);
                    actor.animator.GetComponent<SpriteRenderer>().color = dead.Contains(actor.key) ? new Color(.3f, .3f, .3f, .4f) : Color.white;
                    if (!dead.Contains(actor.key) && step?.ActorKey != actor.key) Idle(actor);
                }
                else
                {
                    reactions[actor.key] = remaining;
                    // 受击期间交替闪红，增强命中辨识度，保持精灵不透明。
                    if (damageReactions.Contains(actor.key))
                    {
                        flashRemaining[actor.key] = Mathf.Max(0, flashRemaining[actor.key] - delta);
                        actor.animator.GetComponent<SpriteRenderer>().color = flashRemaining[actor.key] > 0 && Mathf.FloorToInt(flashRemaining[actor.key] / flashIntervals[actor.key]) % 2 == 0
                            ? new Color(1f, .12f, .12f, 1f) : Color.white;
                    }
                }
            }
            if (step == null) return;
            if (heroOpening)
            {
                var clip = scene.Get("hero").Find(timing.Text("animation"));
                if (players["hero"].Time >= clip.length)
                {
                    heroOpening = false; heroOpened = true; elapsed = 0;
                    Idle(scene.Get("hero"));
                    StartAction();
                }
                return;
            }
            elapsed += delta;
            if (elapsed > timing.Decimal("timeout")) throw new InvalidOperationException("动作超时：" + step.PresentationKey);
            var acting = scene.Get(step.ActorKey);
            if (!players[acting.key].Valid) throw new InvalidOperationException("动作组件失效：" + acting.key);
            if (approaching || returning)
            {
                var destination = returning ? acting.home : scene.Get(step.TargetKey).animator.transform.position +
                    new Vector3((float)timing.Decimal("approach_offset_x"), (float)timing.Decimal("approach_offset_y"), 0);
                acting.animator.transform.position = Vector3.MoveTowards(acting.animator.transform.position, destination, (float)timing.Decimal("move_speed") * delta);
                float tolerance = (float)timing.Decimal("arrival_tolerance");
                if ((acting.animator.transform.position - destination).sqrMagnitude <= tolerance * tolerance)
                {
                    acting.animator.transform.position = destination;
                    if (returning) Finish(); else StartAction();
                }
                return;
            }
            heroActionElapsed += delta * (float)timing.Decimal("speed");
            double actionElapsed = acting.key == "hero" ? heroActionElapsed : players[acting.key].Time;
            if (timeline.TakeHit(actionElapsed)) { ++HitCount; commit(step); }
            if (cancelled || step == null) return;
            if (!timeline.TakeComplete(actionElapsed)) return;
            // 战士第一击留在近身位，第二击完成才返回；次数来自行为计划而非写死二击。
            bool moreHits = step.ActorKey != "hero" && step.Kind == "attack" && step.HitIndex < EnemyHitCount(step.ActorKey);
            if (step.ActorKey != "hero" && step.Kind == "attack" && !moreHits)
            { returning = true; players[acting.key].Play(acting.Find("Walk_south-west")); }
            else Finish();
        }
        private int EnemyHitCount(string actorKey)
        {
            string definition = null;
            foreach (var row in catalog.Table(ConfigSchema.Roster)) if (row.Key == actorKey) definition = row.Text("actor_key");
            string group = catalog.Get(ConfigSchema.Actors, definition).Text("behavior_group");
            foreach (var row in catalog.Table(ConfigSchema.EnemyActions)) if (row.Text("group") == group && row.Text("kind") == "attack") return row.Integer("hit_count");
            return 0;
        }
        private void Finish()
        { var finished = step; step = null; if (finished.ActorKey != "hero") Idle(scene.Get(finished.ActorKey)); complete(finished); }
        public void React(ActionStep hit, BattleSnapshot snapshot)
        {
            var target = scene.Get(hit.TargetKey); var renderer = target.animator.GetComponent<SpriteRenderer>();
            if (hit.Kind == "charge") return;
            ActorState state = null; foreach (var actor in snapshot.Actors) if (actor.Key == hit.TargetKey) state = actor;
            if (state != null && !state.Alive) { dead.Add(target.key); reactions.Remove(target.key); players[target.key].Dispose(); renderer.color = new Color(.3f, .3f, .3f, .4f); }
            else if (hit.Kind == "damage" || hit.Kind == "attack")
            {
                ConfigRecord reaction = null;
                foreach (var row in catalog.Table(ConfigSchema.Reactions))
                    if (row.Text("actor_key") == state.DefinitionKey) { reaction = row; break; }
                if (reaction == null) throw new InvalidOperationException("缺少角色受击配置：" + state.DefinitionKey);
                string animation = reaction.Text("animation");
                var clip = animation.Length == 0 ? null : target.Find(animation);
                if (animation.Length > 0 && clip == null) throw new InvalidOperationException("受击动画未绑定：" + animation);
                float speed = (float)reaction.Decimal("speed");
                float flashDuration = (float)reaction.Decimal("flash_duration");
                renderer.color = new Color(1f, .12f, .12f, 1f);
                reactions[target.key] = Mathf.Max(flashDuration, clip != null ? clip.length / speed : 0);
                flashRemaining[target.key] = flashDuration;
                flashIntervals[target.key] = (float)reaction.Decimal("flash_interval");
                damageReactions.Add(target.key);
                if (clip != null) players[target.key].Play(clip, speed);
            }
            else if (hit.Kind == "heal" || hit.Kind == "shield")
            { renderer.color = hit.Kind == "heal" ? Color.green : Color.cyan; reactions[target.key] = .35f; }
        }
        public void Cancel()
        { cancelled = true; step = null; foreach (var player in players.Values) player.Dispose(); }
        public void Dispose() { Cancel(); }
    }
}
