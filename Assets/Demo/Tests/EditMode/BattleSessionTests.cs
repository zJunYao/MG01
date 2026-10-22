using System;
using System.Collections.Generic;
using System.Linq;
using MG01.Demo.Configuration;
using MG01.Demo.Domain;
using MG01.Demo.Editor;
using NUnit.Framework;
using static MG01.Demo.Tests.BattleTestDriver;

namespace MG01.Demo.Tests
{
    public sealed class BattleSessionTests
    {
        [Test] public void 确认过滤被动并锁定药水和增伤顺序()
        {
            var session = Create(); AllItems(session);
            Assert.That(session.TryConfirm(false, out _), Is.True);
            var plan = session.Snapshot.RemainingPlan;
            Assert.That(plan.Select(item => item.ItemKey), Is.EqualTo(new[] { "potion_01", "staff_01", "hat_01", "bubble_01" }));
            Assert.That(plan.Select(item => item.Value), Is.EqualTo(new[] { 10, 8, 12, 9 }));
            Assert.That(session.TryConfirm(false, out _), Is.False);
            Assert.That(session.TryMove("staff_01", ItemContainer.Reserve, -1, -1, 3, out _), Is.False);
            Assert.That(session.TryRotate("staff_01", out _), Is.False);
            Assert.That(session.TrySelectTarget("chief", out _), Is.False);
            Hit(session);
            Assert.That(session.Snapshot.Hero.Health, Is.EqualTo(28));
            Assert.That(session.Snapshot.Items.Any(item => item.Key == "potion_01"), Is.False);
            Assert.That(session.Snapshot.RemainingPlan.Select(item => item.ItemKey), Is.EqualTo(new[] { "staff_01", "hat_01", "bubble_01" }));
            Assert.That(plan[0].ItemKey, Is.EqualTo("potion_01"), "旧计划快照保持只读历史");
        }
        [Test] public void 满血药水过滤保留与治疗裁剪()
        {
            var config = Change(ConfigSourceLoader.Load(), "DemoBattles", "demo_battle", "initial_health", "30");
            var session = Create(config); AllItems(session); session.TryConfirm(false, out _);
            Assert.That(session.Snapshot.RemainingPlan.All(plan => plan.ItemKey != "potion_01"), Is.True);
            Assert.That(session.Snapshot.Items.Any(item => item.Key == "potion_01"), Is.True);
            config = Change(config, "DemoBattles", "demo_battle", "initial_health", "29");
            session = Create(config); AllItems(session); session.TryConfirm(false, out _); Hit(session);
            Assert.That(session.Snapshot.Hero.Health, Is.EqualTo(30));
            Assert.That(session.DrainEvents().Single(item => item.Kind == "恢复").Value, Is.EqualTo(1));
        }
        [Test] public void 同列上行优先而不是逐行扫描()
        {
            var session = Create(); Move(session, "bubble_01", 3, 3); Move(session, "potion_01", 0, 3);
            Move(session, "staff_01", 0, 0); session.TryConfirm(false, out _);
            Assert.That(session.Snapshot.RemainingPlan.Select(item => item.ItemKey), Is.EqualTo(new[] { "staff_01", "potion_01", "hat_01", "bubble_01" }));
        }
        [Test] public void 拖动确认拒绝且事件可排空()
        {
            var session = Create(); Assert.That(session.TryConfirm(true, out _), Is.False);
            Assert.That(session.Snapshot.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(session.DrainEvents().Count, Is.EqualTo(1)); Assert.That(session.DrainEvents(), Is.Empty);
        }
        [Test] public void 每次命中幂等并且结束表现不能代替命中()
        {
            var session = Create(); session.TryConfirm(false, out _); var step = session.Snapshot.CurrentStep;
            Assert.That(session.TryCompletePresentation(step.SessionId, step.StepId, out _), Is.False);
            Assert.That(session.TryCommitHit(Guid.NewGuid(), step.StepId, out _), Is.False);
            Assert.That(session.TryCommitHit(step.SessionId, step.StepId + 1, out _), Is.False);
            Hit(session, false); var health = session.Snapshot.Actors.Single(actor => actor.Key == "warrior").Health;
            Assert.That(session.TryCommitHit(step.SessionId, step.StepId, out _), Is.False);
            Assert.That(session.Snapshot.Actors.Single(actor => actor.Key == "warrior").Health, Is.EqualTo(health));
            Assert.That(session.TryCompletePresentation(step.SessionId, step.StepId, out _), Is.True);
            Assert.That(session.Snapshot.CurrentStep.StepId, Is.GreaterThan(step.StepId));
            Assert.That(session.TryCompletePresentation(step.SessionId, step.StepId, out _), Is.False);
        }
        [Test] public void 空背包敌人逐次攻击及首领意图轮换()
        {
            var session = Create(); Reserve(session, "staff_01", 2); Reserve(session, "hat_01", 3); Reserve(session, "bubble_01", 4);
            Assert.That(session.GetEnemyIntents().Select(intent => intent.Kind), Is.EqualTo(new[] { "attack", "attack", "charge" }));
            session.TryConfirm(false, out _);
            Assert.That(session.Snapshot.Phase, Is.EqualTo(BattlePhase.EnemyExecution));
            var warrior = session.GetEnemyIntents().First(); Assert.That(warrior.HitCount, Is.EqualTo(2)); Assert.That(warrior.Damage, Is.EqualTo(3));
            Assert.That(Hit(session).ActorKey, Is.EqualTo("warrior")); Assert.That(session.Snapshot.Hero.Health, Is.EqualTo(15));
            Assert.That(Hit(session).HitIndex, Is.EqualTo(2)); Assert.That(session.Snapshot.Hero.Health, Is.EqualTo(12));
            Assert.That(Hit(session).ActorKey, Is.EqualTo("thief")); Assert.That(session.Snapshot.Hero.Health, Is.EqualTo(8));
            Assert.That(Hit(session).Kind, Is.EqualTo("charge")); Assert.That(session.Snapshot.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(session.Snapshot.Round, Is.EqualTo(2));
            Assert.That(session.GetEnemyIntents().Last().Damage, Is.EqualTo(12));
            Assert.That(session.GetEnemyIntents().Last().Kind, Is.EqualTo("attack"));
        }
        [Test] public void 战士首击击杀立即失败不再第二击()
        {
            var config = Change(ConfigSourceLoader.Load(), "DemoBattles", "demo_battle", "initial_health", "3");
            var session = Create(config); Reserve(session, "staff_01", 2); Reserve(session, "hat_01", 3); Reserve(session, "bubble_01", 4);
            session.TryConfirm(false, out _); var step = Hit(session);
            Assert.That(session.Snapshot.Phase, Is.EqualTo(BattlePhase.Defeat)); Assert.That(session.Snapshot.Hero.Health, Is.Zero);
            Assert.That(session.Snapshot.CurrentStep, Is.Null);
            Assert.That(session.TryCompletePresentation(step.SessionId, step.StepId, out _), Is.False);
            Assert.That(session.DrainEvents().Count(item => item.Kind == "步骤命中"), Is.EqualTo(1));
        }
        [Test] public void 护盾逐击吸收且剩余护盾下轮清除()
        {
            var config = Change(ConfigSourceLoader.Load(), "DemoEffects", "bubble_shield", "value", "20");
            var session = Create(config); Reserve(session, "staff_01", 2); Reserve(session, "hat_01", 3);
            session.TryConfirm(false, out _); Hit(session);
            Assert.That(session.Snapshot.Hero.Shield, Is.EqualTo(20));
            Hit(session); Assert.That(session.Snapshot.Hero.Shield, Is.EqualTo(17));
            Hit(session); Assert.That(session.Snapshot.Hero.Shield, Is.EqualTo(14));
            Hit(session); Assert.That(session.Snapshot.Hero.Shield, Is.EqualTo(10));
            Hit(session); Assert.That(session.Snapshot.Hero.Shield, Is.Zero); Assert.That(session.Snapshot.Hero.Health, Is.EqualTo(18));
            Assert.That(session.DrainEvents().Single(item => item.Kind == "护盾清除").Value, Is.EqualTo(10));
        }
        [Test] public void 部分护盾只扣穿透生命()
        {
            var config = Change(ConfigSourceLoader.Load(), "DemoEffects", "bubble_shield", "value", "4");
            var session = Create(config); Reserve(session, "staff_01", 2); Reserve(session, "hat_01", 3);
            session.TryConfirm(false, out _); Hit(session); Hit(session);
            Assert.That(session.Snapshot.Hero.Shield, Is.EqualTo(1)); Assert.That(session.Snapshot.Hero.Health, Is.EqualTo(18));
            Hit(session); Assert.That(session.Snapshot.Hero.Shield, Is.Zero); Assert.That(session.Snapshot.Hero.Health, Is.EqualTo(16));
        }
        [Test] public void 最后一敌死亡即停止后置药水和泡泡()
        {
            var config = Change(ConfigSourceLoader.Load(), "DemoEffects", "staff_damage", "value", "100");
            var session = Create(config); session.TrySelectTarget("chief", out _); FinishRound(session);
            Assert.That(session.Snapshot.Phase, Is.EqualTo(BattlePhase.Preparation));
            // 第一轮法杖杀首领、帽子伤战士；第二轮法杖杀战士，帽子伤盗贼；第三轮法杖最后击杀。
            FinishRound(session); Move(session, "potion_01", 2, 3);
            session.TryConfirm(false, out _); var last = Hit(session);
            Assert.That(session.Snapshot.Phase, Is.EqualTo(BattlePhase.Victory));
            Assert.That(session.Snapshot.Items.Any(item => item.Key == "potion_01"), Is.True);
            Assert.That(session.Snapshot.Hero.Shield, Is.Zero);
            Assert.That(session.TryCommitHit(last.SessionId, last.StepId, out _), Is.False);
        }
        [Test] public void 目标死亡从头转移按钮循环并拒绝死者()
        {
            var config = Change(ConfigSourceLoader.Load(), "DemoActors", "goblin_thief", "max_health", "1");
            var session = Create(config); session.TrySelectTarget("thief", out _); session.TryConfirm(false, out _); Hit(session);
            Assert.That(session.Snapshot.TargetKey, Is.EqualTo("warrior"), "不能从死去盗贼向后找首领");
            Assert.That(session.Snapshot.Actors.Single(actor => actor.Key == "chief").Health, Is.EqualTo(28), "溢出不转移");
            FinishRound(session); Assert.That(session.TrySelectTarget("thief", out _), Is.False);
            Assert.That(session.TrySelectTarget("hero", out _), Is.False);
            Assert.That(session.TrySelectTarget("missing", out _), Is.False);
            session.TryCycleTarget(out _); Assert.That(session.Snapshot.TargetKey, Is.EqualTo("chief"));
            session.TryCycleTarget(out _); Assert.That(session.Snapshot.TargetKey, Is.EqualTo("warrior"));
        }
        [Test] public void 重开恢复药水首领轮次并使旧凭证失效()
        {
            var old = Create(); AllItems(old); old.TryConfirm(false, out _); var step = Hit(old);
            var fresh = old.Restart(); Assert.That(old.Snapshot.Phase, Is.EqualTo(BattlePhase.Closed));
            Assert.That(fresh.Snapshot.SessionId, Is.Not.EqualTo(old.Snapshot.SessionId));
            Assert.That(fresh.Snapshot.Round, Is.EqualTo(1)); Assert.That(fresh.Snapshot.Hero.Health, Is.EqualTo(18));
            Assert.That(fresh.Snapshot.Items.Count, Is.EqualTo(5)); Assert.That(fresh.GetEnemyIntents().Last().Kind, Is.EqualTo("charge"));
            Assert.That(old.TryCommitHit(step.SessionId, step.StepId, out _), Is.False);
            Assert.That(fresh.TryCommitHit(step.SessionId, step.StepId, out _), Is.False);
            Assert.That(old.TryMove("staff_01", ItemContainer.Backpack, 0, 0, -1, out _), Is.False);
            fresh.Close(); fresh.Close(); Assert.That(fresh.TryConfirm(false, out _), Is.False);
        }
        [Test] public void 后置药水执行前满血静默移除且不消耗()
        {
            var config = Change(ConfigSourceLoader.Load(), "DemoEffects", "potion_heal", "value", "30");
            config = new DemoConfigCatalog(ConfigSchema.All.Select(schema =>
                new KeyValuePair<string, IReadOnlyList<ConfigRecord>>(schema.Name, schema == ConfigSchema.Loadout ?
                    config.Table(schema).Concat(new[] { new ConfigRecord(schema,
                        new[] { "6", "potion_02", "demo_battle", "healing_potion", "backpack", "1", "3", "0", "0" }, 7) }).ToArray() : config.Table(schema))));
            var session = Create(config); Move(session, "potion_01", 0, 0); session.TryConfirm(false, out _);
            Assert.That(session.Snapshot.RemainingPlan.Select(plan => plan.ItemKey).Take(2), Is.EqualTo(new[] { "potion_01", "potion_02" }));
            Hit(session);
            Assert.That(session.Snapshot.Hero.Health, Is.EqualTo(30));
            Assert.That(session.Snapshot.CurrentStep.ItemKey, Is.EqualTo("staff_01"));
            Assert.That(session.Snapshot.RemainingPlan.Any(plan => plan.ItemKey == "potion_02"), Is.False);
            Assert.That(session.Snapshot.Items.Any(item => item.Key == "potion_02"), Is.True);
            Assert.That(session.DrainEvents().Any(item => item.Subject == "potion_02"), Is.False, "失效药水不产生消耗或跳过提示");
        }
        [Test] public void 蓄力游标只在完成本次表现后推进()
        {
            var session = Create(); Reserve(session, "staff_01", 2); Reserve(session, "hat_01", 3); Reserve(session, "bubble_01", 4);
            session.TryConfirm(false, out _); Hit(session); Hit(session); Hit(session);
            var step = session.Snapshot.CurrentStep;
            Assert.That(step.Kind, Is.EqualTo("charge")); Hit(session, false);
            Assert.That(session.GetEnemyIntents().Last().Kind, Is.EqualTo("charge"));
            Assert.That(session.TryCommitHit(step.SessionId, step.StepId, out _), Is.False);
            Assert.That(session.TryCompletePresentation(step.SessionId, step.StepId, out _), Is.True);
            Assert.That(session.GetEnemyIntents().Last().Kind, Is.EqualTo("attack"));
        }
        [Test] public void 结束后多次重开不残留事件和步骤()
        {
            var session = Create();
            for (int repeat = 0; repeat < 3; ++repeat)
            {
                while (session.Snapshot.Phase == BattlePhase.Preparation) FinishRound(session);
                Assert.That(session.Snapshot.Phase, Is.EqualTo(BattlePhase.Victory));
                var previousId = session.Snapshot.SessionId; session = session.Restart();
                Assert.That(session.Snapshot.SessionId, Is.Not.EqualTo(previousId));
                Assert.That(session.Snapshot.CurrentStep, Is.Null);
                Assert.That(session.Snapshot.Items.Count, Is.EqualTo(5));
                Assert.That(session.Snapshot.Actors.All(actor => actor.BehaviorCursor == 0), Is.True);
                Assert.That(session.DrainEvents().Select(item => item.Kind), Is.EqualTo(new[] { "会话开始" }));
            }
        }
    }
}
