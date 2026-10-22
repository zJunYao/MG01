using System;
using System.Collections;
using System.Collections.Generic;
using MG01.Demo.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UObject = UnityEngine.Object;

namespace MG01.Demo.Tests
{
    public sealed class DemoResourceScopeTests
    {
        private sealed class Loader : IDemoAssetLoader
        {
            public Action<IDemoAssetLease> Pending;
            public void Load(string path, Action<IDemoAssetLease> completed) { Pending = completed; }
        }
        private sealed class Lease : IDemoAssetLease
        {
            public UObject Asset { get; set; }
            public IReadOnlyList<UObject> Children { get; set; } = Array.Empty<UObject>();
            public int Releases;
            public void Dispose() { ++Releases; }
        }
        [UnityTest] public IEnumerator 迟到加载释放且不回调已退出页面()
        {
            var loader = new Loader(); var scope = new DemoResourceScope(loader); bool called = false;
            var texture = new Texture2D(4, 4); var lease = new Lease { Asset = texture };
            scope.Load("延迟测试", value => called = true); scope.Dispose(); loader.Pending(lease);
            Assert.That(called, Is.False); Assert.That(lease.Releases, Is.EqualTo(1));
            scope.Dispose(); Assert.That(lease.Releases, Is.EqualTo(1)); UObject.Destroy(texture); yield return null;
        }
        [UnityTest] public IEnumerator 子Sprite跟随主纹理引用直到域退出()
        {
            var loader = new Loader(); var scope = new DemoResourceScope(loader);
            var texture = new Texture2D(4, 4); var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero); sprite.name = "测试子图";
            var lease = new Lease { Asset = texture, Children = new UObject[] { sprite } };
            Sprite selected = null; scope.Load("主纹理", value => selected = DemoResourceScope.FindSprite(value, "测试子图")); loader.Pending(lease);
            Assert.That(selected, Is.SameAs(sprite)); Assert.That(lease.Releases, Is.Zero);
            scope.Dispose(); Assert.That(lease.Releases, Is.EqualTo(1)); UObject.Destroy(sprite); UObject.Destroy(texture); yield return null;
        }
    }
}
