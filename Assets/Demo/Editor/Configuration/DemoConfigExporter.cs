using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MG01.Demo.Configuration;
using MG01.Demo.Infrastructure;

namespace MG01.Demo.Editor
{
    public static class DemoConfigExporter
    {
        public const string GameResourcesDirectory = "Assets/GameResources";
        public const string ManifestPath = "Demo/Configuration/DemoBatch.txt";

        public static DemoBatchManifest Export(string sourceDirectory, string resourceDirectory, Action<int> beforeCommitFile = null)
        {
            var catalog = ConfigSourceLoader.Load(sourceDirectory);
            ConfigValidator.ThrowIfInvalid(catalog);
            var files = new Dictionary<string, byte[]>();
            var entries = new List<DemoBatchManifest.Entry>();
            foreach (var schema in ConfigSchema.All)
            {
                var rows = catalog.Table(schema);
                byte[] bytes = DemoTableCodec.Encode(schema, rows);
                DemoTableCodec.VerifyRoundTrip(schema, rows, bytes);
                files.Add("Excel/" + schema.Name + ".bytes", bytes);
                entries.Add(new DemoBatchManifest.Entry(schema.Name, rows.Count, DemoTableCodec.Hash(bytes)));
            }
            var manifest = new DemoBatchManifest(catalog.Version, entries);
            manifest.Read(name => files["Excel/" + name + ".bytes"]);
            // 清单最后提交，运行时拒绝中断留下的新旧混批；异常时回滚原始文件。
            files.Add(ManifestPath, new UTF8Encoding(false).GetBytes(manifest.Serialize()));
            Commit(resourceDirectory, files, beforeCommitFile);
            return manifest;
        }

        private static void Commit(string root, Dictionary<string, byte[]> files, Action<int> beforeCommitFile)
        {
            string transaction = Path.Combine(Path.GetFullPath(root), ".demo-config-" + Guid.NewGuid().ToString("N"));
            var previous = new Dictionary<string, byte[]>();
            var changed = new List<string>();
            Directory.CreateDirectory(transaction);
            try
            {
                foreach (var pair in files)
                {
                    string stage = Path.Combine(transaction, pair.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(stage));
                    File.WriteAllBytes(stage, pair.Value);
                    if (!File.ReadAllBytes(stage).SequenceEqual(pair.Value)) throw new IOException("配置暂存读回失败：" + pair.Key);
                    string destination = Path.Combine(root, pair.Key);
                    previous.Add(pair.Key, File.Exists(destination) ? File.ReadAllBytes(destination) : null);
                }
                int index = 0;
                foreach (var pair in files)
                {
                    beforeCommitFile?.Invoke(index++);
                    string destination = Path.Combine(root, pair.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    changed.Add(pair.Key);
                    // 暂存文件位于同一卷，单文件替换具有原子性；meta不随替换改变。
                    if (File.Exists(destination)) File.Replace(Path.Combine(transaction, pair.Key), destination, null);
                    else File.Move(Path.Combine(transaction, pair.Key), destination);
                }
            }
            catch
            {
                foreach (string path in changed.AsEnumerable().Reverse())
                {
                    string destination = Path.Combine(root, path);
                    if (previous[path] == null) File.Delete(destination);
                    else File.WriteAllBytes(destination, previous[path]);
                }
                throw;
            }
            finally
            {
                // 仅删除本方法创建的唯一暂存目录，绝不清空资源根目录。
                if (Directory.Exists(transaction)) Directory.Delete(transaction, true);
            }
        }
    }
}
