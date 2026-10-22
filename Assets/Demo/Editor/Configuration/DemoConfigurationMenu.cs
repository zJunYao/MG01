using System;
using System.IO;
using System.Linq;
using System.Text;
using MG01.Demo.Configuration;
using UnityEditor;
using UnityEngine;

namespace MG01.Demo.Editor
{
    public static class DemoConfigurationMenu
    {
        [MenuItem("Demo/配置/检查当前运行时门禁")]
        public static void InspectRuntimeGate()
        {
            if (!EditorApplication.isPlaying || DemoConfigurationSystem.Instance == null)
                throw new InvalidOperationException("请从start场景启动并等待配置系统创建后检查");
            var system = DemoConfigurationSystem.Instance;
            bool accepted = system.TryGetCatalog(out var catalog, out string reason);
            string report = "运行时门禁：状态=" + system.Status + "；允许读取=" + accepted;
            if (accepted)
                report += "；版本=" + catalog.Version + "；法杖基础伤害=" + catalog.Get(ConfigSchema.Effects, "staff_damage").Integer("value");
            else report += "；快照为空=" + (catalog == null) + "；原因=" + reason;
            Debug.Log(report);
        }

        [MenuItem("Demo/配置/校验并导出全部表")]
        public static void ExportAll()
        {
            var catalog = ConfigSourceLoader.Load();
            ConfigValidator.ThrowIfInvalid(catalog);
            VerifyResources(catalog);
            var manifest = DemoConfigExporter.Export(ConfigSourceLoader.SourceDirectory, DemoConfigExporter.GameResourcesDirectory);
            AssetDatabase.Refresh();
            string report = "配置版本：" + manifest.Version + "\n" + string.Join("\n", manifest.Entries.Select(entry => entry.Name + "：" + entry.Count + "条；SHA256=" + entry.Hash));
            Directory.CreateDirectory("Assets/Demo/Documentation/Evidence");
            File.WriteAllText("Assets/Demo/Documentation/Evidence/配置导出报告.txt", report + "\n资源静态检查通过；不等于战斗或最终美术验收。\n", new UTF8Encoding(false));
            Debug.Log("Demo配置导出与框架读回校验完成\n" + report);
        }

        public static void VerifyResources(DemoConfigCatalog catalog)
        {
            foreach (var row in catalog.Table(ConfigSchema.Resources))
            {
                string relative = row.Text("path");
                if (relative.Length == 0) continue;
                string path = DemoConfigExporter.GameResourcesDirectory + "/" + relative;
                if (!File.Exists(path)) throw new FileNotFoundException("资源不存在：" + row.Key, path);
                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (row.Text("kind") == "sprite")
                {
                    var sprites = assets.OfType<Sprite>().ToArray();
                    string sub = row.Text("sub_asset");
                    if ((sub.Length == 0 && sprites.Length != 1) || (sub.Length > 0 && sprites.Count(sprite => sprite.name == sub) != 1))
                        throw new FormatException("Sprite引用不唯一或不存在，请填写sub_asset：" + row.Key);
                }
                else if (row.Text("kind") == "prefab" && !assets.OfType<GameObject>().Any())
                    throw new FormatException("角色或页面资源不是预制体：" + row.Key);
            }
        }
    }
}
