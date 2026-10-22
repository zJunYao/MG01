using System;
using System.Linq;
using System.Collections.Generic;
using MG01.Demo.Views;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MG01.Demo
{
    /// <summary>每次进入加载一次；加载未完成先退出时，完成回调只负责卸载。</summary>
    public sealed class DemoBattleSceneScope : IDisposable
    {
        public const string Path = "Assets/Demo/Scenes/DemoBattle.unity";
        private bool disposed;
        private bool requested, unloading;
        private static readonly Queue<DemoBattleSceneScope> waiting = new Queue<DemoBattleSceneScope>();
        private static DemoBattleSceneScope owner;
        private Action<BattleSceneBinding> onReady;
        private Action<string> onFailed;
        private Scene loaded;
        private readonly Dictionary<Camera, int> cameraMasks = new Dictionary<Camera, int>();
        public BattleSceneBinding Binding { get; private set; }
        public void Load(Action<BattleSceneBinding> ready, Action<string> failed)
        {
            if (requested || disposed) throw new InvalidOperationException("场景域不能重复加载或在释放后加载");
            requested = true; onReady = ready; onFailed = failed;
            waiting.Enqueue(this); Pump();
        }
        private static void Pump()
        {
            if (owner != null) return;
            while (waiting.Count > 0)
            {
                var next = waiting.Dequeue(); if (next.disposed) continue;
                owner = next;
                try { next.StartLoad(); }
                catch (Exception exception) { next.onFailed(exception.Message); next.ReleaseOwner(); }
                return;
            }
        }
        private void ReleaseOwner()
        { if (owner == this) owner = null; Pump(); }
        private void StartLoad()
        {
            var operation = SceneManager.LoadSceneAsync(Path, LoadSceneMode.Additive);
            if (operation == null) throw new InvalidOperationException("无法加载Demo战斗场景");
            operation.completed += _ =>
            {
                loaded = SceneManager.GetSceneByPath(Path);
                if (disposed) { Unload(); return; }
                try
                {
                    Binding = loaded.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BattleSceneBinding>()).Single();
                    // 既有主相机不能在备战RT之外重复画出同一组角色和背景。
                    foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>())
                    {
                        if (camera.gameObject.scene == loaded) continue;
                        cameraMasks.Add(camera, camera.cullingMask); camera.cullingMask &= ~(1 << Binding.modelLayer);
                    }
                    onReady(Binding);
                }
                catch (Exception exception) { onFailed(exception.Message); }
            };
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (Binding != null) Binding.gameObject.SetActive(false);
            foreach (var entry in cameraMasks) if (entry.Key != null) entry.Key.cullingMask = entry.Value;
            cameraMasks.Clear();
            if (loaded.IsValid() && loaded.isLoaded) Unload();
        }
        private void Unload()
        {
            if (unloading) return; unloading = true;
            if (!loaded.IsValid() || !loaded.isLoaded) { ReleaseOwner(); return; }
            var operation = SceneManager.UnloadSceneAsync(loaded);
            if (operation == null) { ReleaseOwner(); return; }
            operation.completed += _ => ReleaseOwner();
        }
    }
}
