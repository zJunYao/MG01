using System;
using System.Collections.Generic;
using System.Linq;
using MG01.Demo.Application;
using MG01.Demo.Configuration;
using MG01.Demo.Domain;
using MG01.Demo.Editor;
using NUnit.Framework;

namespace MG01.Demo.Tests
{
    /// <summary>测试即时表现仅提交同一会话凭证，不复制伤害或回合计算逻辑。</summary>
    public static class BattleTestDriver
    {
        public static BattleSession Create(DemoConfigCatalog catalog = null) => new BattleSession(catalog ?? ConfigSourceLoader.Load(), "demo_battle");
        public static void Move(BattleSession session, string key, int column, int row)
        { Assert.That(session.TryMove(key, ItemContainer.Backpack, column, row, -1, out string reason), Is.True, reason); }
        public static void Reserve(BattleSession session, string key, int slot)
        { Assert.That(session.TryMove(key, ItemContainer.Reserve, -1, -1, slot, out string reason), Is.True, reason); }
        public static void AllItems(BattleSession session)
        { Move(session, "runestone_01", 2, 1); Move(session, "potion_01", 0, 0); }
        public static ActionStep Hit(BattleSession session, bool complete = true)
        {
            var step = session.Snapshot.CurrentStep;
            Assert.That(step, Is.Not.Null);
            Assert.That(session.TryCommitHit(step.SessionId, step.StepId, out string reason), Is.True, reason);
            if (complete && session.Snapshot.CurrentStep != null)
                Assert.That(session.TryCompletePresentation(step.SessionId, step.StepId, out reason), Is.True, reason);
            return step;
        }
        public static void FinishRound(BattleSession session, IList<BattleEvent> events = null)
        {
            if (session.Snapshot.Phase == BattlePhase.Preparation)
                Assert.That(session.TryConfirm(false, out string reason), Is.True, reason);
            for (int guard = 0; session.Snapshot.CurrentStep != null && guard < 100; ++guard)
            {
                Hit(session);
                foreach (var item in session.DrainEvents()) events?.Add(item);
            }
            Assert.That(session.Snapshot.CurrentStep, Is.Null, "步骤未能收敛");
        }
        public static DemoConfigCatalog Change(DemoConfigCatalog catalog, string table, string key, string field, string value) =>
            ConfigValidationTests.Change(catalog, table, key, field, value);
        public static string Items(BattleSession session) => string.Join("|", session.Snapshot.Items.Select(item =>
            item.Key + ":" + item.Container + ":" + item.Column + ":" + item.Row + ":" + item.Slot + ":" + item.Rotation));
    }
}
