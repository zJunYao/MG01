using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MG01.Test.Editor
{
    public static class StagePreviewBuilder
    {
        public const string ScenePath = "Assets/Test/Scenes/StagePreview.unity";

        [MenuItem("测试/Stage/创建展示场景")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("StagePreview 已存在，请直接打开，避免覆盖手工调整。");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                Camera camera = new GameObject("主摄像机").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 0;
                Light light = new GameObject("主方向光").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                GameObject canvasObject = new GameObject("测试画布", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                GameObject stage = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameResources/UI/UIPrefab/stage.prefab"), canvasObject.transform);
                RectTransform stageRect = stage.GetComponent<RectTransform>();
                stageRect.anchorMin = stageRect.anchorMax = stageRect.pivot = new Vector2(0.5f, 0.5f);
                stageRect.anchoredPosition = Vector2.zero;
                stageRect.localScale = Vector3.one;
                stageRect.sizeDelta = new Vector2(1920f, 1080f);
                StagePreview preview = new GameObject("Stage展示测试").AddComponent<StagePreview>();
                preview.modelView = stage.GetComponentInChildren<SceneModelView>(true);
                preview.actors = new Animator[4];
                GameObject modelRoot = new GameObject("场景模型");
                string[] names = { "Hero", "GoblinWarrior", "GoblinThief", "GoblinChief" };
                string[] labels = { "主角", "哥布林战士", "哥布林盗贼", "哥布林首领" };
                // 按确认后的 Inspector 截图保存模型站位，可见高度保持参考图比例。
                Vector3[] positions =
                {
                    new Vector3(-6.04f, 0.7f, 0f),
                    new Vector3(1.32f, 0.51f, 0f),
                    new Vector3(3.85f, 0.44f, 0f),
                    new Vector3(6.23f, 0.51f, 0f)
                };
                float[] height = { 0.224f, 0.176f, 0.174f, 0.218f };
                for (int i = 0; i < names.Length; i++)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/GameResources/Characters/" + names[i] + "/" + names[i] + ".prefab");
                    GameObject actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, modelRoot.transform);
                    actor.name = labels[i];
                    foreach (Transform child in actor.GetComponentsInChildren<Transform>(true))
                        child.gameObject.layer = LayerMask.NameToLayer("ModelPreview");
                    Animator animator = actor.GetComponent<Animator>();
                    preview.actors[i] = animator;
                    string idle = i == 0 ? "Idle_south-east" : "Idle_south-west";
                    foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                        if (clip.name == idle) clip.SampleAnimation(actor, 0f);
                    SpriteRenderer renderer = actor.GetComponent<SpriteRenderer>();
                    Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                    Vector2 max = new Vector2(float.MinValue, float.MinValue);
                    foreach (Vector2 vertex in renderer.sprite.vertices)
                    {
                        min = Vector2.Min(min, vertex);
                        max = Vector2.Max(max, vertex);
                    }
                    float scale = height[i] * 10.8f / (max.y - min.y);
                    actor.transform.localScale = Vector3.one * scale;
                    actor.transform.localPosition = positions[i];
                    renderer.sortingOrder = i;
                }
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(previous);
            }
        }
    }
}
