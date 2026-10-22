using System;
using System.Collections.Generic;
using MG01.Demo.Configuration;
using UnityEngine.Scripting;

namespace MG01.Demo.Infrastructure
{
    /// <summary>框架负责异步预加载；显式解析前先对原始密文字节校验哈希，避免吞错和就地解码污染缓存。</summary>
    [Preserve]
    public sealed class DemoExcelTable : ExcelTable
    {
        public ConfigSchema Schema { get; set; }
        public byte[] RawBytes { get; private set; }
        public string LoadError { get; private set; }
        // 不触发基类延迟解析：本表由配置系统按批次哈希、记录数和引用整体强制校验。
        public override void checkAllData() { }
        protected override void onOpenFile()
        {
            try
            {
                var asset = mTableFileData?.get();
                if (asset == null) { LoadError = "表格预加载失败：" + mTableName; return; }
                RawBytes = (byte[])asset.bytes.Clone();
            }
            catch (Exception exception) { LoadError = "表格读取失败：" + mTableName + "；" + exception.Message; }
            finally { FrameBaseHotFix.mResourceManager?.unload(ref mTableFileData); }
        }
        public IReadOnlyList<ConfigRecord> ParseVerified(DemoBatchManifest.Entry entry)
        {
            if (LoadError != null) throw new FormatException(LoadError);
            if (RawBytes == null || DemoTableCodec.Hash(RawBytes) != entry.Hash) throw new FormatException(Schema.Name + "：缺失或哈希不符");
            return DemoTableCodec.Decode(Schema, RawBytes, entry.Count);
        }
        public override void clear() { RawBytes = null; LoadError = null; }
    }
}
