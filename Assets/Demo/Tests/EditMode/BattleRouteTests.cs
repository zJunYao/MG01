using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MG01.Demo.Configuration;
using MG01.Demo.Domain;
using MG01.Demo.Editor;
using NUnit.Framework;
using static MG01.Demo.Tests.BattleTestDriver;

namespace MG01.Demo.Tests
{
    public sealed class BattleRouteTests
    {
        [TestCase("初始配置先战士", false, "warrior", "", new[] { 18, 11, 11, 8, 8 })]
        [TestCase("全部道具先战士", true, "warrior", "", new[] { 28, 25, 25, 25 })]
        [TestCase("全部道具先首领", true, "chief", "", new[] { 27, 26, 26, 26 })]
        [TestCase("不装符石先首领", true, "chief", "runestone_01", new[] { 27, 26, 26, 26, 26 })]
        [TestCase("不装泡泡先首领", true, "chief", "bubble_01", new[] { 18, 8, 4, 4 })]
        public void 五条路线逐轮复现(string route, bool all, string target, string excluded, int[] expected)
        {
            var catalog = ConfigSourceLoader.Load(); var session = Create(catalog);
            if (all) AllItems(session);
            if (excluded.Length > 0) Reserve(session, excluded, 2);
            Assert.That(session.TrySelectTarget(target, out string reason), Is.True, reason);
            var log = new StringBuilder("路线=" + route + "\n配置版本=" + catalog.Version + "\n会话=" + session.Snapshot.SessionId + "\n初始布局=" + Items(session) + "\n");
            var actual = new List<int>();
            for (int guard = 0; guard < 20 && session.Snapshot.Phase == BattlePhase.Preparation; ++guard)
            {
                int round = session.Snapshot.Round;
                var events = new List<BattleEvent>(); foreach (var item in session.DrainEvents()) events.Add(item);
                FinishRound(session, events);
                actual.Add(session.Snapshot.Hero.Health);
                foreach (var entry in events)
                    log.Append("轮=").Append(entry.Round).Append(" 步骤=").Append(entry.StepId).Append(' ').Append(entry.Kind).Append(' ').Append(entry.Subject)
                        .Append(" 数值=").Append(entry.Value).Append(' ').AppendLine(entry.Detail);
                log.Append("第").Append(round).Append("轮结束：生命=").Append(session.Snapshot.Hero.Health).Append(" 状态=").Append(session.Snapshot.Phase)
                    .Append(" 敌人=").AppendLine(string.Join("；", session.Snapshot.Actors.Where(actor => actor.TargetOrder > 0).Select(actor => actor.Key + ":" + actor.Health)));
            }
            string output = "Assets/Demo/Documentation/Evidence/Rules"; Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, route + ".txt"), log.ToString(), new UTF8Encoding(false));
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(session.Snapshot.Phase, Is.EqualTo(BattlePhase.Victory));
            Assert.That(session.Snapshot.Round, Is.EqualTo(expected.Length));
        }

        [Test] public void 反向摆法改变伤害分配但不复制计算逻辑()
        {
            foreach (bool reversed in new[] { false, true })
            {
                var catalog = Change(ConfigSourceLoader.Load(), "DemoActors", "goblin_chief", "max_health", "8");
                var session = Create(catalog); AllItems(session);
                if (reversed)
                {
                    // 先腾出旧占格，全部通过真实移动命令构造，不直接写测试状态。
                    Reserve(session, "hat_01", 2); Move(session, "staff_01", 4, 0);
                    Move(session, "runestone_01", 3, 1); Move(session, "hat_01", 1, 1);
                }
                session.TrySelectTarget("chief", out _); session.TryConfirm(false, out _);
                Hit(session); Hit(session); Hit(session);
                Assert.That(session.Snapshot.Actors.Single(actor => actor.Key == "chief").Health, Is.Zero);
                Assert.That(session.Snapshot.Actors.Single(actor => actor.Key == "warrior").Health, Is.EqualTo(reversed ? 6 : 2));
            }
        }
        [Test] public void 配置变化直接驱动实际结算()
        {
            var catalog = Change(ConfigSourceLoader.Load(), "DemoEffects", "staff_damage", "value", "7");
            var session = Create(catalog); session.TryConfirm(false, out _);
            Assert.That(session.Snapshot.CurrentStep.Value, Is.EqualTo(7)); Hit(session);
            Assert.That(session.Snapshot.Actors.Single(actor => actor.Key == "warrior").Health, Is.EqualTo(7));
        }
    }
}
