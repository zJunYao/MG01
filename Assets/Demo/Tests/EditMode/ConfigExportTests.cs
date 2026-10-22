using System;
using System.IO;
using System.Linq;
using MG01.Demo.Configuration;
using MG01.Demo.Editor;
using MG01.Demo.Infrastructure;
using NUnit.Framework;

namespace MG01.Demo.Tests
{
    public sealed class ConfigExportTests
    {
        private string root;
        [SetUp] public void 创建隔离目录()
        {
            root = Path.Combine("C:/Users/John/AppData/Local/Temp/opencode", "demo-config-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }
        [TearDown] public void 清理自建目录() { if (Directory.Exists(root)) Directory.Delete(root, true); }

        [Test] public void 全表使用真实框架往返()
        {
            var catalog = ConfigSourceLoader.Load();
            foreach (var schema in ConfigSchema.All)
                DemoTableCodec.VerifyRoundTrip(schema, catalog.Table(schema), DemoTableCodec.Encode(schema, catalog.Table(schema)));
            DemoConfigurationMenu.VerifyResources(catalog);
        }

        [Test] public void 中文转义和整数边界可读回()
        {
            var catalog = ConfigValidationTests.Change(ConfigSourceLoader.Load(), "DemoItems", "lightning_staff", "name", "中文,\"名字\"\n下一行");
            catalog = ConfigValidationTests.Change(catalog, "DemoItems", "lightning_staff", "id", int.MaxValue.ToString());
            DemoTableCodec.VerifyRoundTrip(ConfigSchema.Items, catalog.Table(ConfigSchema.Items), DemoTableCodec.Encode(ConfigSchema.Items, catalog.Table(ConfigSchema.Items)));
        }

        [Test] public void 空表与错误记录数被拒绝()
        {
            Assert.Throws<FormatException>(() => DemoTableCodec.Encode(ConfigSchema.Items, Array.Empty<ConfigRecord>()));
            var rows = ConfigSourceLoader.Load().Table(ConfigSchema.Items);
            var bytes = DemoTableCodec.Encode(ConfigSchema.Items, rows);
            Assert.Throws<FormatException>(() => DemoTableCodec.Decode(ConfigSchema.Items, bytes, rows.Count - 1));
        }

        [Test] public void 导出清单读回与哈希一致()
        {
            var manifest = DemoConfigExporter.Export(ConfigSourceLoader.SourceDirectory, root);
            var decoded = DemoBatchManifest.Parse(File.ReadAllText(Path.Combine(root, DemoConfigExporter.ManifestPath))).Read(ReadTable);
            Assert.That(decoded.Version, Is.EqualTo(manifest.Version));
            Assert.That(decoded.Table(ConfigSchema.Items).Count, Is.EqualTo(5));
        }

        [Test] public void 提交中途失败还原旧批次()
        {
            DemoConfigExporter.Export(ConfigSourceLoader.SourceDirectory, root);
            var paths = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            var original = paths.ToDictionary(path => path, File.ReadAllBytes);
            Assert.Throws<IOException>(() => DemoConfigExporter.Export(ConfigSourceLoader.SourceDirectory, root,
                index => { if (index == 3) throw new IOException("模拟磁盘提交失败"); }));
            foreach (var pair in original) Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value));
            Assert.That(Directory.GetDirectories(root, ".demo-config-*"), Is.Empty);
        }

        [Test] public void 源表校验失败不覆盖已有产物()
        {
            DemoConfigExporter.Export(ConfigSourceLoader.SourceDirectory, root);
            string manifest = File.ReadAllText(Path.Combine(root, DemoConfigExporter.ManifestPath));
            string source = Path.Combine(root, "源表"); Directory.CreateDirectory(source);
            foreach (var path in Directory.GetFiles(ConfigSourceLoader.SourceDirectory, "*.csv")) File.Copy(path, Path.Combine(source, Path.GetFileName(path)));
            string invalid = Path.Combine(source, "DemoLoadout.csv");
            File.WriteAllText(invalid, File.ReadAllText(invalid).Replace("backpack,4,2", "backpack,2,2"));
            Assert.Throws<FormatException>(() => DemoConfigExporter.Export(source, root));
            Assert.That(File.ReadAllText(Path.Combine(root, DemoConfigExporter.ManifestPath)), Is.EqualTo(manifest));
        }

        [Test] public void 改表重导改变实际读取值并拒绝混批()
        {
            var first = DemoConfigExporter.Export(ConfigSourceLoader.SourceDirectory, root);
            var old = ReadTable("DemoEffects");
            string source = Path.Combine(root, "源表"); Directory.CreateDirectory(source);
            foreach (var path in Directory.GetFiles(ConfigSourceLoader.SourceDirectory, "*.csv")) File.Copy(path, Path.Combine(source, Path.GetFileName(path)));
            string changed = Path.Combine(source, "DemoEffects.csv");
            File.WriteAllText(changed, File.ReadAllText(changed).Replace("staff_damage,damage,6,", "staff_damage,damage,7,"));
            var second = DemoConfigExporter.Export(source, root);
            Assert.That(first.Read(name => name == "DemoEffects" ? old : ReadTable(name)).Get(ConfigSchema.Effects, "staff_damage").Integer("value"), Is.EqualTo(6));
            Assert.That(second.Read(ReadTable).Get(ConfigSchema.Effects, "staff_damage").Integer("value"), Is.EqualTo(7));
            Assert.Throws<FormatException>(() => first.Read(ReadTable));
        }

        [Test] public void 截断篡改和格式版本错误被拒绝()
        {
            var manifest = DemoConfigExporter.Export(ConfigSourceLoader.SourceDirectory, root);
            byte[] bytes = ReadTable("DemoItems"); bytes[0] ^= 1;
            Assert.Throws<FormatException>(() => manifest.Read(name => name == "DemoItems" ? bytes : ReadTable(name)));
            Assert.Throws<FormatException>(() => manifest.Read(name => name == "DemoItems" ? null : ReadTable(name)));
            Assert.Throws<FormatException>(() => DemoBatchManifest.Parse(manifest.Serialize().Replace("格式\t1", "格式\t99")));
        }
        private byte[] ReadTable(string name) => File.ReadAllBytes(Path.Combine(root, "Excel", name + ".bytes"));
    }
}
