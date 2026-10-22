using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace MG01.Demo.Configuration
{
    /// <summary>不可变配置行。值在入口按模式严格转换，业务查询没有数值默认值。</summary>
    public sealed class ConfigRecord
    {
        private readonly IReadOnlyDictionary<string, object> values;
        public ConfigSchema Schema { get; }
        public int SourceLine { get; }
        public int Id => Integer("id");
        public string Key => Text("key");

        public ConfigRecord(ConfigSchema schema, IEnumerable<string> cells, int sourceLine)
        {
            Schema = schema;
            SourceLine = sourceLine;
            string[] input = cells.ToArray();
            if (input.Length != schema.Fields.Count)
                throw new FormatException($"{schema.Name} 第{sourceLine}行：列数应为{schema.Fields.Count}，实际为{input.Length}");
            var parsed = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int index = 0; index < input.Length; ++index)
            {
                ConfigField field = schema.Fields[index];
                string text = input[index];
                try
                {
                    switch (field.Kind)
                    {
                        case ConfigValueKind.Integer:
                            parsed.Add(field.Name, int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)); break;
                        case ConfigValueKind.Decimal:
                            double number = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                            if (double.IsNaN(number) || double.IsInfinity(number)) throw new FormatException();
                            parsed.Add(field.Name, number); break;
                        case ConfigValueKind.Boolean:
                            if (text != "true" && text != "false") throw new FormatException();
                            parsed.Add(field.Name, text == "true"); break;
                        default: parsed.Add(field.Name, text); break;
                    }
                }
                catch (Exception exception) when (exception is FormatException || exception is OverflowException)
                {
                    throw new FormatException($"{schema.Name} 第{sourceLine}行 字段{field.Name}：无效的{field.Kind}值“{text}”", exception);
                }
            }
            values = new ReadOnlyDictionary<string, object>(parsed);
        }
        public string Text(string name) => (string)values[name];
        public int Integer(string name) => (int)values[name];
        public double Decimal(string name) => (double)values[name];
        public bool Boolean(string name) => (bool)values[name];
        public object Value(string name) => values[name];
        public string[] ToCells() => Schema.Fields.Select(field =>
            field.Kind == ConfigValueKind.Boolean ? (Boolean(field.Name) ? "true" : "false") :
            Convert.ToString(values[field.Name], CultureInfo.InvariantCulture)).ToArray();
    }

    /// <summary>运行时快照与框架表的可变行隔离；实例状态不得写回这里。</summary>
    public sealed class DemoConfigCatalog
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<ConfigRecord>> tables;
        public DemoConfigCatalog(IEnumerable<KeyValuePair<string, IReadOnlyList<ConfigRecord>>> source)
        {
            var copy = new Dictionary<string, IReadOnlyList<ConfigRecord>>(StringComparer.Ordinal);
            foreach (var table in source) copy.Add(table.Key, Array.AsReadOnly(table.Value.ToArray()));
            tables = new ReadOnlyDictionary<string, IReadOnlyList<ConfigRecord>>(copy);
        }
        public IReadOnlyList<ConfigRecord> Table(ConfigSchema schema) => tables[schema.Name];
        public ConfigRecord Get(ConfigSchema schema, string key) => Table(schema).Single(row => row.Key == key);
        public string Version => Table(ConfigSchema.Battles).Single().Text("version");
    }
}
