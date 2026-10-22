using System.Linq;
using MG01.Demo.Configuration;
using MG01.Demo.Domain;
using MG01.Demo.Editor;
using NUnit.Framework;
using static MG01.Demo.Tests.BattleTestDriver;

namespace MG01.Demo.Tests
{
    public sealed class InventoryRuleTests
    {
        [Test] public void 初始坐标转换及快照隔离()
        {
            var session = Create(); var old = session.Snapshot;
            var staff = old.Items.Single(item => item.Key == "staff_01");
            Assert.That(new[] { staff.Column, staff.Row, staff.Slot }, Is.EqualTo(new[] { 1, 0, -1 }));
            Assert.That(old.Items.Single(item => item.Key == "runestone_01").Slot, Is.Zero);
            Assert.That(old.Hero.Health, Is.EqualTo(18)); Assert.That(old.Round, Is.EqualTo(1));
            Move(session, "staff_01", 0, 0);
            Assert.That(staff.Column, Is.EqualTo(1), "旧快照不应随会话修改");
            Assert.That(session.Snapshot.Items.Single(item => item.Key == "staff_01").Column, Is.Zero);
        }
        [Test] public void 尺寸旋转四次回原状及方形旋转保留反馈()
        {
            var session = Create(); Move(session, "staff_01", 0, 2);
            var config = ConfigSourceLoader.Load();
            for (int index = 1; index <= 4; ++index)
            {
                Assert.That(session.TryRotate("staff_01", out string reason), Is.True, reason);
                var staff = session.Snapshot.Items.Single(item => item.Key == "staff_01");
                var rect = GridRect.For(staff, config.Get(ConfigSchema.Items, staff.DefinitionKey));
                Assert.That(staff.Rotation, Is.EqualTo(index % 4));
                Assert.That(rect.Width, Is.EqualTo(index % 2 == 1 ? 3 : 1));
                Assert.That(rect.Height, Is.EqualTo(index % 2 == 1 ? 1 : 3));
            }
            Assert.That(session.TryRotate("hat_01", out _), Is.True);
            Assert.That(session.Snapshot.Items.Single(item => item.Key == "hat_01").Rotation, Is.EqualTo(1));
        }
        [TestCase(-1, 0)] [TestCase(7, 0)] [TestCase(0, 3)] [TestCase(3, 1)] [TestCase(int.MaxValue, 0)]
        public void 非法移动不改变任何道具(int column, int row)
        {
            var session = Create(); string old = Items(session);
            Assert.That(session.TryMove("staff_01", ItemContainer.Backpack, column, row, -1, out _), Is.False);
            Assert.That(Items(session), Is.EqualTo(old));
        }
        [Test] public void 备用区满槽与直接交换被拒绝()
        {
            var session = Create(); string old = Items(session);
            Assert.That(session.TryMove("staff_01", ItemContainer.Reserve, -1, -1, 0, out _), Is.False);
            Assert.That(session.TryMove("staff_01", ItemContainer.Reserve, -1, -1, 8, out _), Is.False);
            Assert.That(session.TryMove("staff_01", ItemContainer.Backpack, 3, 1, -1, out _), Is.False);
            Assert.That(Items(session), Is.EqualTo(old));
            Reserve(session, "staff_01", 2); Move(session, "staff_01", 0, 0);
            Assert.That(session.TryRotate("potion_01", out _), Is.False);
        }
        [Test] public void 旋转碰撞和越界原子回退()
        {
            var session = Create(); string old = Items(session);
            // 初始法杖横转会占第4列第1行，帽子从第2行开始，合法。
            Assert.That(session.TryRotate("staff_01", out _), Is.True);
            Move(session, "staff_01", 4, 3);
            old = Items(session);
            Assert.That(session.TryRotate("staff_01", out _), Is.False, "竖转超过下边界");
            Assert.That(Items(session), Is.EqualTo(old));
            var second = Create(); Move(second, "staff_01", 1, 1); old = Items(second);
            Assert.That(second.TryRotate("staff_01", out _), Is.False, "横转与帽子重叠");
            Assert.That(Items(second), Is.EqualTo(old));
        }
        [Test] public void 全部道具合法装入且相邻同时强化两武器()
        {
            var session = Create(); AllItems(session); var config = ConfigSourceLoader.Load();
            foreach (string key in new[] { "staff_01", "hat_01" })
                Assert.That(AdjacencyService.DamageBonus(config, session.Snapshot.Items, session.Snapshot.Items.Single(item => item.Key == key)), Is.EqualTo(2));
            Assert.That(session.Snapshot.Items.All(item => item.Container == ItemContainer.Backpack), Is.True);
        }
        [Test] public void 斜角不加成且长共边只加一次()
        {
            var session = Create(); Move(session, "runestone_01", 2, 3); var config = ConfigSourceLoader.Load();
            var staff = session.Snapshot.Items.Single(item => item.Key == "staff_01");
            Assert.That(AdjacencyService.DamageBonus(config, session.Snapshot.Items, staff), Is.Zero);
            Move(session, "runestone_01", 2, 1);
            Assert.That(AdjacencyService.DamageBonus(config, session.Snapshot.Items, staff), Is.EqualTo(2));
            var a = new GridRect(0, 0, 1, 3); var b = new GridRect(1, 0, 2, 3);
            Assert.That(a.SharesEdge(b), Is.True);
            Assert.That(a.SharesEdge(new GridRect(1, 3, 1, 1)), Is.False);
        }
        [Test] public void 三格共边仍按一个符石加成()
        {
            var config = Change(ConfigSourceLoader.Load(), "DemoItems", "lightning_runestone", "height", "3");
            var session = Create(config); Move(session, "runestone_01", 2, 0);
            var staff = session.Snapshot.Items.Single(item => item.Key == "staff_01");
            Assert.That(AdjacencyService.DamageBonus(config, session.Snapshot.Items, staff), Is.EqualTo(2));
        }
        [Test] public void 容量配置可缩小为满槽且未知道具拒绝()
        {
            var config = Change(ConfigSourceLoader.Load(), "DemoBattles", "demo_battle", "reserve_capacity", "2");
            config = Change(config, "DemoBattles", "demo_battle", "reserve_columns", "2");
            var session = Create(config); string old = Items(session);
            Assert.That(session.TryMove("staff_01", ItemContainer.Reserve, -1, -1, 2, out _), Is.False);
            Assert.That(session.TryMove("staff_01", ItemContainer.Reserve, -1, -1, 0, out _), Is.False);
            Assert.That(session.TryRotate("unknown", out _), Is.False);
            Assert.That(session.TryMove("unknown", ItemContainer.Backpack, 0, 0, -1, out _), Is.False);
            Assert.That(session.TryMove("staff_01", (ItemContainer)99, 0, 0, -1, out _), Is.False);
            Assert.That(Items(session), Is.EqualTo(old));
        }
    }
}
