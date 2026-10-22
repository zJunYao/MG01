using System;
using System.Collections.Generic;
using System.Linq;

namespace MG01.Demo.Domain
{
    public static class TargetPolicy
    {
        public static string FirstAlive(IEnumerable<ActorState> actors) => actors.Where(actor => actor.TargetOrder > 0 && actor.Alive).OrderBy(actor => actor.TargetOrder).Select(actor => actor.Key).FirstOrDefault();
        public static string Cycle(IEnumerable<ActorState> actors, string current)
        {
            var alive = actors.Where(actor => actor.TargetOrder > 0 && actor.Alive).OrderBy(actor => actor.TargetOrder).ToArray();
            if (alive.Length == 0) return null;
            int index = Array.FindIndex(alive, actor => actor.Key == current);
            return alive[(index + 1) % alive.Length].Key;
        }
    }
    internal static class BattleReducer
    {
        internal static void Apply(BattleState state, ActionStep step, bool consume, Action<string, string, int, string> emit)
        {
            var target = state.Actors.Single(actor => actor.Key == step.TargetKey);
            if (step.Kind == "damage" || step.Kind == "attack")
            {
                int absorbed = Math.Min(target.Shield, step.Value);
                target.Shield -= absorbed;
                int loss = Math.Min(target.Health, step.Value - absorbed);
                target.Health -= loss;
                emit("承伤", target.Key, loss, "护盾吸收=" + absorbed + "；生命=" + target.Health + "；护盾=" + target.Shield);
                if (!target.Alive) emit("死亡", target.Key, 0, "生命归零");
            }
            else if (step.Kind == "heal")
            {
                int amount = Math.Min(step.Value, target.MaxHealth - target.Health);
                if (amount > 0)
                {
                    target.Health += amount;
                    emit("恢复", target.Key, amount, "生命=" + target.Health);
                    if (consume)
                    {
                        state.Items.RemoveAll(item => item.Key == step.ItemKey);
                        emit("道具消耗", step.ItemKey, 0, "实际恢复后销毁");
                    }
                }
            }
            else if (step.Kind == "shield")
            {
                target.Shield = checked(target.Shield + step.Value);
                emit("护盾", target.Key, step.Value, "护盾=" + target.Shield);
            }
            else if (step.Kind == "charge") emit("蓄力", step.ActorKey, 0, "本次行动无伤害");
            else throw new InvalidOperationException("未知结算效果：" + step.Kind);

            if (!state.Hero.Alive) state.Phase = BattlePhase.Defeat;
            else if (TargetPolicy.FirstAlive(state.Actors) == null) state.Phase = BattlePhase.Victory;
            if (state.Phase == BattlePhase.Victory || state.Phase == BattlePhase.Defeat)
            { emit("战斗结束", state.Hero.Key, state.Hero.Health, state.Phase.ToString()); return; }
            var selected = state.Actors.FirstOrDefault(actor => actor.Key == state.TargetKey);
            if (selected == null || !selected.Alive)
            {
                state.TargetKey = TargetPolicy.FirstAlive(state.Actors);
                emit("目标变化", state.TargetKey, 0, "击杀后从站位顺序开头查找");
            }
        }
    }
}
