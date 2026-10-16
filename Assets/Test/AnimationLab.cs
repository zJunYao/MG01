using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MG01.Test
{
    /// <summary>使用独立地面锚点与显示位移调试动画；不接入游戏框架。</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class AnimationLab : MonoBehaviour
    {
        [Serializable]
        public sealed class Subject
        {
            public string label;
            public Transform groundAnchor;
            public Animator animator;
            public AnimationClip[] clips;
            public string[] labels;
            public bool chief;
            [NonSerialized] public Vector3 localFoot;
        }

        public Camera previewCamera;
        public Subject[] subjects;
        public AnimationLabProfile savedProfile;
        [SerializeField, Tooltip("播放时的临时参数，需点击保存按钮写入独立配置")]
        private AnimationLabProfile workingProfile;
        private int selectedSubject;
        private int selectedClip;
        private bool paused;
        private bool repeat = true;
        private float speed = 1f;
        private float time;
        private Vector2 scroll;
        private Font chineseFont;
        private Texture2D markerTexture;
        private Sprite markerSprite;
        private SpriteRenderer shadow;
        private string status = "调整仅影响此场景；保存后可在下次运行继续使用。";
        private readonly List<GameObject> isolatedRoots = new List<GameObject>();

        private void Awake() { IsolateOtherScenes(); }

        private void IsolateOtherScenes()
        {
            // 附加打开时仅在播放期间隔离其他场景，退出播放自动还原编辑状态。
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene == gameObject.scene || !scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.activeSelf) { isolatedRoots.Add(root); root.SetActive(false); }
            }
        }

        private Subject Current => subjects[selectedSubject];
        private AnimationClip Clip => Current.clips[selectedClip];
        private bool IsJumpAttack => Current.chief && Clip.name.StartsWith("Attack", StringComparison.Ordinal);
        private float Progress => Clip.length > 0f ? time / Clip.length : 0f;

        private void Start()
        {
            // Awake 时附加场景可能尚未加载完；在其他观察脚本 Start 前再执行隔离。
            IsolateOtherScenes();
            chineseFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16);
            workingProfile = Instantiate(savedProfile);
            workingProfile.name = "本次调试参数（未保存）";
            foreach (Subject subject in subjects)
            {
                var renderer = subject.animator.GetComponent<SpriteRenderer>();
                subject.localFoot = MonsterPreview.GetLocalFoot(renderer.sprite);
            }
            CreateGroundMarkers();
            selectedSubject = Array.FindIndex(subjects, subject => subject.chief);
            if (selectedSubject < 0) selectedSubject = 0;
            SelectSubject(selectedSubject);
        }

        private void CreateGroundMarkers()
        {
            markerTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    pixels[y * 32 + x] = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) <= 15f
                        ? Color.white : Color.clear;
            markerTexture.SetPixels(pixels);
            markerTexture.Apply();
            markerSprite = Sprite.Create(markerTexture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
            shadow = new GameObject("固定地面影子").AddComponent<SpriteRenderer>();
            shadow.sprite = markerSprite;
            shadow.color = new Color(0f, 0f, 0f, 0.45f);
            shadow.sortingOrder = -1;
        }

        private void Update()
        {
            if (!paused)
            {
                time += Time.unscaledDeltaTime * speed;
                if (time >= Clip.length)
                {
                    time = repeat ? Mathf.Repeat(time, Clip.length) : Clip.length;
                    if (!repeat) paused = true;
                }
            }
            ApplyPose();
        }

        public void SelectSubject(int index)
        {
            selectedSubject = index;
            for (int i = 0; i < subjects.Length; i++) subjects[i].groundAnchor.gameObject.SetActive(i == index);
            selectedClip = Current.chief ? Array.FindIndex(Current.clips, clip => clip.name.StartsWith("Attack")) : 0;
            if (selectedClip < 0) selectedClip = 0;
            time = 0f;
            ApplyPose();
        }

        private void ApplyPose()
        {
            workingProfile.ClampValues();
            Current.animator.speed = 0f;
            // 非循环片段保持末帧；循环片段在进度末端也不回绕到第一帧。
            float sampleTime = Mathf.Min(time, Mathf.Max(0f, Clip.length - 0.001f));
            Current.animator.Play(Clip.name, 0, sampleTime / Clip.length);
            Current.animator.Update(0f);
            float height = IsJumpAttack ? workingProfile.EvaluateHeight(Progress) : 0f;
            Current.animator.transform.localPosition = -Current.localFoot + Vector3.up * height;
            shadow.transform.position = Current.groundAnchor.position + Vector3.down * 0.02f;
            shadow.transform.localScale = new Vector3(0.8f / (1f + height * 0.2f), 0.13f, 1f);
        }

        private void OnGUI()
        {
            if (!workingProfile) return;
            Font previous = GUI.skin.font;
            if (chineseFont) GUI.skin.font = chineseFont;
            GUILayout.BeginArea(new Rect(16f, 16f, Mathf.Min(340f, Screen.width - 32f), Screen.height - 32f), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("动画实验室 · 独立表现调试");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < subjects.Length; i++)
                if (GUILayout.Toggle(selectedSubject == i, subjects[i].label, GUI.skin.button) && selectedSubject != i)
                    SelectSubject(i);
            GUILayout.EndHorizontal();
            for (int i = 0; i < Current.clips.Length; i++)
                if (GUILayout.Toggle(selectedClip == i, Current.labels[i], GUI.skin.button) && selectedClip != i)
                {
                    selectedClip = i;
                    time = 0f;
                    ApplyPose();
                }
            paused = GUILayout.Toggle(paused, "暂停");
            repeat = GUILayout.Toggle(repeat, "循环预览");
            GUILayout.Label("播放速度：" + speed.ToString("0.00") + " 倍");
            speed = GUILayout.HorizontalSlider(speed, 0.1f, 2f);
            GUILayout.Label("时间：" + time.ToString("0.00") + " / " + Clip.length.ToString("0.00") + " 秒");
            float seek = GUILayout.HorizontalSlider(time, 0f, Clip.length);
            if (!Mathf.Approximately(seek, time)) { time = seek; paused = true; ApplyPose(); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("上一帧")) Step(-1);
            if (GUILayout.Button("下一帧")) Step(1);
            if (GUILayout.Button("重播")) { time = 0f; paused = false; ApplyPose(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("酋长攻击 · 附加跳跃");
            workingProfile.enableJump = GUILayout.Toggle(workingProfile.enableJump, "启用附加高度（可关闭对比原动作）");
            DrawSlider("额外高度（世界单位）", ref workingProfile.extraHeight, 0f, 3f);
            DrawSlider("起跳进度", ref workingProfile.takeoff, 0f, 0.98f);
            workingProfile.ClampValues();
            DrawSlider("最高点进度", ref workingProfile.apex, workingProfile.takeoff + 0.01f, 0.99f);
            workingProfile.ClampValues();
            DrawSlider("落地进度", ref workingProfile.landing, workingProfile.apex + 0.01f, 1f);
            workingProfile.ClampValues();
            GUILayout.Label("时机：" + (workingProfile.takeoff * Clip.length).ToString("0.00") + " → " +
                (workingProfile.apex * Clip.length).ToString("0.00") + " → " + (workingProfile.landing * Clip.length).ToString("0.00") + " 秒");
            if (!IsJumpAttack) GUILayout.Label("跳跃参数仅在酋长攻击时生效。", GUILayout.Height(28));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("查看最高点"))
            {
                ShowApex();
            }
            if (GUILayout.Button("还原已保存参数"))
            {
                Destroy(workingProfile);
                workingProfile = Instantiate(savedProfile);
                status = "已还原配置中保存的参数。";
                ApplyPose();
            }
            GUILayout.EndHorizontal();
#if UNITY_EDITOR
            if (GUILayout.Button("保存跳跃参数到独立配置")) SaveProfile();
#endif
            GUILayout.Label(status, GUILayout.Height(48));
            GUILayout.Label("镜头缩放");
            previewCamera.orthographicSize = GUILayout.HorizontalSlider(previewCamera.orthographicSize, 1.8f, 5f);
            GUILayout.Label("跳高为原图之上的额外位移。\n落地前应归零，避免地面特效被抬起。", GUILayout.Height(48));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.skin.font = previous;
        }

        private static void DrawSlider(string label, ref float value, float min, float max)
        {
            GUILayout.Label(label + "：" + value.ToString("0.00"));
            value = GUILayout.HorizontalSlider(value, min, max);
        }

        private void Step(int direction)
        {
            paused = true;
            time = Mathf.Clamp(time + direction / Clip.frameRate, 0f, Clip.length);
            ApplyPose();
        }

        public void ShowApex()
        {
            int chief = Array.FindIndex(subjects, subject => subject.chief);
            if (chief >= 0) { SelectSubject(chief); time = workingProfile.apex * Clip.length; paused = true; ApplyPose(); }
        }

#if UNITY_EDITOR
        public void ValidatePlayback()
        {
            bool previousEnabled = workingProfile.enableJump;
            foreach (Subject subject in subjects)
            {
                SelectSubject(Array.IndexOf(subjects, subject));
                for (int i = 0; i < subject.clips.Length; i++)
                {
                    selectedClip = i;
                    time = Clip.length * 0.5f;
                    ApplyPose();
                    if (!subject.animator.GetComponent<SpriteRenderer>().sprite)
                        throw new InvalidOperationException("角色运行时采样为空：" + subject.label);
                }
            }
            workingProfile.enableJump = false;
            ShowApex();
            float originalY = Current.animator.transform.localPosition.y;
            workingProfile.enableJump = true;
            ApplyPose();
            if (Mathf.Abs(Current.animator.transform.localPosition.y - originalY - workingProfile.extraHeight) > 0.001f)
                throw new InvalidOperationException("最高点附加高度不正确。");
            time = workingProfile.landing * Clip.length;
            ApplyPose();
            if (Mathf.Abs(Current.animator.transform.localPosition.y - originalY) > 0.001f)
                throw new InvalidOperationException("落地后高度未归零。");
            workingProfile.enableJump = previousEnabled;
            ShowApex();
            Debug.Log("动画实验室播放验证通过：四角色动作切换、最高点高度与落地复位正常，当前暂停在酋长最高点。");
        }

        public void SaveProfile()
        {
            workingProfile.ClampValues();
            string originalName = savedProfile.name;
            UnityEditor.Undo.RecordObject(savedProfile, "保存动画实验室参数");
            UnityEditor.EditorUtility.CopySerialized(workingProfile, savedProfile);
            savedProfile.name = originalName;
            UnityEditor.EditorUtility.SetDirty(savedProfile);
            UnityEditor.AssetDatabase.SaveAssets();
            status = "参数已保存，退出播放后保留；原动画未修改。";
        }
#endif

        private void OnDestroy()
        {
            foreach (GameObject root in isolatedRoots)
                if (root) root.SetActive(true);
            if (workingProfile) Destroy(workingProfile);
            if (chineseFont) Destroy(chineseFont);
            if (shadow) Destroy(shadow.gameObject);
            if (markerSprite) Destroy(markerSprite);
            if (markerTexture) Destroy(markerTexture);
        }
    }
}
