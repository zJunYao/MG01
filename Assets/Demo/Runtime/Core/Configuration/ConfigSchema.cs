using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MG01.Demo.Configuration
{
    public enum ConfigValueKind { Text, Integer, Decimal, Boolean }

    /// <summary>显式字段顺序同时用于CSV与二进制编码，不依赖反射字段枚举顺序。</summary>
    public sealed class ConfigField
    {
        public string Name { get; }
        public ConfigValueKind Kind { get; }
        public ConfigField(string name, ConfigValueKind kind) { Name = name; Kind = kind; }
    }

    public sealed class ConfigSchema
    {
        public string Name { get; }
        public ReadOnlyCollection<ConfigField> Fields { get; }
        public ConfigSchema(string name, params ConfigField[] fields)
        {
            Name = name;
            Fields = Array.AsReadOnly(fields);
        }
        private static ConfigField S(string name) => new ConfigField(name, ConfigValueKind.Text);
        private static ConfigField I(string name) => new ConfigField(name, ConfigValueKind.Integer);
        private static ConfigField F(string name) => new ConfigField(name, ConfigValueKind.Decimal);
        private static ConfigField B(string name) => new ConfigField(name, ConfigValueKind.Boolean);

        public static readonly ConfigSchema Items = new ConfigSchema("DemoItems",
            I("id"), S("key"), S("name"), S("category"), I("width"), I("height"), B("rotatable"),
            S("effect_key"), S("icon_key"), S("detail_template"));
        public static readonly ConfigSchema Effects = new ConfigSchema("DemoEffects",
            I("id"), S("key"), S("kind"), I("value"), S("condition"), S("target"), B("consume"), S("presentation_key"));
        public static readonly ConfigSchema Actors = new ConfigSchema("DemoActors",
            I("id"), S("key"), S("name"), I("max_health"), S("resource_key"), S("portrait_key"), S("behavior_group"));
        public static readonly ConfigSchema EnemyActions = new ConfigSchema("DemoEnemyActions",
            I("id"), S("key"), S("group"), I("sequence"), S("kind"), I("hit_count"), I("damage"), S("presentation_key"));
        public static readonly ConfigSchema Battles = new ConfigSchema("DemoBattles",
            I("id"), S("key"), S("version"), I("columns"), I("rows"), I("reserve_capacity"), I("reserve_columns"),
            S("hero_key"), I("initial_health"), I("initial_shield"), S("sort_policy"), S("default_target"), S("scene_binding"));
        public static readonly ConfigSchema Roster = new ConfigSchema("DemoRoster",
            I("id"), S("key"), S("battle_key"), S("actor_key"), I("action_order"), I("target_order"), S("position_key"));
        public static readonly ConfigSchema Loadout = new ConfigSchema("DemoLoadout",
            I("id"), S("key"), S("battle_key"), S("item_key"), S("container"), I("column"), I("row"), I("rotation"), I("slot"));
        public static readonly ConfigSchema Resources = new ConfigSchema("DemoResources",
            I("id"), S("key"), S("kind"), S("path"), S("sub_asset"), B("required"), S("status"));
        public static readonly ConfigSchema Presentations = new ConfigSchema("DemoPresentations",
            I("id"), S("key"), S("animation"), F("hit_time"), F("end_time"), F("speed"), F("move_speed"), S("effect_key"), F("timeout"),
            F("approach_offset_x"), F("approach_offset_y"), F("arrival_tolerance"), S("next_hit_presentation"));
        public static readonly ConfigSchema Interaction = new ConfigSchema("DemoInteraction",
            I("id"), S("key"), F("double_click_seconds"), F("drag_threshold"), I("reference_width"), I("reference_height"), S("rotation_anchor"));
        public static readonly ConfigSchema Reactions = new ConfigSchema("DemoReactions",
            I("id"), S("key"), S("actor_key"), S("animation"), F("speed"), F("flash_duration"), F("flash_interval"));

        public static readonly IReadOnlyList<ConfigSchema> All = Array.AsReadOnly(new[] {
            Items, Effects, Actors, EnemyActions, Battles, Roster, Loadout, Resources, Presentations, Interaction, Reactions });
        public static ConfigSchema Find(string name) => All.Single(schema => schema.Name == name);
    }
}
