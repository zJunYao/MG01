using System;
using System.Collections.Generic;
using System.Linq;
using MG01.Demo.Configuration;

namespace MG01.Demo.Domain
{
    public enum BattlePhase { Preparation, HeroExecution, EnemyExecution, Victory, Defeat, Closed }
    public sealed class ActorState
    {
        public string Key { get; }
        public string DefinitionKey { get; }
        public int ActionOrder { get; }
        public int TargetOrder { get; }
        public int MaxHealth { get; }
        public int Health { get; internal set; }
        public int Shield { get; internal set; }
        public int BehaviorCursor { get; internal set; }
        public bool Alive => Health > 0;
        internal ActorState(ConfigRecord roster, ConfigRecord actor)
        {
            Key = roster.Key; DefinitionKey = actor.Key; ActionOrder = roster.Integer("action_order");
            TargetOrder = roster.Integer("target_order"); MaxHealth = actor.Integer("max_health"); Health = MaxHealth;
        }
        internal ActorState Copy() => (ActorState)MemberwiseClone();
    }
    /// <summary>领域事件是只读的事实记录，表现订阅者不得借此反向修改状态。</summary>
    public sealed class BattleEvent
    {
        public Guid SessionId { get; }
        public int Round { get; }
        public long StepId { get; }
        public string Kind { get; }
        public string Subject { get; }
        public int Value { get; }
        public string Detail { get; }
        internal BattleEvent(BattleState state, long stepId, string kind, string subject, int value, string detail)
        { SessionId = state.SessionId; Round = state.Round; StepId = stepId; Kind = kind; Subject = subject; Value = value; Detail = detail; }
    }

    /// <summary>仅会话持有可变集合；对外一律返回独立拷贝的只读快照。</summary>
    internal sealed class BattleState
    {
        internal Guid SessionId = Guid.NewGuid();
        internal int Round = 1;
        internal BattlePhase Phase = BattlePhase.Preparation;
        internal string TargetKey;
        internal string ActingKey;
        internal readonly List<ItemInstance> Items;
        internal readonly List<ActorState> Actors;
        internal ActorState Hero => Actors.Single(actor => actor.TargetOrder == 0);
        internal BattleState(DemoConfigCatalog catalog, ConfigRecord battle)
        {
            Items = catalog.Table(ConfigSchema.Loadout).Where(row => row.Text("battle_key") == battle.Key).Select(row => new ItemInstance(row)).ToList();
            Actors = catalog.Table(ConfigSchema.Roster).Where(row => row.Text("battle_key") == battle.Key)
                .OrderBy(row => row.Integer("action_order")).Select(row => new ActorState(row, catalog.Get(ConfigSchema.Actors, row.Text("actor_key")))).ToList();
            Hero.Health = battle.Integer("initial_health");
            // 按已确认规则，进入第一轮备战即清盾，后续备战同样清除。
            Hero.Shield = 0;
            TargetKey = battle.Text("default_target"); ActingKey = Hero.Key;
        }
    }
    public sealed class BattleSnapshot
    {
        public Guid SessionId { get; }
        public int Round { get; }
        public BattlePhase Phase { get; }
        public string TargetKey { get; }
        public string ActingKey { get; }
        public IReadOnlyList<ItemInstance> Items { get; }
        public IReadOnlyList<ActorState> Actors { get; }
        public ActorState Hero => Actors.Single(actor => actor.TargetOrder == 0);
        public IReadOnlyList<ActionPlan> RemainingPlan { get; }
        public ActionStep CurrentStep { get; }
        internal BattleSnapshot(BattleState state, IEnumerable<ActionPlan> plans, ActionStep step)
        {
            SessionId = state.SessionId; Round = state.Round; Phase = state.Phase; TargetKey = state.TargetKey; ActingKey = state.ActingKey;
            Items = Array.AsReadOnly(state.Items.Select(item => item.Copy()).ToArray());
            Actors = Array.AsReadOnly(state.Actors.Select(actor => actor.Copy()).ToArray());
            RemainingPlan = Array.AsReadOnly(plans.ToArray()); CurrentStep = step;
        }
    }
}
