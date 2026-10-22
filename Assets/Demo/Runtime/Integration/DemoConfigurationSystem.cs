using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MG01.Demo.Configuration;
using MG01.Demo.Infrastructure;
using UnityEngine;

namespace MG01.Demo
{
    public enum DemoConfigurationStatus { NotStarted, Loading, Ready, Failed }

    /// <summary>随框架启动，先验证配置，不初始化战斗。进入Demo时必须通过TryGetCatalog门禁。</summary>
    public sealed class DemoConfigurationSystem : FrameSystem
    {
        public const string ManifestResource = "Demo/Configuration/DemoBatch.txt";
        public static DemoConfigurationSystem Instance { get; private set; }
        public DemoConfigurationStatus Status { get; private set; }
        public string Error { get; private set; }
        private readonly Dictionary<string, DemoExcelTable> tables = new Dictionary<string, DemoExcelTable>();
        private DemoConfigCatalog catalog;
        private int generation;
        private DateTime deadline;

        public DemoConfigurationSystem() { Instance = this; }
        public void RegisterTables()
        {
            foreach (var schema in ConfigSchema.All)
            {
                var row = DemoExcelRow.Create(schema);
                var table = (DemoExcelTable)FrameBaseHotFix.mExcelManager.registe(schema.Name, typeof(DemoExcelTable), row.GetType());
                table.Schema = schema;
                // 此处晚于框架的resourceAvailable广播；启动资源就绪后新注册的表需显式接收就绪状态。
                table.setResourceAvailable(true);
                tables.Add(schema.Name, table);
            }
        }
        public override void init()
        {
            base.init();
            Status = DemoConfigurationStatus.Loading;
            deadline = DateTime.UtcNow.AddSeconds(30);
            int request = ++generation;
            try
            {
                FrameBaseHotFix.mResourceManager.loadGameResourceAsync<TextAsset>(ManifestResource, (ResourceRef<TextAsset> handle) =>
                {
                    try
                    {
                        if (request != generation || Status != DemoConfigurationStatus.Loading) return;
                        var asset = handle?.get();
                        if (asset == null) throw new FileNotFoundException("配置清单缺失：" + ManifestResource);
                        var manifest = DemoBatchManifest.Parse(asset.text);
                        var records = new List<KeyValuePair<string, IReadOnlyList<ConfigRecord>>>();
                        foreach (var entry in manifest.Entries)
                            records.Add(new KeyValuePair<string, IReadOnlyList<ConfigRecord>>(entry.Name, tables[entry.Name].ParseVerified(entry)));
                        var snapshot = new DemoConfigCatalog(records);
                        ConfigValidator.ThrowIfInvalid(snapshot);
                        if (snapshot.Version != manifest.Version) throw new FormatException("清单与战斗配置版本不一致");
                        catalog = snapshot; Status = DemoConfigurationStatus.Ready;
                        Debug.Log("Demo配置已通过运行时门禁，版本：" + catalog.Version + "；道具数：" + catalog.Table(ConfigSchema.Items).Count);
                        WriteProbe(true, null);
                    }
                    catch (Exception exception) { Fail(exception.Message); }
                    finally { if (handle != null) FrameBaseHotFix.mResourceManager.unload(ref handle); }
                });
            }
            catch (Exception exception) { Fail(exception.Message); }
        }
        public bool TryGetCatalog(out DemoConfigCatalog result, out string reason)
        {
            result = Status == DemoConfigurationStatus.Ready ? catalog : null;
            reason = Status == DemoConfigurationStatus.Failed ? Error : "Demo配置尚未加载校验完成";
            return result != null;
        }
        public override void update(float elapsedTime)
        {
            base.update(elapsedTime);
            if (Status == DemoConfigurationStatus.Loading && DateTime.UtcNow >= deadline) Fail("Demo配置清单加载超时，已阻止开战");
        }
        private void Fail(string reason)
        {
            if (Status == DemoConfigurationStatus.Failed) return;
            ++generation; catalog = null; Error = reason; Status = DemoConfigurationStatus.Failed;
            Debug.LogError("Demo配置加载失败：" + reason);
            WriteProbe(false, reason);
        }
        public override void destroy()
        {
            ++generation; catalog = null; tables.Clear();
            if (Instance == this) Instance = null;
            base.destroy();
        }
        private void WriteProbe(bool success, string reason)
        {
            // 仅诊断开关写证据；不在正常游戏中自动退出或写入项目源码目录。
            if (!Environment.GetCommandLineArgs().Contains("-demo-config-smoke") && Environment.GetEnvironmentVariable("MG01_DEMO_CONFIG_PROBE") != "1") return;
            try
            {
                string directory = Path.Combine(UnityEngine.Application.persistentDataPath, "Demo", "Evidence"); Directory.CreateDirectory(directory);
                string content = "配置启动验证=" + (success ? "通过" : "失败") + "\n运行环境=" + UnityEngine.Application.platform + "\n时间=" + DateTime.UtcNow.ToString("O") + "\n";
                if (success)
                    content += "配置版本=" + catalog.Version + "\n道具数=" + catalog.Table(ConfigSchema.Items).Count +
                        "\n法杖基础伤害=" + catalog.Get(ConfigSchema.Effects, "staff_damage").Integer("value") + "\n";
                else content += "原因=" + reason + "\n";
                File.WriteAllText(Path.Combine(directory, "配置启动验证.txt"), content, new UTF8Encoding(false));
                Debug.Log("Demo配置启动证据：" + directory);
            }
            catch (Exception exception) { Debug.LogWarning("Demo诊断写入失败：" + exception.Message); }
        }
    }
}
