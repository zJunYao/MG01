using System.IO;
using System.Linq;
using MG01.Demo.Configuration;
using MG01.Demo.Editor;
using MG01.Demo.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MG01.Demo.Tests
{
    public sealed class BattleSceneBindingTests
    {
        [Test] public void 独立场景保留相机主光完整动作与参考站位且不携带观察脚本()
        {
            var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(DemoBattleSceneBuilder.Path, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single(); var binding = root.GetComponent<BattleSceneBinding>();
                Assert.That(binding.battleCamera, Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<Light>().Any(light => light.type == LightType.Directional), Is.True);
                Assert.That(root.GetComponentsInChildren<AudioListener>(), Is.Empty);
                Assert.That(binding.actors.Select(actor => actor.key), Is.EquivalentTo(new[] { "hero", "warrior", "thief", "chief" }));
                Assert.That(root.GetComponentsInChildren<MonoBehaviour>().All(component => component != null && component.GetType().Namespace != "MG01.Test"), Is.True);
                foreach (var actor in binding.actors)
                {
                    Assert.That(actor.home, Is.EqualTo(actor.animator.transform.position));
                    Assert.That(actor.scale, Is.EqualTo(actor.animator.transform.localScale));
                    Assert.That(actor.animator.gameObject.layer, Is.EqualTo(binding.modelLayer));
                }
                var catalog = ConfigSourceLoader.Load();
                foreach (var timing in catalog.Table(ConfigSchema.Presentations))
                {
                    string key = timing.Key.StartsWith("warrior") ? "warrior" : timing.Key.StartsWith("thief") ? "thief" : timing.Key.StartsWith("chief") ? "chief" : "hero";
                    var clip = binding.Get(key).Find(timing.Text("animation"));
                    Assert.That(clip, Is.Not.Null, timing.Key); Assert.That(clip.length, Is.GreaterThan(0));
                    Assert.That(clip.length / timing.Decimal("speed"), Is.LessThan(timing.Decimal("timeout")), timing.Key);
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); UnityEngine.SceneManagement.SceneManager.SetActiveScene(original); }
        }
        [Test] public void 从参考重新生成保持原场景内容不变()
        {
            var before = File.ReadAllBytes("Assets/Test/Scenes/HeroPreview.unity");
            DemoBattleSceneBuilder.Build();
            Assert.That(File.ReadAllBytes("Assets/Test/Scenes/HeroPreview.unity"), Is.EqualTo(before));
        }
    }
}
