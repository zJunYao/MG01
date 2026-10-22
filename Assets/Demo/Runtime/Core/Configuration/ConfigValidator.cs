using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MG01.Demo.Configuration
{
    public static class ConfigValidator
    {
        public static IReadOnlyList<string> Validate(DemoConfigCatalog catalog)
        {
            var errors = new List<string>();
            void Error(ConfigRecord row, string message) => errors.Add($"{row.Schema.Name} 第{row.SourceLine}行（{row.Key}）：{message}");
            void Check(ConfigRecord row, bool valid, string message) { if (!valid) Error(row, message); }
            void Ref(ConfigRecord row, string field, ConfigSchema target, bool optional = false)
            {
                string key = row.Text(field);
                if (optional && key.Length == 0) return;
                Check(row, catalog.Table(target).Any(other => other.Key == key), $"{field}引用不存在：{key}");
            }
            void Choice(ConfigRecord row, string field, params string[] valid) =>
                Check(row, valid.Contains(row.Text(field)), $"{field}不支持：{row.Text(field)}");
            void Positive(ConfigRecord row, params string[] fields)
            { foreach (string field in fields) Check(row, row.Integer(field) > 0, $"{field}必须为正整数"); }

            foreach (ConfigSchema schema in ConfigSchema.All)
            {
                var rows = catalog.Table(schema);
                if (rows.Count == 0) errors.Add($"{schema.Name}：空表不允许导出");
                foreach (var row in rows)
                {
                    Check(row, row.Id > 0, "id必须为正整数");
                    Check(row, Regex.IsMatch(row.Key, "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$"), "key必须是snake_case标识");
                }
                foreach (var duplicate in rows.GroupBy(row => row.Id).Where(group => group.Count() > 1))
                    Error(duplicate.Last(), $"重复id：{duplicate.Key}");
                foreach (var duplicate in rows.GroupBy(row => row.Key).Where(group => group.Count() > 1))
                    Error(duplicate.Last(), $"重复key：{duplicate.Key}");
            }
            if (catalog.Table(ConfigSchema.Battles).Count != 1) errors.Add("DemoBattles：首版要求且仅允许一场固定战斗");
            if (catalog.Table(ConfigSchema.Interaction).Count != 1) errors.Add("DemoInteraction：要求且仅允许一组交互参数");

            foreach (var row in catalog.Table(ConfigSchema.Items))
            {
                Choice(row, "category", "attack", "passive", "defense", "consumable");
                Positive(row, "width", "height"); Ref(row, "effect_key", ConfigSchema.Effects); Ref(row, "icon_key", ConfigSchema.Resources);
                Check(row, row.Text("name").Length > 0 && row.Text("detail_template").Contains("{value}"), "名称不能为空，详情必须引用{value}");
                var effect = catalog.Table(ConfigSchema.Effects).FirstOrDefault(other => other.Key == row.Text("effect_key"));
                if (effect != null)
                {
                    var mapping = new Dictionary<string, string> { { "attack", "damage" }, { "passive", "adjacent_bonus" }, { "defense", "shield" }, { "consumable", "heal" } };
                    Check(row, mapping.TryGetValue(row.Text("category"), out string kind) && kind == effect.Text("kind"), "道具类别与效果不匹配");
                }
            }
            foreach (var row in catalog.Table(ConfigSchema.Effects))
            {
                Choice(row, "kind", "damage", "adjacent_bonus", "shield", "heal");
                Check(row, row.Integer("value") >= 0, "value不能为负");
                string kind = row.Text("kind");
                string condition = kind == "heal" ? "missing_health" : kind == "adjacent_bonus" ? "shared_edge" : "always";
                string target = kind == "damage" ? "enemy" : kind == "adjacent_bonus" ? "adjacent_attack" : "self";
                Check(row, row.Text("condition") == condition && row.Text("target") == target, "条件或目标不符合当前效果规则");
                Check(row, row.Boolean("consume") == (kind == "heal"), "当前仅治疗消耗品在实际生效后销毁");
                Ref(row, "presentation_key", ConfigSchema.Presentations, kind == "adjacent_bonus");
                Check(row, kind != "adjacent_bonus" || row.Text("presentation_key").Length == 0, "被动增伤不允许独立演出步骤");
            }
            foreach (var row in catalog.Table(ConfigSchema.Actors))
            {
                Positive(row, "max_health"); Ref(row, "resource_key", ConfigSchema.Resources); Ref(row, "portrait_key", ConfigSchema.Resources);
                if (row.Text("behavior_group").Length > 0)
                    Check(row, catalog.Table(ConfigSchema.EnemyActions).Any(action => action.Text("group") == row.Text("behavior_group")), "行为组不存在");
            }
            foreach (var row in catalog.Table(ConfigSchema.EnemyActions))
            {
                Positive(row, "sequence"); Choice(row, "kind", "attack", "charge"); Ref(row, "presentation_key", ConfigSchema.Presentations);
                Check(row, Regex.IsMatch(row.Text("group"), "^[a-z][a-z0-9_]*$"), "行为组标识无效");
                Check(row, row.Text("kind") == "charge" ? row.Integer("hit_count") == 0 && row.Integer("damage") == 0 :
                    row.Integer("hit_count") > 0 && row.Integer("damage") >= 0, "攻击次数或伤害与动作类型不符");
            }
            foreach (var group in catalog.Table(ConfigSchema.EnemyActions).GroupBy(row => row.Text("group")))
                if (!group.Select(row => row.Integer("sequence")).OrderBy(value => value).SequenceEqual(Enumerable.Range(1, group.Count())))
                    Error(group.First(), "行为序号必须从1连续且无重复");

            foreach (var row in catalog.Table(ConfigSchema.Battles))
            {
                Positive(row, "columns", "rows", "reserve_capacity", "reserve_columns", "initial_health"); Ref(row, "hero_key", ConfigSchema.Actors);
                Choice(row, "sort_policy", "position_column_row");
                Check(row, row.Text("version").Length > 0 && row.Text("scene_binding").Length > 0, "版本和场景绑定标识不能为空");
                Check(row, row.Integer("initial_shield") >= 0, "初始护盾不能为负");
                Check(row, row.Integer("reserve_columns") <= row.Integer("reserve_capacity"), "备用展示列数不能超过容量");
                var hero = catalog.Table(ConfigSchema.Actors).FirstOrDefault(actor => actor.Key == row.Text("hero_key"));
                if (hero != null) Check(row, row.Integer("initial_health") <= hero.Integer("max_health") && hero.Text("behavior_group").Length == 0, "主角生命超上限或错误配置敌人行为");
                var roster = catalog.Table(ConfigSchema.Roster).Where(actor => actor.Text("battle_key") == row.Key).ToArray();
                Check(row, roster.Length >= 2, "至少需要主角与一名敌人");
                Check(row, roster.Select(actor => actor.Integer("action_order")).OrderBy(value => value).SequenceEqual(Enumerable.Range(1, roster.Length)), "行动序号必须1起算连续无重复");
                var enemies = roster.Where(actor => actor.Integer("target_order") > 0).ToArray();
                Check(row, enemies.Select(actor => actor.Integer("target_order")).OrderBy(value => value).SequenceEqual(Enumerable.Range(1, enemies.Length)), "敌人目标序号必须连续无重复");
                Check(row, enemies.Any(actor => actor.Key == row.Text("default_target")), "默认目标必须是本场存活敌人的实例标识");
                Check(row, roster.Count(actor => actor.Integer("target_order") == 0 && actor.Text("actor_key") == row.Text("hero_key") && actor.Integer("action_order") == 1) == 1, "主角必须且仅有一个并首先行动");
                Check(row, roster.Select(actor => actor.Text("position_key")).Distinct().Count() == roster.Length, "站位标识重复");
            }
            foreach (var row in catalog.Table(ConfigSchema.Roster))
            {
                Ref(row, "battle_key", ConfigSchema.Battles); Ref(row, "actor_key", ConfigSchema.Actors); Positive(row, "action_order");
                Check(row, row.Integer("target_order") >= 0 && row.Text("position_key").Length > 0, "目标序号或站位无效");
                var actor = catalog.Table(ConfigSchema.Actors).FirstOrDefault(other => other.Key == row.Text("actor_key"));
                if (actor != null) Check(row, (row.Integer("target_order") == 0) == (actor.Text("behavior_group").Length == 0), "主角/敌人与目标顺序不匹配");
            }
            foreach (var row in catalog.Table(ConfigSchema.Loadout))
            { Ref(row, "battle_key", ConfigSchema.Battles); Ref(row, "item_key", ConfigSchema.Items); Choice(row, "container", "backpack", "reserve"); }
            foreach (var row in catalog.Table(ConfigSchema.Resources))
            {
                Choice(row, "kind", "sprite", "prefab", "effect"); Choice(row, "status", "verified", "unverified", "temporary", "pending_user");
                string path = row.Text("path");
                bool placeholder = !row.Boolean("required") && row.Text("kind") == "effect" &&
                    (row.Text("status") == "temporary" || row.Text("status") == "pending_user");
                Check(row, path.Length > 0 || placeholder, "仅非必需临时/待提供效果允许空路径");
                if (path.Length > 0)
                    Check(row, !path.StartsWith("/") && !path.Contains("\\") && !path.Contains(":") && !path.Split('/').Any(part => part == ".." || part == "." || part.Length == 0) &&
                        !path.Any(char.IsWhiteSpace) && !path.StartsWith("Assets/") && path.LastIndexOf('.') > path.LastIndexOf('/'), "资源路径必须相对于GameResources，含后缀且无穿越或空白");
                Check(row, row.Text("kind") == "sprite" || row.Text("sub_asset").Length == 0, "仅sprite支持子资源名");
            }
            foreach (var row in catalog.Table(ConfigSchema.Presentations))
            {
                Ref(row, "effect_key", ConfigSchema.Resources, true);
                Ref(row, "next_hit_presentation", ConfigSchema.Presentations, true);
                Check(row, row.Text("animation").Length > 0, "动画状态不能为空");
                Check(row, row.Decimal("hit_time") >= 0 && row.Decimal("hit_time") <= row.Decimal("end_time") && row.Decimal("end_time") <= 1, "归一化时点应满足0≤命中≤结束≤1");
                Check(row, row.Decimal("speed") > 0 && row.Decimal("move_speed") >= 0 && row.Decimal("timeout") > 0, "演出速度或超时无效");
                Check(row, row.Decimal("arrival_tolerance") > 0 && row.Decimal("arrival_tolerance") <= .1, "到位误差必须大于0且不超过0.1世界单位");
            }
            foreach (var row in catalog.Table(ConfigSchema.Reactions))
            {
                Ref(row, "actor_key", ConfigSchema.Actors);
                Check(row, row.Decimal("speed") > 0 && row.Decimal("flash_duration") >= 0 && row.Decimal("flash_interval") > 0, "受击速度和闪烁间隔必须为正，闪烁时长不能为负");
            }
            foreach (var actor in catalog.Table(ConfigSchema.Actors))
                Check(actor, catalog.Table(ConfigSchema.Reactions).Count(row => row.Text("actor_key") == actor.Key) == 1, "每个角色必须配置且只能配置一条受击表现");
            foreach (var row in catalog.Table(ConfigSchema.Interaction))
            {
                Check(row, row.Decimal("double_click_seconds") > 0 && row.Decimal("drag_threshold") > 0, "交互识别参数必须为正");
                Positive(row, "reference_width", "reference_height"); Choice(row, "rotation_anchor", "top_left");
            }
            errors.AddRange(InitialLayoutValidator.Validate(catalog));
            return errors.AsReadOnly();
        }

        public static void ThrowIfInvalid(DemoConfigCatalog catalog)
        {
            var errors = Validate(catalog);
            if (errors.Count > 0) throw new FormatException(string.Join("\n", errors));
        }
    }
}
