using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MG01.Demo.Configuration
{
    public sealed class CsvRow
    {
        public int Line { get; }
        public string[] Cells { get; }
        public CsvRow(int line, string[] cells) { Line = line; Cells = cells; }
    }

    /// <summary>严格处理引号、转义和跨行单元格，同时保留原文件物理行号。</summary>
    public static class CsvConfigReader
    {
        public static IReadOnlyList<CsvRow> Parse(string text, string source)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var rows = new List<CsvRow>();
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false, closed = false, started = false;
            int line = 1, rowLine = 1;
            int start = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
            for (int index = start; index < text.Length; ++index)
            {
                char value = text[index];
                if (quoted)
                {
                    if (value == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"') { cell.Append('"'); ++index; }
                        else { quoted = false; closed = true; }
                    }
                    else if (value == '\r' || value == '\n')
                    {
                        if (value == '\r' && index + 1 < text.Length && text[index + 1] == '\n') ++index;
                        cell.Append('\n'); ++line;
                    }
                    else cell.Append(value);
                    continue;
                }
                if (value == ',' || value == '\r' || value == '\n')
                {
                    cells.Add(cell.ToString()); cell.Clear(); closed = false; started = false;
                    if (value == ',') continue;
                    if (value == '\r' && index + 1 < text.Length && text[index + 1] == '\n') ++index;
                    rows.Add(new CsvRow(rowLine, cells.ToArray())); cells.Clear();
                    rowLine = ++line;
                    continue;
                }
                if (closed) throw new FormatException($"{source} 第{line}行：闭引号后只能出现分隔符或换行");
                if (value == '"')
                {
                    if (started) throw new FormatException($"{source} 第{line}行：非引号字段中出现双引号");
                    quoted = true; started = true;
                }
                else { cell.Append(value); started = true; }
            }
            if (quoted) throw new FormatException($"{source} 第{rowLine}行：字段引号未闭合");
            if (started || closed || cells.Count > 0)
            {
                cells.Add(cell.ToString()); rows.Add(new CsvRow(rowLine, cells.ToArray()));
            }
            return rows.AsReadOnly();
        }

        public static IReadOnlyList<ConfigRecord> Read(ConfigSchema schema, string text)
        {
            var rows = Parse(text, schema.Name);
            if (rows.Count < 2) throw new FormatException($"{schema.Name}：表头或数据为空");
            if (!rows[0].Cells.SequenceEqual(schema.Fields.Select(field => field.Name)))
                throw new FormatException($"{schema.Name} 第1行：表头名称或顺序与模式不一致");
            return Array.AsReadOnly(rows.Skip(1).Select(row => new ConfigRecord(schema, row.Cells, row.Line)).ToArray());
        }
    }
}
