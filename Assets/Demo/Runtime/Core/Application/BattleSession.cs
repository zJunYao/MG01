using System;
using System.Collections.Generic;
using System.Linq;
using MG01.Demo.Configuration;
using MG01.Demo.Domain;

namespace MG01.Demo.Application
{
    /// <summary>唯一状态所有者。命令串行执行，事件为可排空事实队列，不在提交过程中回调外部代码。</summary>
    public sealed class BattleSession
    {
        private readonly DemoConfigCatalog catalog;
        private readonly ConfigRecord battle;
        private readonly BattleState state;
        private readonly InventoryService inventory;
        private readonly List<BattleEvent> pending = new List<BattleEvent>();
        private readonly List<ActionPlan> plans = new List<ActionPlan>();
        private int planIndex, enemyIndex, hitIndex;
        private long nextStep;
        private ActionStep current;
        private bool committed;
        private EnemyIntent enemyIntent;
        public BattleSnapshot Snapshot => new BattleSnapshot(state, plans.Skip(planIndex), current);

        public BattleSession(DemoConfigCatalog catalog, string battleKey)
        {
            ConfigValidator.ThrowIfInvalid(catalog);
            this.catalog = catalog; battle = catalog.Get(ConfigSchema.Battles, battleKey);
            state = new BattleState(catalog, battle); inventory = new InventoryService(catalog, battle);
            Emit("会话开始", battle.Key, state.Hero.Health, "配置版本=" + catalog.Version);
        }
        public IReadOnlyList<BattleEvent> DrainEvents()
        { var result = Array.AsReadOnly(pending.ToArray()); pending.Clear(); return result; }
        private void Emit(string kind, string subject, int value, string detail) => pending.Add(new BattleEvent(state, current?.StepId ?? 0, kind, subject, value, detail));
        private bool Preparing(out string reason)
        { reason = state.Phase == BattlePhase.Preparation ? null : "仅主角备战阶段允许此操作"; return reason == null; }
        public bool TryMove(string key, ItemContainer container, int column, int row, int slot, out string reason)
        {
            if (!Preparing(out reason) || !inventory.TryMove(state.Items, key, container, column, row, slot, out reason)) return false;
            Emit("道具移动", key, 0, container + "；列=" + column + "；行=" + row + "；槽=" + slot); return true;
        }
        public bool TryRotate(string key, out string reason)
        {
            if (!Preparing(out reason) || !inventory.TryRotate(state.Items, key, out reason)) return false;
            Emit("道具旋转", key, state.Items.Single(item => item.Key == key).Rotation, "左上角锚点"); return true;
        }
        public bool TrySelectTarget(string key, out string reason)
        {
            if (!Preparing(out reason)) return false;
            if (!state.Actors.Any(actor => actor.Key == key && actor.TargetOrder > 0 && actor.Alive))
            { reason = "目标不存在或已死亡"; return false; }
            state.TargetKey = key; Emit("目标变化", key, 0, "备战选敌"); return true;
        }
        public bool TryCycleTarget(out string reason) => TrySelectTarget(TargetPolicy.Cycle(state.Actors, state.TargetKey), out reason);
        public bool TryConfirm(bool isDragging, out string reason)
        {
            if (!Preparing(out reason)) return false;
            if (isDragging) { reason = "拖动期间不能确认"; return false; }
            plans.Clear(); plans.AddRange(ActionPlanner.Build(catalog, Snapshot)); planIndex = 0;
            state.Phase = BattlePhase.HeroExecution;
            Emit("阶段变化", state.Hero.Key, 0, "确认并锁定道具顺序及相邻增伤");
            PrepareNext(); reason = null; return true;
        }
        public IReadOnlyList<EnemyIntent> GetEnemyIntents() => Array.AsReadOnly(state.Actors.Where(actor => actor.TargetOrder > 0 && actor.Alive)
            .OrderBy(actor => actor.ActionOrder).Select(actor => EnemyActionPlanner.Build(catalog, actor)).ToArray());

