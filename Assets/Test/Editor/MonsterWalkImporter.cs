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
    /// <summary>仅追加怪物走路资源与测试场景引用，保留攻击裁剪和手工站位。</summary>
    public static class MonsterWalkImporter
    {
        private const string Root = "Assets/GameResources/Characters/";
        private const string Action = "Walk_south-west";
        private static readonly string[] Monsters = { "GoblinWarrior", "GoblinThief", "GoblinChief" };

        [MenuItem("测试/角色/导入三个怪物走路动画")]
        public static void Import()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Test/Scenes/HeroPreview.unity" || scene.isDirty)
                throw new InvalidOperationException("请先在非播放模式保存并打开 HeroPreview 场景。");
            MonsterPreview panel = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<MonsterPreview>(true)).Single();
            foreach (string monster in Monsters)
            {
                string path = Root + monster + "/";
                if (!File.Exists(path + "Sprites/" + Action + ".png") ||
                    !AssetDatabase.LoadAssetAtPath<AnimatorController>(path + "Animations/" + monster + ".controller") ||
                    File.Exists(path + "Animations/" + Action + ".anim"))
                    throw new InvalidOperationException("素材或控制器缺失，或者走路动画已存在，拒绝覆盖：" + monster);
                if (panel.actors.Count(actor => actor.animator && actor.animator.name == monster) != 1)
                    throw new InvalidOperationException("场景怪物引用不唯一：" + monster);
            }
            foreach (string monster in Monsters)
            {
                Sprite[] sprites = CharacterActionImporter.ImportSprites(monster, Action, 8);
                AnimationClip clip = CharacterActionImporter.CreateClip(monster, Action, sprites, 5f, true);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    Root + monster + "/Animations/" + monster + ".controller");
                controller.AddMotion(clip);
                EditorUtility.SetDirty(controller);
                MonsterPreview.Actor actor = panel.actors.Single(item => item.animator.name == monster);
                actor.clips = actor.clips.Concat(new[] { clip }).ToArray();
            }
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Validate();
            Debug.Log("三个怪物走路动画已追加：各 8 帧、5 FPS、1.6 秒循环，已接入去程与返程，原攻击和站位不变。");
        }

        [MenuItem("测试/角色/验证三个怪物走路动画")]
        public static void Validate()
        {
            foreach (string monster in Monsters)
            {
                string path = Root + monster + "/";
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path + "Sprites/" + Action + ".png")
                    .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
                var importer = AssetImporter.GetAtPath(path + "Sprites/" + Action + ".png") as TextureImporter;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path + "Animations/" + Action + ".anim");
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path + "Animations/" + monster + ".controller");
                if (!clip || !controller || sprites.Length != 8 || !importer || !importer.alphaIsTransparency ||
                    importer.spritePixelsPerUnit != 100f || importer.filterMode != FilterMode.Point ||
                    Mathf.Abs(clip.length - 1.6f) > 0.001f || clip.frameRate != 5f || !clip.isLooping ||
                    !controller.animationClips.Contains(clip) || controller.layers[0].stateMachine.defaultState.name != "Idle_south-west")
                    throw new InvalidOperationException("走路资源或控制器配置异常：" + monster);
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys.Length != 8 || !keys.Select(key => key.value).SequenceEqual(sprites.Cast<UnityEngine.Object>()))
                    throw new InvalidOperationException("走路帧序列异常：" + monster);
                var sample = new GameObject("走路采样验证");
                try
                {
                    var renderer = sample.AddComponent<SpriteRenderer>();
                    var animator = sample.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    for (int i = 0; i < sprites.Length; i++)
                    {
                        clip.SampleAnimation(sample, (i + 0.1f) / 5f);
                        if (renderer.sprite != sprites[i]) throw new InvalidOperationException("走路精灵采样失败。");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(sample); }
            }
            Debug.Log("走路资源验证通过：三个循环动画、24 个透明精灵切片，帧序列和控制器引用正确。");
        }
    }
}
