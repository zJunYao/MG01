using System.Collections.Generic;
using System.Linq;

namespace MG01.Demo.Configuration
{
    public static class InitialLayoutValidator
    {
        public static IReadOnlyList<string> Validate(DemoConfigCatalog catalog)
        {
            var errors = new List<string>();
            foreach (var battle in catalog.Table(ConfigSchema.Battles))
            {
                var occupied = new HashSet<long>();
                var slots = new HashSet<int>();
                foreach (var row in catalog.Table(ConfigSchema.Loadout).Where(value => value.Text("battle_key") == battle.Key))
                {
                    void Error(string message) => errors.Add($"DemoLoadout 第{row.SourceLine}行（{row.Key}）：{message}");
                    var item = catalog.Table(ConfigSchema.Items).FirstOrDefault(value => value.Key == row.Text("item_key"));
                    if (item == null) continue;
                    int rotation = row.Integer("rotation");
                    if (rotation < 0 || rotation > 3 || (!item.Boolean("rotatable") && rotation != 0))
                    { Error("旋转次数无效"); continue; }
                    if (row.Text("container") == "reserve")
                    {
                        int slot = row.Integer("slot");
                        if (slot < 1 || slot > battle.Integer("reserve_capacity") || !slots.Add(slot)) Error("备用槽位越界或重复");
                        if (row.Integer("column") != 0 || row.Integer("row") != 0 || rotation != 0) Error("备用区坐标应为0且不旋转");
                        continue;
                    }
                    if (row.Text("container") != "backpack") continue;
                    int width = item.Integer(rotation % 2 == 0 ? "width" : "height");
                    int height = item.Integer(rotation % 2 == 0 ? "height" : "width");
                    int column = row.Integer("column") - 1, top = row.Integer("row") - 1;
                    if (row.Integer("slot") != 0) Error("背包道具备用槽位必须为0");
                    if (width <= 0 || height <= 0 || column < 0 || top < 0 ||
                        (long)column + width > battle.Integer("columns") || (long)top + height > battle.Integer("rows"))
                    { Error("初始占格越界或尺寸无效"); continue; }
                    for (int x = column; x < column + width; ++x)
                        for (int y = top; y < top + height; ++y)
                            if (!occupied.Add(((long)x << 32) | (uint)y)) Error("初始占格重叠");
                }
            }
            return errors.AsReadOnly();
        }
    }
}
