using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MG01.Demo.Configuration;

namespace MG01.Demo.Infrastructure
{
    public static class DemoTableCodec
    {
        public const int FormatVersion = 1;
        public static byte[] Encode(ConfigSchema schema, IReadOnlyList<ConfigRecord> rows)
        {
            if (rows.Count == 0) throw new FormatException($"{schema.Name}：禁止导出空表");
            var writer = new SerializerWrite();
            foreach (var row in rows)
            {
                if (row.Schema != schema) throw new FormatException("导出行模式不匹配");
                foreach (var field in schema.Fields)
                {
                    switch (field.Kind)
                    {
                        case ConfigValueKind.Integer: writer.write(row.Integer(field.Name)); break;
                        case ConfigValueKind.Decimal: writer.write(row.Decimal(field.Name)); break;
                        case ConfigValueKind.Boolean: writer.write((byte)(row.Boolean(field.Name) ? 1 : 0)); break;
                        default: writer.writeString(row.Text(field.Name)); break;
                    }
                }
            }
            var bytes = new byte[writer.getDataSize()];
            Buffer.BlockCopy(writer.getBuffer(), 0, bytes, 0, bytes.Length);
            // 与ExcelTable.decodeFile互逆；变换不是安全加密，哈希用于一致性校验。
            string key;
            using (var md5 = MD5.Create()) key = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes("ASLD" + schema.Name))).Replace("-", "") + "23y35y983";
            for (int index = 0; index < bytes.Length; ++index)
                bytes[index] = (byte)(((bytes[index] - ((index << 1) & 0xFF)) & 0xFF) ^ (byte)key[index % key.Length]);
            return bytes;
        }

        public static IReadOnlyList<ConfigRecord> Decode(ConfigSchema schema, byte[] input, int expectedRows)
        {
            if (input == null || input.Length == 0 || expectedRows <= 0) throw new FormatException($"{schema.Name}：内容或记录数无效");
            var bytes = (byte[])input.Clone();
            ExcelTable.decodeFile(bytes, schema.Name);
            var reader = new SerializerRead(); reader.init(bytes);
            var rows = new List<ConfigRecord>();
            var ids = new HashSet<int>(); var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < expectedRows; ++index)
            {
                if (reader.getIndex() >= reader.getDataSize()) throw new FormatException($"{schema.Name}：缺少第{index + 1}条数据");
                var row = DemoExcelRow.Create(schema);
                if (!row.read(reader)) throw new FormatException($"{schema.Name}：第{index + 1}条数据读取失败");
                if (!ids.Add(row.Record.Id) || !keys.Add(row.Record.Key)) throw new FormatException($"{schema.Name}：重复行号或业务标识");
                rows.Add(row.Record);
            }
            if (reader.getIndex() != reader.getDataSize()) throw new FormatException($"{schema.Name}：清单记录数与实际字节长度不符");
            return rows.AsReadOnly();
        }

        public static void VerifyRoundTrip(ConfigSchema schema, IReadOnlyList<ConfigRecord> original, byte[] bytes)
        {
            var decoded = Decode(schema, bytes, original.Count);
            for (int index = 0; index < original.Count; ++index)
                if (!original[index].ToCells().SequenceEqual(decoded[index].ToCells()))
                    throw new FormatException($"{schema.Name}：第{index + 1}条导出后读回不一致");
        }
        public static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