        public bool TryCommitHit(Guid sessionId, long stepId, out string reason)
        {
            if (!ValidStep(sessionId, stepId, out reason)) return false;
            if (committed) { reason = "此步骤已经提交命中"; return false; }
            committed = true;
            bool consume = state.Phase == BattlePhase.HeroExecution && plans[planIndex].Consume;
            Emit("步骤命中", current.ActorKey, current.Value, current.Kind + "；目标=" + current.TargetKey + "；道具=" + current.ItemKey + "；次数=" + current.HitIndex);
            BattleReducer.Apply(state, current, consume, Emit);
            if (state.Phase == BattlePhase.Victory || state.Phase == BattlePhase.Defeat)
            { current = null; plans.Clear(); planIndex = 0; }
            reason = null; return true;
        }
        public bool TryCompletePresentation(Guid sessionId, long stepId, out string reason)
        {
            if (!ValidStep(sessionId, stepId, out reason)) return false;
            if (!committed) { reason = "必须先提交命中，表现结束不能代替结算"; return false; }
            if (state.Phase == BattlePhase.HeroExecution) ++planIndex;
            else
            {
                ++hitIndex;
                if (hitIndex >= Math.Max(1, enemyIntent.HitCount))
                {
                    var actor = state.Actors[enemyIndex];
                    ++actor.BehaviorCursor; ++enemyIndex; hitIndex = 0; enemyIntent = null;
                }
            }
            current = null; PrepareNext(); reason = null; return true;
        }
        private bool ValidStep(Guid sessionId, long stepId, out string reason)
        {
            bool valid = state.SessionId == sessionId && current != null && current.StepId == stepId &&
                (state.Phase == BattlePhase.HeroExecution || state.Phase == BattlePhase.EnemyExecution);
            reason = valid ? null : "会话或步骤已失效"; return valid;
        }
        private void PrepareNext()
        {
            committed = false;
            if (state.Phase == BattlePhase.HeroExecution)
            {
                while (planIndex < plans.Count)
                {
                    var plan = plans[planIndex];
                    if (!state.Items.Any(item => item.Key == plan.ItemKey) || (plan.Kind == "heal" && state.Hero.Health >= state.Hero.MaxHealth))
                    { ++planIndex; continue; }
                    string target = plan.Kind == "damage" ? state.TargetKey : state.Hero.Key;
                    current = new ActionStep(state.SessionId, ++nextStep, state.Hero.Key, target, plan.ItemKey, plan.Kind, plan.Value, 1, plan.PresentationKey);
                    return;
                }
                state.Phase = BattlePhase.EnemyExecution; enemyIndex = 1; hitIndex = 0; enemyIntent = null;
                Emit("阶段变化", state.Hero.Key, 0, "主角行动结束，进入敌人阶段");
            }
            while (enemyIndex < state.Actors.Count)
            {
                var actor = state.Actors[enemyIndex];
                if (!actor.Alive) { ++enemyIndex; continue; }
                state.ActingKey = actor.Key;
                if (enemyIntent == null) enemyIntent = EnemyActionPlanner.Build(catalog, actor);
                current = new ActionStep(state.SessionId, ++nextStep, actor.Key, state.Hero.Key, null, enemyIntent.Kind,
                    enemyIntent.Damage, hitIndex + 1, enemyIntent.PresentationKey);
                return;
            }
            ++state.Round; state.Phase = BattlePhase.Preparation; state.ActingKey = state.Hero.Key;
            int shield = state.Hero.Shield; state.Hero.Shield = 0; plans.Clear(); planIndex = 0;
            Emit("护盾清除", state.Hero.Key, shield, "进入下一轮备战");
            Emit("阶段变化", state.Hero.Key, 0, "主角备战");
        }
        public BattleSession Restart()
        { Close(); return new BattleSession(catalog, battle.Key); }
        public void Close()
        {
            if (state.Phase == BattlePhase.Closed) return;
            state.Phase = BattlePhase.Closed; current = null; plans.Clear(); planIndex = 0;
            Emit("会话关闭", battle.Key, 0, "旧命令及命中凭证失效");
        }
    }
}
