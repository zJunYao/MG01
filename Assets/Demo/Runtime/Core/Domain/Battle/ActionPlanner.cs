using System;
using System.Collections.Generic;
using System.Linq;
using MG01.Demo.Configuration;

namespace MG01.Demo.Domain
{
    public sealed class ActionPlan
    {
        public string ItemKey { get; }
        public string Kind { get; }
        public int BaseValue { get; }
        public int Bonus { get; }
        public int Value => checked(BaseValue + Bonus);
        public bool Consume { get; }
        public string PresentationKey { get; }
        internal ActionPlan(ItemInstance item, ConfigRecord effect, int bonus)
        { ItemKey = item.Key; Kind = effect.Text("kind"); BaseValue = effect.Integer("value"); Bonus = bonus; Consume = effect.Boolean("consume"); PresentationKey = effect.Text("presentation_key"); }
    }
    public static class ActionPlanner
    {
        public static IReadOnlyList<ActionPlan> Build(DemoConfigCatalog catalog, BattleSnapshot snapshot)
        {
            var plans = new List<ActionPlan>();
            foreach (var item in snapshot.Items.Where(item => item.Container == ItemContainer.Backpack).OrderBy(item => item.Column).ThenBy(item => item.Row))
            {
                var definition = catalog.Get(ConfigSchema.Items, item.DefinitionKey);
                var effect = catalog.Get(ConfigSchema.Effects, definition.Text("effect_key"));
                if (effect.Text("kind") == "adjacent_bonus" || (effect.Text("kind") == "heal" && snapshot.Hero.Health >= snapshot.Hero.MaxHealth)) continue;
                plans.Add(new ActionPlan(item, effect, AdjacencyService.DamageBonus(catalog, snapshot.Items, item)));
            }
            return plans.AsReadOnly();
        }
    }
    public sealed class EnemyIntent
    {
        public string ActorKey { get; }
        public string Kind { get; }
        public int HitCount { get; }
        public int Damage { get; }
        public string PresentationKey { get; }
        internal EnemyIntent(string actorKey, ConfigRecord row)
        { ActorKey = actorKey; Kind = row.Text("kind"); HitCount = row.Integer("hit_count"); Damage = row.Integer("damage"); PresentationKey = row.Text("presentation_key"); }
    }
    public static class EnemyActionPlanner
    {
        public static EnemyIntent Build(DemoConfigCatalog catalog, ActorState actor)
        {
            if (actor.TargetOrder == 0 || !actor.Alive) throw new InvalidOperationException("仅存活敌人具有行动意图");
            string group = catalog.Get(ConfigSchema.Actors, actor.DefinitionKey).Text("behavior_group");
            var cycle = catalog.Table(ConfigSchema.EnemyActions).Where(row => row.Text("group") == group).OrderBy(row => row.Integer("sequence")).ToArray();
            return new EnemyIntent(actor.Key, cycle[actor.BehaviorCursor % cycle.Length]);
        }
    }
    /// <summary>命中凭证不含视图引用；一个攻击命中对应一个步骤，蓄力也有独立零伤害步骤。</summary>
    public sealed class ActionStep
    {
        public Guid SessionId { get; }
        public long StepId { get; }
        public string ActorKey { get; }
        public string TargetKey { get; }
        public string ItemKey { get; }
        public string Kind { get; }
        public int Value { get; }
        public int HitIndex { get; }
        public string PresentationKey { get; }
        internal ActionStep(Guid session, long id, string actor, string target, string item, string kind, int value, int hitIndex, string presentation)
        { SessionId = session; StepId = id; ActorKey = actor; TargetKey = target; ItemKey = item; Kind = kind; Value = value; HitIndex = hitIndex; PresentationKey = presentation; }
    }
}
