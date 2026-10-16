using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MG01.Test.Editor
{
    public static class AnimationLabSetup
    {
        private const string ScenePath = "Assets/Test/Scenes/AnimationLab.unity";
        private const string ProfilePath = "Assets/Test/Settings/ChiefJump.asset";

        [MenuItem("测试/动画实验室/创建独立动画调试场景")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出播放模式。");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("动画实验室已存在，不自动覆盖手工调整。");
            string[] ids = { "Hero", "GoblinWarrior", "GoblinThief", "GoblinChief" };
            string[] names = { "主角", "战士", "盗贼", "酋长" };
            var prefabs = ids.Select(id => AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GameResources/Characters/" + id + "/" + id + ".prefab")).ToArray();
            var clips = ids.Select(id => AssetDatabase.FindAssets("t:AnimationClip", new[] {
                "Assets/GameResources/Characters/" + id + "/Animations" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid)))
                .OrderBy(clip => clip.name.StartsWith("Attack") ? 1 : 0).ThenBy(clip => clip.name).ToArray()).ToArray();
            if (prefabs.Any(prefab => !prefab) || clips.Any(list => list.Length == 0))
                throw new InvalidOperationException("角色预制体或动画缺失。");
            Directory.CreateDirectory("Assets/Test/Settings");
            AssetDatabase.Refresh();
            var profile = AssetDatabase.LoadAssetAtPath<AnimationLabProfile>(ProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<AnimationLabProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            // 附加创建，保留用户原场景及未保存的编辑内容。
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var camera = new GameObject("主摄像机").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(-1.25f, 1.45f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 2.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.14f, 0.19f);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("主方向光").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var lab = new GameObject("动画实验室").AddComponent<AnimationLab>();
            lab.previewCamera = camera;
            lab.savedProfile = profile;
            lab.subjects = new AnimationLab.Subject[4];
            for (int i = 0; i < ids.Length; i++)
            {
                var anchor = new GameObject(names[i] + "_地面锚点");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], scene);
                model.transform.SetParent(anchor.transform, false);
                Vector3 foot = MonsterPreview.GetLocalFoot(model.GetComponent<SpriteRenderer>().sprite);
                model.transform.localPosition = -foot;
                PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
                lab.subjects[i] = new AnimationLab.Subject { label = names[i], groundAnchor = anchor.transform,
                    animator = model.GetComponent<Animator>(), clips = clips[i], chief = i == 3,
                    labels = clips[i].Select(clip => clip.name.StartsWith("Attack") ? "攻击" :
                        clip.name.StartsWith("Run") ? "跑步" : clip.name.StartsWith("Breathe") ? "呼吸待机" : "待机").ToArray() };
                anchor.SetActive(i == 3);
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = lab.gameObject;
            Debug.Log("独立动画实验室已创建：四角色动作切换、酋长附加跳跃调节及独立参数保存；原场景和原动画未修改。");
        }

        [MenuItem("测试/动画实验室/验证配置和场景")]
        public static void Validate()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AnimationLabProfile>(ProfilePath);
            if (!profile) throw new InvalidOperationException("跳跃配置缺失。");
            profile.ClampValues();
            if (profile.enableJump && (Mathf.Abs(profile.EvaluateHeight(profile.apex) - profile.extraHeight) > 0.001f
                || profile.EvaluateHeight(0f) != 0f || profile.EvaluateHeight(1f) != 0f
                || profile.EvaluateHeight(profile.takeoff) != 0f || profile.EvaluateHeight(profile.landing) != 0f))
                throw new InvalidOperationException("起落高度曲线验证失败。");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("请先打开动画实验室场景。");
            var lab = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<AnimationLab>(true)).Single();
            if (lab.subjects.Length != 4 || !lab.previewCamera || lab.savedProfile != profile)
                throw new InvalidOperationException("实验室场景配置不正确。");
            int frames = 0;
            foreach (AnimationLab.Subject subject in lab.subjects)
            {
                if (!subject.groundAnchor || !subject.animator || subject.clips.Length != subject.labels.Length)
                    throw new InvalidOperationException("角色配置不完整：" + subject.label);
                var sample = new GameObject("实验室采样检查");
                try
                {
                    var renderer = sample.AddComponent<SpriteRenderer>();
                    sample.AddComponent<Animator>().runtimeAnimatorController = subject.animator.runtimeAnimatorController;
                    foreach (AnimationClip clip in subject.clips)
                    {
                        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                        foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                        {
                            if (key.time >= clip.length) continue;
                            clip.SampleAnimation(sample, key.time + 0.05f / clip.frameRate);
                            if (!renderer.sprite || renderer.sprite != key.value)
                                throw new InvalidOperationException("动画采样失败：" + subject.label + "/" + clip.name);
                            frames++;
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(sample); }
            }
            Debug.Log("动画实验室验证通过：4 个角色、" + lab.subjects.Sum(subject => subject.clips.Length) +
                " 个动作、" + frames + " 帧采样正常，跳跃起落及最高点正确。");
            if (EditorApplication.isPlaying) lab.ValidatePlayback();
        }
    }
}
