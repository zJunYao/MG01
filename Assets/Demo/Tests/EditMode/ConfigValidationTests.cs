using System;
using System.Collections.Generic;
using System.Linq;
using MG01.Demo.Configuration;
using MG01.Demo.Editor;
using NUnit.Framework;

namespace MG01.Demo.Tests
{
    public sealed class ConfigValidationTests
    {
        [Test] public void 源表数值和初始状态与规范一致()
        {
            var catalog = ConfigSourceLoader.Load();
            Assert.That(ConfigValidator.Validate(catalog), Is.Empty);
            Assert.That(catalog.Table(ConfigSchema.Items).Count, Is.EqualTo(5));
            Assert.That(catalog.Get(ConfigSchema.Effects, "staff_damage").Integer("value"), Is.EqualTo(6));
            Assert.That(catalog.Get(ConfigSchema.Effects, "hat_damage").Integer("value"), Is.EqualTo(10));
            Assert.That(catalog.Get(ConfigSchema.Effects, "adjacent_damage").Integer("value"), Is.EqualTo(2));
            Assert.That(catalog.Get(ConfigSchema.Effects, "bubble_shield").Integer("value"), Is.EqualTo(9));
            Assert.That(catalog.Get(ConfigSchema.Effects, "potion_heal").Integer("value"), Is.EqualTo(10));
            Assert.That(catalog.Get(ConfigSchema.EnemyActions, "warrior_attack").Integer("hit_count"), Is.EqualTo(2));
            Assert.That(catalog.Get(ConfigSchema.EnemyActions, "warrior_attack").Integer("damage"), Is.EqualTo(3));
            Assert.That(catalog.Get(ConfigSchema.Battles, "demo_battle").Integer("initial_health"), Is.EqualTo(18));
        }

        [TestCase("DemoItems", "lightning_staff", "id", "2", "重复id")]
        [TestCase("DemoItems", "lightning_staff", "key", "thunder_hat", "重复key")]
        [TestCase("DemoItems", "lightning_staff", "effect_key", "missing", "引用不存在")]
        [TestCase("DemoActors", "hero", "max_health", "-1", "必须为正")]
        [TestCase("DemoEnemyActions", "chief_attack", "sequence", "3", "连续")]
        [TestCase("DemoLoadout", "hat_01", "column", "2", "重叠")]
        [TestCase("DemoLoadout", "staff_01", "column", "8", "越界")]
        [TestCase("DemoLoadout", "potion_01", "slot", "1", "重复")]
        [TestCase("DemoLoadout", "potion_01", "slot", "9", "越界")]
        [TestCase("DemoLoadout", "potion_01", "rotation", "1", "不旋转")]
        [TestCase("DemoResources", "staff_icon", "path", "../outside.png", "穿越")]
        [TestCase("DemoResources", "staff_icon", "path", "", "空路径")]
        public void 拒绝非法配置(string table, string key, string field, string value, string expected)
        {
            Assert.That(ConfigValidator.Validate(Change(ConfigSourceLoader.Load(), table, key, field, value)), Has.Some.Contains(expected));
        }

        public static DemoConfigCatalog Change(DemoConfigCatalog source, string table, string key, string field, string value)
        {
            return new DemoConfigCatalog(ConfigSchema.All.Select(schema => new KeyValuePair<string, IReadOnlyList<ConfigRecord>>(schema.Name,
                source.Table(schema).Select(row =>
                {
                    if (schema.Name != table || row.Key != key) return row;
                    var cells = row.ToCells(); cells[schema.Fields.Select(column => column.Name).ToList().IndexOf(field)] = value;
                    return new ConfigRecord(schema, cells, row.SourceLine);
                }).ToArray())));
        }
    }
}
