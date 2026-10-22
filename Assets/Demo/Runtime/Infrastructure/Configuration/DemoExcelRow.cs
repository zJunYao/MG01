using System;
using System.Globalization;
using MG01.Demo.Configuration;
using UnityEngine.Scripting;

namespace MG01.Demo.Infrastructure
{
    /// <summary>ExcelData的强校验适配，字段顺序来自显式模式；不以日志代替解析结果。</summary>
    [Preserve]
    public abstract class DemoExcelRow : ExcelData
    {
        public abstract ConfigSchema Schema { get; }
        public ConfigRecord Record { get; private set; }
        public override bool read(SerializerRead reader)
        {
            var cells = new string[Schema.Fields.Count];
            for (int index = 0; index < cells.Length; ++index)
            {
                var field = Schema.Fields[index];
                bool success;
                switch (field.Kind)
                {
                    case ConfigValueKind.Integer:
                        success = reader.read(out int integer);
                        cells[index] = integer.ToString(CultureInfo.InvariantCulture); break;
                    case ConfigValueKind.Decimal:
                        success = reader.read(out double number);
                        cells[index] = number.ToString("R", CultureInfo.InvariantCulture); break;
                    case ConfigValueKind.Boolean:
                        success = reader.read(out byte boolean);
                        if (boolean > 1) return false;
                        cells[index] = boolean == 1 ? "true" : "false"; break;
                    default:
                        int start = reader.getIndex();
                        // 先核验字符串字节数，避免损坏的表触发超大分配或框架读越界日志。
                        if (!reader.read(out int length) || length < 0 || length > reader.getDataSize() - reader.getIndex()) return false;
                        reader.setIndex(start);
                        success = reader.readString(out cells[index]); break;
                }
                if (!success) return false;
            }
            Record = new ConfigRecord(Schema, cells, 0);
            mID = Record.Id;
            return true;
        }

        public static DemoExcelRow Create(ConfigSchema schema)
        {
            if (schema == ConfigSchema.Items) return new DemoItemsRow();
            if (schema == ConfigSchema.Effects) return new DemoEffectsRow();
            if (schema == ConfigSchema.Actors) return new DemoActorsRow();
            if (schema == ConfigSchema.EnemyActions) return new DemoEnemyActionsRow();
            if (schema == ConfigSchema.Battles) return new DemoBattlesRow();
            if (schema == ConfigSchema.Roster) return new DemoRosterRow();
            if (schema == ConfigSchema.Loadout) return new DemoLoadoutRow();
            if (schema == ConfigSchema.Resources) return new DemoResourcesRow();
            if (schema == ConfigSchema.Presentations) return new DemoPresentationsRow();
            if (schema == ConfigSchema.Interaction) return new DemoInteractionRow();
            if (schema == ConfigSchema.Reactions) return new DemoReactionsRow();
            throw new ArgumentException("未知Demo表模式");
        }
    }
    [Preserve] public sealed class DemoItemsRow : DemoExcelRow { public override ConfigSchema Schema => ConfigSchema.Items; }
    [Preserve] public sealed class DemoEffectsRow : DemoExcelRow { public override ConfigSchema Schema => ConfigSchema.Effects; }
    [Preserve] public sealed class DemoActorsRow : DemoExcelRow { public override ConfigSchema Schema => ConfigSchema.Actors; }
    [Preserve] public sealed class DemoEnemyActionsRow : DemoExcelRow { public override ConfigSchema Schema => ConfigSchema.EnemyActions; }
    [Preserve] public sealed class DemoBattlesRow : DemoExcelRow { public override ConfigSchema Schema => ConfigSchema.Battles; }
    [Preserve] public sealed class DemoRosterRow : DemoExcelRow { public override ConfigSchema Schema => ConfigSchema.Roster; }
    [Preserve] public sealed class DemoLoadoutRow : DemoExcelRow { public override ConfigSchema Schema => ConfigSchema.Loadout; }
    [Preserve] public sealed class DemoResourcesRow : DemoExcelRow { public override ConfigSchema Schema => ConfigSchema.Resources; }
    [Preserve] public sealed class DemoPresentationsRow : DemoExcelRow { public override ConfigSchema Schema => ConfigSchema.Presentations; }
    [Preserve] public sealed class DemoInteractionRow : DemoExcelRow { public override ConfigSchema Schema => ConfigSchema.Interaction; }
    [Preserve] public sealed class DemoReactionsRow : DemoExcelRow { public override ConfigSchema Schema => ConfigSchema.Reactions; }
}
