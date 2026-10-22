using System;
using MG01.Demo.Configuration;
using NUnit.Framework;

namespace MG01.Demo.Tests
{
    public sealed class CsvConfigReaderTests
    {
        [Test]
        public void 解析字节顺序标记和转义字段()
        {
            var rows = CsvConfigReader.Parse("\uFEFF名称,说明\r\n药水,\"恢复,包含\"\"引号\"\"\r\n第二行\"\r\n", "测试表");
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].Cells[0], Is.EqualTo("名称"));
            Assert.That(rows[1].Cells[1], Is.EqualTo("恢复,包含\"引号\"\n第二行"));
        }

        [Test]
        public void 末尾空字段不会丢失()
        {
            Assert.That(CsvConfigReader.Parse("1,2,", "测试表")[0].Cells, Is.EqualTo(new[] { "1", "2", "" }));
        }

        [TestCase("1,\"未闭合", "第1行")]
        [TestCase("1,\"已闭合\"额外", "第1行")]
        [TestCase("1,未转义\"引号", "第1行")]
        [TestCase("首行\n\"多行\n未闭合", "第2行")]
        public void 错误包含物理行号(string input, string expected)
        {
            Assert.That(Assert.Throws<FormatException>(() => CsvConfigReader.Parse(input, "错误表")).Message, Does.Contain(expected));
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        public void 拒绝非有限小数(string invalid)
        {
            Assert.Throws<FormatException>(() => new ConfigRecord(ConfigSchema.Interaction,
                new[] { "1", "default", invalid, "10", "1920", "1080", "top_left" }, 2));
        }
    }
}
