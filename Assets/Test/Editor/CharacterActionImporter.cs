using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace MG01.Test.Editor
{
    /// <summary>导入已筛选的奔跑和哥布林图集，并检查动画、控制器与预制体引用。</summary>
    public static class CharacterActionImporter
    {
        private const string Root = "Assets/GameResources/Characters/";
        private static readonly string[] Monsters = { "GoblinWarrior", "GoblinThief", "GoblinChief" };
        private static readonly string[] MonsterNames = { "哥布林战士", "哥布林盗贼", "哥布林酋长" };

        [MenuItem("测试/角色/导入奔跑与三个哥布林")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出播放模式。");
            var hero = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Hero/Animations/Hero.controller");
            if (!hero) throw new InvalidOperationException("找不到现有主角控制器。");
            string runPath = Root + "Hero/Animations/Run_south-east.anim";
            if (File.Exists(runPath) || hero.layers[0].stateMachine.states.Any(s => s.state.name == "Run_south-east")
                || Monsters.Any(m => File.Exists(Root + m + "/" + m + ".prefab")
                    || Directory.Exists(Root + m + "/Animations")))
                throw new InvalidOperationException("目标动画或预制体已存在；为保护手工调整，请使用验证菜单检查现有资源。");

            RequireTexture("Hero", "Run_south-east");
            foreach (string monster in Monsters)
            {
                RequireTexture(monster, "Idle_south-west");
                RequireTexture(monster, "Attack_south-west");
            }
            Sprite[] run = ImportSprites("Hero", "Run_south-east", 8);
            AnimationClip runClip = CreateClip("Hero", "Run_south-east", run, 8f, true);
            // 只追加奔跑状态，保留原控制器的默认状态及现有动作。
            hero.AddMotion(runClip);
            EditorUtility.SetDirty(hero);

            for (int i = 0; i < Monsters.Length; i++)
            {
                string monster = Monsters[i];
                Directory.CreateDirectory(Root + monster + "/Animations");
                AssetDatabase.Refresh();
                Sprite[] idle = ImportSprites(monster, "Idle_south-west", 8);
                Sprite[] attack = ImportSprites(monster, "Attack_south-west", 41);
                AnimationClip idleClip = CreateClip(monster, "Idle_south-west", idle, 8f, true);
                AnimationClip attackClip = CreateClip(monster, "Attack_south-west", attack, 41f / 3f, false);
                var controller = AnimatorController.CreateAnimatorControllerAtPath(
                    Root + monster + "/Animations/" + monster + ".controller");
                controller.AddMotion(idleClip);
                controller.AddMotion(attackClip);
                controller.layers[0].stateMachine.defaultState = controller.layers[0].stateMachine.states
                    .Single(s => s.state.motion == idleClip).state;
                var model = new GameObject(MonsterNames[i]);
                try
                {
                    model.AddComponent<SpriteRenderer>().sprite = idle[0];
                    model.AddComponent<Animator>().runtimeAnimatorController = controller;
                    PrefabUtility.SaveAsPrefabAsset(model, Root + monster + "/" + monster + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(model); }
            }
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("角色导入完成：主角奔跑 8 帧；三个哥布林各含西南攻击 41 帧、西南待机 8 帧。");
        }

        private static void RequireTexture(string character, string action)
        {
            string path = Root + character + "/Sprites/" + action + ".png";
            if (!File.Exists(path)) throw new FileNotFoundException("请先运行图集准备脚本。", path);
        }

        internal static Sprite[] ImportSprites(string character, string action, int count)
        {
            string path = Root + character + "/Sprites/" + action + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // 以下为初始设置；最终 Mipmap 与平台覆盖由项目框架的 AssetImport 统一管理。
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            int columns = Math.Min(8, count);
            int rows = (count + columns - 1) / columns;
            var rectangles = new List<SpriteRect>();
            for (int i = 0; i < count; i++)
                rectangles.Add(new SpriteRect
                {
                    name = action + "_" + i.ToString("D3"),
                    rect = new Rect(i % columns * 256, (rows - 1 - i / columns) * 256, 256, 256),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate()
                });
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(rectangles.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rectangles.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (sprites.Length != count) throw new InvalidOperationException("切片数量不正确：" + path);
            return sprites;
        }

        internal static AnimationClip CreateClip(string character, string action, Sprite[] sprites, float fps, bool loop)
        {
            var clip = new AnimationClip { name = action, frameRate = fps };
            // 每张图片占一帧；不在末尾重复追加最后一帧，避免循环时额外停顿。
            var keys = sprites.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / fps, value = sprite }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.stopTime = sprites.Length / fps;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, Root + character + "/Animations/" + action + ".anim");
            return clip;
        }

        [MenuItem("测试/角色/验证奔跑与三个哥布林")]
        public static void Validate()
        {
            ValidateClip("Hero", "Run_south-east", 8, true);
            var hero = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Hero/Animations/Hero.controller");
            string[] expected = { "Attack_south-east", "Idle_south-east", "Breathe_south-east", "Run_south-east", "Hit_south-east" };
            HeroHitImporter.Validate();
            if (!hero || !hero.layers[0].stateMachine.states.Select(s => s.state.name).OrderBy(n => n)
                    .SequenceEqual(expected.OrderBy(n => n))
                || hero.layers[0].stateMachine.defaultState.name != "Breathe_south-east")
                throw new InvalidOperationException("主角控制器的动作或默认状态不正确。");
            foreach (string monster in Monsters)
            {
                ValidateClip(monster, "Idle_south-west", 8, true);
                ValidateClip(monster, "Attack_south-west", 41, false, monster == "GoblinThief" ? 8 : 0);
                ValidateClip(monster, "Walk_south-west", 8, true);
                string directory = Root + monster;
                if (Directory.GetFiles(directory + "/Sprites", "*.png").Length != 3
                    || Directory.GetFiles(directory + "/Animations", "*.anim").Length != 3)
                    throw new InvalidOperationException("怪物包含多余的图集或动作：" + monster);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(directory + "/" + monster + ".prefab");
                if (!prefab || prefab.GetComponents<Component>().Length != 3
                    || !prefab.GetComponent<SpriteRenderer>() || !prefab.GetComponent<Animator>())
                    throw new InvalidOperationException("怪物预制体组件不正确：" + monster);
                var controller = prefab.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
                if (!controller || controller.layers.Length != 1 || controller.layers[0].stateMachine.states.Length != 3
                    || controller.layers[0].stateMachine.defaultState.name != "Idle_south-west"
                    || controller.animationClips.Length != 3 || controller.animationClips.Any(c => !c))
                    throw new InvalidOperationException("怪物动画控制器不正确：" + monster);
                if (prefab.GetComponent<SpriteRenderer>().sprite.name != "Idle_south-west_000")
                    throw new InvalidOperationException("怪物默认精灵不正确：" + monster);
            }
            Debug.Log("角色资源验证通过：主角奔跑及三个怪物的待机、攻击、走路动画，逐帧采样和资源引用均正常。");
        }

        private static void ValidateClip(string character, string action, int count, bool loop, int skippedFrames = 0)
        {
            string texturePath = Root + character + "/Sprites/" + action + ".png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (!texture || texture.width != 2048 || texture.height != ((count + 7) / 8) * 256
                || sprites.Length != count || importer.isReadable
                || importer.filterMode != FilterMode.Point || importer.spritePixelsPerUnit != 100f
                || sprites.Any(s => s.rect.size != new Vector2(256, 256) || s.pivot != new Vector2(128, 128)))
                throw new InvalidOperationException("图集导入设置或切片不正确：" + texturePath);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + character + "/Animations/" + action + ".anim");
            if (!clip) throw new InvalidOperationException("动画缺失：" + character + "/" + action);
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (bindings.Length != 1 || bindings[0].type != typeof(SpriteRenderer) || bindings[0].propertyName != "m_Sprite")
                throw new InvalidOperationException("动画绑定不正确：" + action);
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            if (keys.Length != count - skippedFrames || AnimationUtility.GetAnimationClipSettings(clip).loopTime != loop
                || Math.Abs(clip.length - keys.Length / clip.frameRate) > 0.001f)
                throw new InvalidOperationException("动画帧数、时长或循环设置不正确：" + action);
            var sample = new GameObject("动画采样验证");
            try
            {
                var renderer = sample.AddComponent<SpriteRenderer>();
                sample.AddComponent<Animator>().runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    Root + character + "/Animations/" + character + ".controller");
                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i].value != sprites[i + skippedFrames]) throw new InvalidOperationException("动画精灵顺序不正确：" + action);
                    clip.SampleAnimation(sample, (i + 0.25f) / clip.frameRate);
                    if (renderer.sprite != sprites[i + skippedFrames]) throw new InvalidOperationException("动画采样不正确：" + action
                        + "，帧 " + i + "，实际精灵：" + (renderer.sprite ? renderer.sprite.name : "空"));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(sample); }
        }
    }
}
