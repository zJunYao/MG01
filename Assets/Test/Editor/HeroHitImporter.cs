using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MG01.Test.Editor
{
    /// <summary>追加主角右下受击动作，不覆盖原动画、默认状态或场景站位。</summary>
    public static class HeroHitImporter
    {
        private const string Root = "Assets/GameResources/Characters/Hero/";
        private const string Action = "Hit_south-east";
        private const string ScenePath = "Assets/Test/Scenes/HeroPreview.unity";

        public static void ImportBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("此入口仅用于批处理导入。");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Import();
        }

        [MenuItem("测试/主角/导入右下受击动画")]
        public static void Import()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("请在非播放模式保存并打开 HeroPreview 场景。");
            HeroPreview preview = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HeroPreview>(true)).Single();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Animations/Hero.controller");
            if (!controller || !File.Exists(Root + "Sprites/" + Action + ".png") ||
                preview.clips.Length != preview.labels.Length || !preview.hero ||
                preview.hero.runtimeAnimatorController != controller)
                throw new InvalidOperationException("受击图集、主角控制器或测试场景配置不完整。");
            if (File.Exists(Root + "Animations/" + Action + ".anim") ||
                controller.layers[0].stateMachine.states.Any(state => state.state.name == Action))
                throw new InvalidOperationException("受击动画已存在，拒绝覆盖手工调整，请使用验证菜单。");
            Sprite[] sprites = CharacterActionImporter.ImportSprites("Hero", Action, 9);
            AnimationClip clip = CharacterActionImporter.CreateClip("Hero", Action, sprites, 5f, false);
            controller.AddMotion(clip);
            EditorUtility.SetDirty(controller);
            preview.clips = preview.clips.Concat(new[] { clip }).ToArray();
            preview.labels = preview.labels.Concat(new[] { "受击 · 右下" }).ToArray();
            EditorUtility.SetDirty(preview);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Validate();
            Debug.Log("主角受击动画已导入：9 帧、5 FPS、1.8 秒、不循环，HeroPreview 已增加受击选项，原动作及站位保持不变。");
        }

        [MenuItem("测试/主角/验证右下受击动画")]
        public static void Validate()
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Root + "Sprites/" + Action + ".png")
                .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            var importer = AssetImporter.GetAtPath(Root + "Sprites/" + Action + ".png") as TextureImporter;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/" + Action + ".anim");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Animations/Hero.controller");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Hero.prefab");
            if (!clip || !controller || !prefab || !importer || sprites.Length != 9 ||
                !importer.alphaIsTransparency || importer.spritePixelsPerUnit != 100f ||
                importer.filterMode != FilterMode.Point || clip.isLooping || clip.frameRate != 5f ||
                Mathf.Abs(clip.length - 1.8f) > 0.001f || !controller.animationClips.Contains(clip) ||
                controller.layers[0].stateMachine.defaultState.name != "Breathe_south-east" ||
                prefab.GetComponent<Animator>().runtimeAnimatorController != controller ||
                sprites.Any(sprite => sprite.rect.size != new Vector2(256f, 256f) ||
                    sprite.pivot != new Vector2(128f, 128f)))
                throw new InvalidOperationException("主角受击资源、控制器或预制体引用不正确。");
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys.Length != 9 || !keys.Select(key => key.value).SequenceEqual(sprites.Cast<UnityEngine.Object>()))
                throw new InvalidOperationException("受击帧序列异常。");
            var sample = new GameObject("主角受击采样检查");
            try
            {
                var renderer = sample.AddComponent<SpriteRenderer>();
                sample.AddComponent<Animator>().runtimeAnimatorController = controller;
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (Mathf.Abs(keys[i].time - i * 0.2f) > 0.001f)
                        throw new InvalidOperationException("受击帧时序异常。");
                    clip.SampleAnimation(sample, (i + 0.1f) / 5f);
                    if (renderer.sprite != sprites[i]) throw new InvalidOperationException("受击逐帧采样失败。");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(sample); }
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path == ScenePath)
            {
                var preview = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<HeroPreview>(true)).Single();
                int index = Array.IndexOf(preview.clips, clip);
                if (index < 0 || preview.labels.Length != preview.clips.Length || preview.labels[index] != "受击 · 右下")
                    throw new InvalidOperationException("HeroPreview 受击测试入口缺失。");
            }
            Debug.Log("主角受击验证通过：9 帧时序及采样正确、不循环、1.8 秒，控制器默认状态和预制体引用正常。");
        }
    }
}
