using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MG01.Demo.Configuration;

namespace MG01.Demo.Infrastructure
{
    /// <summary>仅存派生元数据，不维护第二份战斗配置；用固定文本格式避免JSON运行依赖。</summary>
    public sealed class DemoBatchManifest
    {
        public sealed class Entry
        {
            public string Name { get; }
            public int Count { get; }
            public string Hash { get; }
            public Entry(string name, int count, string hash) { Name = name; Count = count; Hash = hash; }
        }
        public string Version { get; }
        public IReadOnlyList<Entry> Entries { get; }
        public DemoBatchManifest(string version, IEnumerable<Entry> entries)
        {
            if (string.IsNullOrWhiteSpace(version) || version.Any(char.IsWhiteSpace)) throw new FormatException("清单版本不能为空或包含空白");
            Version = version; Entries = Array.AsReadOnly(entries.ToArray());
            if (Entries.Count != ConfigSchema.All.Count || Entries.Select(entry => entry.Name).Distinct().Count() != Entries.Count ||
                ConfigSchema.All.Any(schema => !Entries.Any(entry => entry.Name == schema.Name))) throw new FormatException("清单表集合不完整或重复");
            foreach (var entry in Entries)
                if (entry.Count <= 0 || entry.Hash.Length != 64 || entry.Hash.Any(value => !"0123456789abcdef".Contains(value)))
                    throw new FormatException($"清单条目无效：{entry.Name}");
        }
        public string Serialize()
        {
            var result = new StringBuilder().Append("格式\t").Append(DemoTableCodec.FormatVersion).Append("\n版本\t").Append(Version).Append('\n');
            foreach (var entry in Entries) result.Append(entry.Name).Append('\t').Append(entry.Count).Append('\t').Append(entry.Hash).Append('\n');
            return result.ToString();
        }
        public static DemoBatchManifest Parse(string text)
        {
            var lines = text.Replace("\r", "").TrimEnd('\n').Split('\n');
            if (lines.Length != ConfigSchema.All.Count + 2 || lines[0] != "格式\t" + DemoTableCodec.FormatVersion || !lines[1].StartsWith("版本\t", StringComparison.Ordinal))
                throw new FormatException("配置清单格式、版本或长度不匹配");
            var entries = new List<Entry>();
            foreach (var line in lines.Skip(2))
            {
                var fields = line.Split('\t');
                if (fields.Length != 3 || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int count)) throw new FormatException("配置清单条目格式无效");
                entries.Add(new Entry(fields[0], count, fields[2]));
            }
            return new DemoBatchManifest(lines[1].Substring(3), entries);
        }
        public DemoConfigCatalog Read(Func<string, byte[]> readTable)
        {
            var tables = new List<KeyValuePair<string, IReadOnlyList<ConfigRecord>>>();
            foreach (var entry in Entries)
            {
                byte[] bytes = readTable(entry.Name);
                if (bytes == null || DemoTableCodec.Hash(bytes) != entry.Hash) throw new FormatException($"{entry.Name}：缺表或哈希不符，拒绝混合配置批次");
                tables.Add(new KeyValuePair<string, IReadOnlyList<ConfigRecord>>(entry.Name, DemoTableCodec.Decode(ConfigSchema.Find(entry.Name), bytes, entry.Count)));
            }
            var catalog = new DemoConfigCatalog(tables);
            ConfigValidator.ThrowIfInvalid(catalog);
            if (catalog.Version != Version) throw new FormatException("清单与战斗配置版本不一致");
            return catalog;
        }
    }
}
