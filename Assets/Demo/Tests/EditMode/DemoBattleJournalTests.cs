using System;
using System.Collections.Generic;
using MG01.Demo.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace MG01.Demo.Tests
{
    public sealed class DemoBattleJournalTests
    {
        [Test] public void 结构化日志含前后快照且正确转义中文换行()
        {
            var lines = new List<string>(); var session = BattleTestDriver.Create();
            var journal = new DemoBattleJournal("测试版", lines.Add); var before = session.Snapshot;
            session.TryConfirm(false, out _); var step = session.Snapshot.CurrentStep; BattleTestDriver.Hit(session, false);
            journal.Record("命中", before, session.Snapshot, step, "中文\n引号\"");
            var entry = JsonUtility.FromJson<DemoBattleJournal.Entry>(lines[0]);
            Assert.That(entry.detail, Is.EqualTo("中文\n引号\"")); Assert.That(entry.before.items.Length, Is.EqualTo(5));
            Assert.That(entry.before.actors[1].health, Is.EqualTo(14)); Assert.That(entry.after.actors[1].health, Is.EqualTo(8));
            Assert.That(entry.step, Is.EqualTo(step.StepId)); Assert.That(entry.version, Is.EqualTo("测试版"));
        }
        [Test] public void 写入失败不改变会话且停用后续写入()
        {
            int attempts = 0; var session = BattleTestDriver.Create(); var before = session.Snapshot;
            var journal = new DemoBattleJournal("测试版", _ => { ++attempts; throw new System.IO.IOException("模拟磁盘失败"); });
            Assert.DoesNotThrow(() => journal.Record("开始", null, before));
            journal.Record("再次写入", before, before); Assert.That(attempts, Is.EqualTo(1)); Assert.That(journal.Error, Is.EqualTo("模拟磁盘失败"));
            session.TryConfirm(false, out _); BattleTestDriver.Hit(session); Assert.That(session.Snapshot.Actors[1].Health, Is.EqualTo(8));
        }
    }
}
