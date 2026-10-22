using System;
using System.Collections.Generic;
using System.Linq;
using MG01.Demo.Configuration;

namespace MG01.Demo.Domain
{
    public sealed class InventoryService
    {
        private readonly DemoConfigCatalog catalog;
        private readonly ConfigRecord battle;
        public InventoryService(DemoConfigCatalog catalog, ConfigRecord battle) { this.catalog = catalog; this.battle = battle; }

        // 校验全部通过才写入；目标占用时拒绝，不实现隐式交换。
        internal bool TryMove(IList<ItemInstance> items, string key, ItemContainer container, int column, int row, int slot, out string reason)
        {
            var item = items.FirstOrDefault(value => value.Key == key);
            if (item == null) { reason = "道具不存在或已消耗"; return false; }
            if (!Enum.IsDefined(typeof(ItemContainer), container)) { reason = "容器无效"; return false; }
            if (container == ItemContainer.Reserve)
            {
                if (slot < 0 || slot >= battle.Integer("reserve_capacity") || items.Any(other => other != item && other.Container == container && other.Slot == slot))
                { reason = "备用槽位越界或已占用"; return false; }
                item.Container = container; item.Column = -1; item.Row = -1; item.Slot = slot; item.Rotation = 0;
            }
            else
            {
                if (!Fits(items, item, column, row, item.Rotation, out reason)) return false;
                item.Container = container; item.Column = column; item.Row = row; item.Slot = -1;
            }
            reason = null; return true;
        }
        internal bool TryRotate(IList<ItemInstance> items, string key, out string reason)
        {
            var item = items.FirstOrDefault(value => value.Key == key);
            if (item == null) { reason = "道具不存在或已消耗"; return false; }
            if (item.Container != ItemContainer.Backpack) { reason = "备用区不能旋转"; return false; }
            if (!catalog.Get(ConfigSchema.Items, item.DefinitionKey).Boolean("rotatable")) { reason = "此道具不能旋转"; return false; }
            int next = (item.Rotation + 1) % 4;
            if (!Fits(items, item, item.Column, item.Row, next, out reason)) return false;
            item.Rotation = next; reason = null; return true;
        }
        private bool Fits(IList<ItemInstance> items, ItemInstance item, int column, int row, int rotation, out string reason)
        {
            var rect = GridRect.At(column, row, rotation, catalog.Get(ConfigSchema.Items, item.DefinitionKey));
            if (column < 0 || row < 0 || rect.Right > battle.Integer("columns") || rect.Bottom > battle.Integer("rows"))
            { reason = "占格越界"; return false; }
            if (items.Any(other => other != item && other.Container == ItemContainer.Backpack && rect.Overlaps(GridRect.For(other, catalog.Get(ConfigSchema.Items, other.DefinitionKey)))))
            { reason = "占格重叠"; return false; }
            reason = null; return true;
        }
    }

    public static class AdjacencyService
    {
        public static int DamageBonus(DemoConfigCatalog catalog, IReadOnlyList<ItemInstance> items, ItemInstance attack)
        {
            if (attack.Container != ItemContainer.Backpack) return 0;
            var definition = catalog.Get(ConfigSchema.Items, attack.DefinitionKey);
            if (definition.Text("category") != "attack") return 0;
            var rect = GridRect.For(attack, definition);
            int total = 0;
            foreach (var other in items.Where(value => value.Container == ItemContainer.Backpack && value.Key != attack.Key))
            {
                var item = catalog.Get(ConfigSchema.Items, other.DefinitionKey);
                var effect = catalog.Get(ConfigSchema.Effects, item.Text("effect_key"));
                if (effect.Text("kind") == "adjacent_bonus" && rect.SharesEdge(GridRect.For(other, item))) total = checked(total + effect.Integer("value"));
            }
            return total;
        }
    }
}
