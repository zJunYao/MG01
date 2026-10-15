using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MG01.Test.Editor
{
    /// <summary>只生成独立主角资源与测试场景，不修改框架、包配置或启动场景。</summary>
    public static class HeroAssetBuilder
    {
        private const string Root = "Assets/GameResources/Characters/Hero";
        private const string ScenePath = "Assets/Test/Scenes/HeroPreview.unity";
        [Serializable] private class Export { public Sheet spritesheet; }
        [Serializable] private class Sheet { public Cell cell_size; public Row[] rows; }
        [Serializable] private class Cell { public int width; public int height; }
        [Serializable] private class Row { public int row; public string type; public int frame_count; public string direction; }

        [MenuItem("测试/主角/导入资源并创建观察场景")]
        public static void Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("请先保存当前场景，避免覆盖未保存的工作。");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Hero.prefab") || File.Exists(ScenePath))
                throw new InvalidOperationException("主角资源或观察场景已存在；为保护手工调整，不自动覆盖。");

            Directory.CreateDirectory(Root + "/Sprites");
            Directory.CreateDirectory(Root + "/Animations");
            Directory.CreateDirectory("Assets/Test/Scenes");
            AssetDatabase.Refresh();
            Export export = JsonUtility.FromJson<Export>(File.ReadAllText(Root + "/Source/Hero.json"));
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(Root + "/Source/Hero.png")))
                throw new InvalidOperationException("无法读取主角图集。");
            var clips = new List<AnimationClip>();
            var labels = new List<string>();
            Sprite defaultSprite = null;
            try
            {
                foreach (Row row in export.spritesheet.rows)
                {
                    if (row.type != "animation" || row.direction != "south-east") continue;
                    Sprite[] sprites = ImportRow(source, export.spritesheet.cell_size, row);
                    if (row.frame_count == 8) defaultSprite = sprites[0];
                    bool attack = row.frame_count == 41;
                    string kind = attack ? "Attack" : row.frame_count == 8 ? "Idle" : "Breathe";
                    clips.Add(CreateClip(kind + "_" + row.direction, sprites, attack ? 41f / 3f : 8f, !attack));
                    labels.Add((attack ? "魔法攻击" : kind == "Idle" ? "待机" : "呼吸待机") + " · 右下");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(Root + "/Animations/Hero.controller");
            foreach (AnimationClip clip in clips) controller.AddMotion(clip);
            controller.layers[0].stateMachine.defaultState = controller.layers[0].stateMachine.states
                .First(s => s.state.name == "Breathe_south-east").state;
            var model = new GameObject("主角");
            model.AddComponent<SpriteRenderer>().sprite = defaultSprite;
            model.AddComponent<Animator>().runtimeAnimatorController = controller;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(model, Root + "/Hero.prefab");
            UnityEngine.Object.DestroyImmediate(model);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("主摄像机").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 2.4f;
            camera.transform.position = new Vector3(-0.85f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.11f, 0.16f);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("主方向光").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var preview = new GameObject("主角观察工具").AddComponent<HeroPreview>();
            preview.hero = instance.GetComponent<Animator>();
            preview.previewCamera = camera;
            // 默认展示右下呼吸，只保留右下方向的动画。
            int initial = clips.FindIndex(c => c.name == "Breathe_south-east");
            var ordered = Enumerable.Range(0, clips.Count).OrderBy(i => i == initial ? 0 : 1).ToArray();
            preview.clips = ordered.Select(i => clips[i]).ToArray();
            preview.labels = ordered.Select(i => labels[i]).ToArray();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = instance;
            if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("主角导入完成：66 个精灵、3 个右下方向动画、独立预制体和观察场景。未修改框架及启动配置。");
        }

        [MenuItem("测试/主角/仅保留右下方向动画")]
        public static void KeepSouthEastOnly()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出播放模式。");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("请先打开并保存主角观察场景。");
            string[] names = { "Breathe_south-east", "Idle_south-east", "Attack_south-east" };
            AnimationClip[] kept = names.Select(name => AssetDatabase.LoadAssetAtPath<AnimationClip>(
                Root + "/Animations/" + name + ".anim")).ToArray();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Animations/Hero.controller");
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(Root + "/Sprites/Row3.png")
                .OfType<Sprite>().OrderBy(s => s.name).First();
            var preview = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<HeroPreview>(true)).Single();
            if (kept.Any(clip => !clip) || !controller || !preview.hero)
                throw new InvalidOperationException("主角右下方向资源或场景引用缺失，未执行清理。");

            var machine = controller.layers[0].stateMachine;
            machine.defaultState = machine.states.First(s => s.state.motion == kept[0]).state;
            foreach (ChildAnimatorState child in machine.states)
                if (!kept.Contains(child.state.motion as AnimationClip)) machine.RemoveState(child.state);
            EditorUtility.SetDirty(controller);

            GameObject model = PrefabUtility.LoadPrefabContents(Root + "/Hero.prefab");
            try
            {
                model.GetComponent<SpriteRenderer>().sprite = sprite;
                PrefabUtility.SaveAsPrefabAsset(model, Root + "/Hero.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(model); }
            preview.clips = kept;
            preview.labels = new[] { "呼吸待机 · 右下", "待机 · 右下", "魔法攻击 · 右下" };
            EditorUtility.SetDirty(preview);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            // 只清理本工具生成的主角动画与其他方向切片，不触碰背景及框架。
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { Root + "/Animations" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!names.Contains(Path.GetFileNameWithoutExtension(path))) AssetDatabase.DeleteAsset(path);
            }
            foreach (int row in new[] { 0, 1, 4 })
                AssetDatabase.DeleteAsset(Root + "/Sprites/Row" + row + ".png");
            AssetDatabase.SaveAssets();
            Debug.Log("清理完成：仅保留右下方向的待机、呼吸和魔法攻击动画，以及 66 个精灵。背景与站位保持不变。");
        }

        private static Sprite[] ImportRow(Texture2D source, Cell cell, Row row)
        {
            int columns = Mathf.Min(8, row.frame_count);
            int rows = Mathf.CeilToInt(row.frame_count / (float)columns);
            var texture = new Texture2D(columns * cell.width, rows * cell.height, TextureFormat.RGBA32, false);
            texture.SetPixels32(new Color32[texture.width * texture.height]);
            var metadata = new List<SpriteRect>();
            for (int i = 0; i < row.frame_count; i++)
            {
                int x = i % columns * cell.width;
                int y = (rows - 1 - i / columns) * cell.height;
                texture.SetPixels(x, y, cell.width, cell.height, source.GetPixels(i * cell.width,
                    source.height - (row.row + 1) * cell.height, cell.width, cell.height));
                metadata.Add(new SpriteRect { name = "Row" + row.row + "_" + i.ToString("D3"),
                    rect = new Rect(x, y, cell.width, cell.height), alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f), spriteID = GUID.Generate() });
            }
            string path = Root + "/Sprites/Row" + row.row + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(metadata.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                metadata.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            Sprite[] result = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (result.Length != row.frame_count) throw new InvalidOperationException("图集切片数量不匹配：" + path);
            return result;
        }

        private static AnimationClip CreateClip(string name, Sprite[] sprites, float fps, bool loop)
        {
            var clip = new AnimationClip { name = name, frameRate = fps };
            var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
            for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
            keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = sprites[sprites.Length - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, Root + "/Animations/" + name + ".anim");
            return clip;
        }
    }
}
