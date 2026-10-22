using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace MG01.Demo.Infrastructure
{
    public interface IDemoAssetLease : IDisposable
    {
        UObject Asset { get; }
        IReadOnlyList<UObject> Children { get; }
    }
    public interface IDemoAssetLoader
    {
        void Load(string path, Action<IDemoAssetLease> completed);
    }
    /// <summary>框架引用封装；Sprite子资源不单独引用，始终保留主纹理。</summary>
    public sealed class FrameworkDemoAssetLoader : IDemoAssetLoader
    {
        public void Load(string path, Action<IDemoAssetLease> completed)
        {
            FrameBaseHotFix.mResourceManager.loadGameResourceAsync<UObject>(path,
                (ResourceRef<UObject> handle, UObject[] children, byte[] bytes, string loadedPath) => completed(new Lease(handle, children)), false);
        }
        private sealed class Lease : IDemoAssetLease
        {
            private ResourceRef<UObject> handle;
            public UObject Asset => handle?.get();
            public IReadOnlyList<UObject> Children { get; }
            public Lease(ResourceRef<UObject> handle, UObject[] children) { this.handle = handle; Children = children ?? Array.Empty<UObject>(); }
            public void Dispose()
            {
                if (handle == null) return;
                FrameBaseHotFix.mResourceManager.unload(ref handle);
            }
        }
    }
    public sealed class DemoResourceScope : IDisposable
    {
        private readonly IDemoAssetLoader loader;
        private readonly List<IDemoAssetLease> leases = new List<IDemoAssetLease>();
        private readonly List<GameObject> instances = new List<GameObject>();
        private bool disposed;
        public DemoResourceScope(IDemoAssetLoader loader) { this.loader = loader; }
        public void Load(string path, Action<IDemoAssetLease> completed)
        {
            if (disposed) return;
            loader.Load(path, lease =>
            {
                if (disposed) { lease?.Dispose(); return; }
                if (lease == null || lease.Asset == null) { lease?.Dispose(); completed(null); return; }
                leases.Add(lease); completed(lease);
            });
        }
        public GameObject Instantiate(GameObject prefab, Transform parent)
        {
            if (disposed) throw new ObjectDisposedException(nameof(DemoResourceScope));
            var instance = UObject.Instantiate(prefab, parent, false); instances.Add(instance); return instance;
        }
        public static Sprite FindSprite(IDemoAssetLease lease, string subAsset)
        {
            if (lease == null) return null;
            if (lease.Asset is Sprite sprite && (string.IsNullOrEmpty(subAsset) || sprite.name == subAsset)) return sprite;
            return lease.Children.OfType<Sprite>().FirstOrDefault(value => string.IsNullOrEmpty(subAsset) || value.name == subAsset);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var instance in instances) if (instance != null) { instance.SetActive(false); UObject.Destroy(instance); }
            instances.Clear();
            // 销毁实例之后再释放资源。Unity延迟Destroy期间对象已经停用，不再渲染。
            foreach (var lease in leases) lease.Dispose(); leases.Clear();
        }
    }
}
