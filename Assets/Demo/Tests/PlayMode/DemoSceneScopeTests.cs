using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MG01.Demo.Tests
{
    public sealed class DemoSceneScopeTests
    {
        [UnityTest] public IEnumerator 加载中退出立即再次进入不会串用或遗留场景()
        {
            var first = new DemoBattleSceneScope(); var second = new DemoBattleSceneScope();
            bool lateCallback = false, ready = false; string error = null;
            try
            {
                first.Load(_ => lateCallback = true, reason => error = reason); first.Dispose();
                second.Load(_ => ready = true, reason => error = reason);
                float deadline = Time.realtimeSinceStartup + 15;
                while (!ready && error == null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(error, Is.Null); Assert.That(ready, Is.True); Assert.That(lateCallback, Is.False);
                int count = 0;
                for (int i = 0; i < SceneManager.sceneCount; ++i) if (SceneManager.GetSceneAt(i).path == DemoBattleSceneScope.Path) ++count;
                Assert.That(count, Is.EqualTo(1));
                second.Dispose(); deadline = Time.realtimeSinceStartup + 5;
                while (SceneManager.GetSceneByPath(DemoBattleSceneScope.Path).isLoaded && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(SceneManager.GetSceneByPath(DemoBattleSceneScope.Path).isLoaded, Is.False);
            }
            finally { first.Dispose(); second.Dispose(); }
        }
    }
}
