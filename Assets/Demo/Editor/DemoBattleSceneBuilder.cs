using System;
using System.IO;
using System.Linq;
using MG01.Demo.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MG01.Demo.Editor
{
    public static class DemoBattleSceneBuilder
    {
        public const string Path = "Assets/Demo/Scenes/DemoBattle.unity";
        [MenuItem("Demo/场景/提取参考并生成独立战斗场景")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行");
            var original = SceneManager.GetActiveScene();
            var reference = EditorSceneManager.OpenScene("Assets/Test/Scenes/HeroPreview.unity", OpenSceneMode.Additive);
            Scene generated = default;
            try
            {
                var roots = reference.GetRootGameObjects();
                // 不引用Test程序集；仅在编辑器通过序列化字段读取观察资产。
                var heroTool = roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).Single(component => component != null && component.GetType().Name == "HeroPreview");
                var monsterTool = heroTool.GetComponents<MonoBehaviour>().Single(component => component.GetType().Name == "MonsterPreview");
                var heroData = new SerializedObject(heroTool); var monsterData = new SerializedObject(monsterTool);
                var camera = (Camera)heroData.FindProperty("previewCamera").objectReferenceValue;
                // 先保存参考副本再清空副本，兼容测试运行器存在未命名临时场景的情况；不保存或关闭用户场景。
                Directory.CreateDirectory("Assets/Demo/Scenes");
                EditorSceneManager.SaveScene(reference, Path, true);
                generated = EditorSceneManager.OpenScene(Path, OpenSceneMode.Additive); SceneManager.SetActiveScene(generated);
                foreach (var copiedRoot in generated.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(copiedRoot);
                var root = new GameObject("DemoBattleRoot"); var binding = root.AddComponent<BattleSceneBinding>();
                var cameraObject = UnityEngine.Object.Instantiate(camera.gameObject, root.transform); cameraObject.name = "BattleCamera";
                binding.battleCamera = cameraObject.GetComponent<Camera>(); binding.battleCamera.depth = 10; binding.battleCamera.cullingMask = 1 << 8;
                foreach (var listener in cameraObject.GetComponents<AudioListener>()) UnityEngine.Object.DestroyImmediate(listener);
                binding.battleCamera.enabled = false;
                var light = new GameObject("MainLight").AddComponent<Light>(); light.type = LightType.Directional; light.transform.SetParent(root.transform); light.transform.rotation = Quaternion.Euler(50, -30, 0);
                var list = new System.Collections.Generic.List<BattleSceneBinding.Actor>();
                var hero = (Animator)heroData.FindProperty("hero").objectReferenceValue;
                list.Add(CopyActor("hero", hero, heroData.FindProperty("clips"), hero.transform.position, root.transform));
                var monsters = monsterData.FindProperty("actors"); string[] keys = { "warrior", "thief", "chief" };
                for (int i = 0; i < monsters.arraySize; ++i)
                {
                    var entry = monsters.GetArrayElementAtIndex(i);
                    list.Add(CopyActor(keys[i], (Animator)entry.FindPropertyRelative("animator").objectReferenceValue, entry.FindPropertyRelative("clips"), entry.FindPropertyRelative("attackPosition").vector3Value, root.transform));
                }
                binding.actors = list.ToArray();
                var report = new System.Text.StringBuilder("# 参考绑定与动作时序核对\n\n[VERIFIED] 以下数据由Unity编辑器读取实际场景与AnimationClip，不是手写估计。\n\n");
                foreach (var actor in binding.actors)
                {
                    report.AppendLine("- " + actor.key + "：出生=" + actor.home.ToString("F3") + "，缩放=" + actor.scale.ToString("F3") + "，近身=" + actor.approach.ToString("F3"));
                }
                report.AppendLine("\n## 时序\n\n归一化命中点为当前配置值，已验证逻辑同步，不代表最终美术接触帧验收。\n");
                foreach (var timing in ConfigSourceLoader.Load().Table(MG01.Demo.Configuration.ConfigSchema.Presentations))
                {
                    string key = timing.Key.StartsWith("warrior") ? "warrior" : timing.Key.StartsWith("thief") ? "thief" : timing.Key.StartsWith("chief") ? "chief" : "hero";
                    var clip = binding.Get(key).Find(timing.Text("animation"));
                    if (clip == null) throw new InvalidOperationException("绑定缺少动作：" + timing.Key);
                    double duration = clip.length / timing.Decimal("speed");
                    report.AppendLine("- " + timing.Key + "：片段=" + clip.name + "，片长=" + clip.length.ToString("F3") + "秒，命中=" + (duration * timing.Decimal("hit_time")).ToString("F3") + "秒，完成=" + (duration * timing.Decimal("end_time")).ToString("F3") + "秒，移动速度=" + timing.Decimal("move_speed"));
                }
                Directory.CreateDirectory("Assets/Demo/Documentation/Evidence/Phase4");
                File.WriteAllText("Assets/Demo/Documentation/Evidence/Phase4/参考绑定与时序.md", report.ToString());
                var texture = (Texture2D)heroData.FindProperty("backgroundTexture").objectReferenceValue;
                if (texture != null)
                {
                    var background = new GameObject("BattleBackground", typeof(SpriteRenderer)); background.transform.SetParent(root.transform); background.layer = 8;
                    // 保存为独立Sprite资产，场景不依赖运行时临时Sprite。
                    Directory.CreateDirectory("Assets/Demo/Scenes");
                    string spritePath = "Assets/Demo/Scenes/ReferenceBackground.asset";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (sprite == null) { sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100); AssetDatabase.CreateAsset(sprite, spritePath); }
                    var renderer = background.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = -100;
                    float height = camera.orthographicSize * 2; float scale = Mathf.Max(height * (16f / 9) / sprite.bounds.size.x, height / sprite.bounds.size.y);
                    background.transform.localScale = Vector3.one * scale; background.transform.position = camera.ViewportToWorldPoint(new Vector3(.5f, .5f, 20));
                }
                Directory.CreateDirectory("Assets/Demo/Scenes"); EditorSceneManager.SaveScene(generated, Path);
                var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != Path).ToList(); scenes.Add(new EditorBuildSettingsScene(Path, true)); EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("Demo独立场景已生成；参考场景保持不变：" + Path);
            }
            finally
            {
                if (generated.IsValid()) EditorSceneManager.CloseScene(generated, true);
                EditorSceneManager.CloseScene(reference, true); if (original.IsValid()) SceneManager.SetActiveScene(original);
            }
        }
        private static BattleSceneBinding.Actor CopyActor(string key, Animator source, SerializedProperty clips, Vector3 approach, Transform parent)
        {
            var model = UnityEngine.Object.Instantiate(source.gameObject, parent); model.name = "Actor_" + key;
            model.transform.position = source.transform.position; model.transform.localScale = source.transform.lossyScale;
            foreach (var transform in model.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 8;
            var animations = Enumerable.Range(0, clips.arraySize).Select(i => (AnimationClip)clips.GetArrayElementAtIndex(i).objectReferenceValue)
                .Concat(AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/GameResources/Characters/" + (key == "hero" ? "Hero" : key == "warrior" ? "GoblinWarrior" : key == "thief" ? "GoblinThief" : "GoblinChief") + "/Animations" })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid))))
                .Where(clip => clip != null).Distinct().ToArray();
            return new BattleSceneBinding.Actor { key = key, animator = model.GetComponent<Animator>(), clips = animations, home = model.transform.position, scale = model.transform.localScale, approach = approach };
        }
    }
}
