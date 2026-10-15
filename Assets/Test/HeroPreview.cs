using UnityEngine;

namespace MG01.Test
{
    /// <summary>独立观察工具，不依赖游戏框架。</summary>
    public sealed class HeroPreview : MonoBehaviour
    {
        public Animator hero;
        public Camera previewCamera;
        public Texture2D backgroundTexture;
        public AnimationClip[] clips;
        public string[] labels;
        private int selected;
        private bool paused;
        private bool repeat = true;
        private float speed = 1f;
        private float time;
        private Font chineseFont;
        private Vector2 scroll;
        private SpriteRenderer backgroundRenderer;
        private Sprite backgroundSprite;
        private SpriteRenderer heroRenderer;

        private void Start()
        {
            chineseFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16);
            if (hero) heroRenderer = hero.GetComponent<SpriteRenderer>();
            if (backgroundTexture)
            {
                backgroundSprite = Sprite.Create(backgroundTexture,
                    new Rect(0, 0, backgroundTexture.width, backgroundTexture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                backgroundRenderer = new GameObject("测试背景_scene_2").AddComponent<SpriteRenderer>();
                backgroundRenderer.sprite = backgroundSprite;
                if (heroRenderer) backgroundRenderer.sortingLayerID = heroRenderer.sortingLayerID;
                backgroundRenderer.sortingOrder = heroRenderer ? heroRenderer.sortingOrder - 1 : -100;
            }
            Select(0);
        }

        private void LateUpdate()
        {
            if (!previewCamera || !previewCamera.orthographic) return;
            if (backgroundRenderer)
            {
                float height = previewCamera.orthographicSize * 2f;
                float scale = Mathf.Max(height * previewCamera.aspect / backgroundSprite.bounds.size.x,
                    height / backgroundSprite.bounds.size.y);
                backgroundRenderer.transform.localScale = Vector3.one * scale;
                backgroundRenderer.transform.position = previewCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 20f));
            }
            if (heroRenderer)
            {
                // 按参考图红框定位到左下方道路，并计入精灵底部的透明留白。
                Vector3 foot = previewCamera.ViewportToWorldPoint(new Vector3(0.25f, 0.14f,
                    hero.transform.position.z - previewCamera.transform.position.z));
                Bounds bounds = heroRenderer.bounds;
                hero.transform.position += new Vector3(foot.x - bounds.center.x, foot.y - bounds.min.y, 0f);
            }
        }

        private void Update()
        {
            if (!hero || clips == null || clips.Length == 0) return;
            if (!paused)
            {
                time += Time.unscaledDeltaTime * speed;
                float length = clips[selected].length;
                if (time >= length)
                {
                    time = repeat ? Mathf.Repeat(time, length) : length;
                    if (!repeat) paused = true;
                }
            }
            Sample();
        }

        private void Select(int index)
        {
            selected = index;
            time = 0f;
            Sample();
        }

        private void Sample()
        {
            if (!hero || clips == null || clips.Length == 0) return;
            hero.speed = 0f;
            hero.Play(clips[selected].name, 0, Mathf.Clamp01(time / clips[selected].length));
            hero.Update(0f);
        }

        private void OnGUI()
        {
            if (!hero || clips == null || clips.Length == 0) return;
            Font previousFont = GUI.skin.font;
            if (chineseFont) GUI.skin.font = chineseFont;
            float panelWidth = Mathf.Min(290f, Screen.width * 0.36f);
            GUILayout.BeginArea(new Rect(Screen.width - panelWidth - 16, 16, panelWidth,
                Mathf.Min(Screen.height - 32, 760)), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("主角 · 模型 / 动画 / 特效观察");
            GUILayout.Label("右下方向 · 待机 / 呼吸 / 魔法攻击");
            for (int i = 0; i < clips.Length; i++)
                if (GUILayout.Toggle(selected == i, labels[i], GUI.skin.button) && selected != i) Select(i);
            paused = GUILayout.Toggle(paused, "暂停播放");
            repeat = GUILayout.Toggle(repeat, "循环预览（包含攻击）");
            GUILayout.Label("播放速度：" + speed.ToString("0.00") + " 倍");
            speed = GUILayout.HorizontalSlider(speed, 0.1f, 2f);
            GUILayout.Label("帧：" + Mathf.Min(Mathf.FloorToInt(time * clips[selected].frameRate) + 1,
                Mathf.RoundToInt(clips[selected].length * clips[selected].frameRate)));
            float seek = GUILayout.HorizontalSlider(time, 0f, clips[selected].length);
            if (!Mathf.Approximately(seek, time)) { time = seek; paused = true; Sample(); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("上一帧")) Step(-1);
            if (GUILayout.Button("下一帧")) Step(1);
            if (GUILayout.Button("重播")) { time = 0f; paused = false; Sample(); }
            GUILayout.EndHorizontal();
            if (previewCamera)
            {
                GUILayout.Label("镜头缩放");
                previewCamera.orthographicSize = GUILayout.HorizontalSlider(previewCamera.orthographicSize, 1.4f, 5f);
                if (backgroundRenderer)
                    backgroundRenderer.enabled = GUILayout.Toggle(backgroundRenderer.enabled, "显示场景背景");
                if (GUILayout.Button("切换纯色背景明暗"))
                    previewCamera.backgroundColor = previewCamera.backgroundColor.r < 0.3f
                        ? new Color(0.72f, 0.75f, 0.78f) : new Color(0.08f, 0.11f, 0.16f);
            }
            GUILayout.Label("魔法光效已包含在原始攻击帧中。\n当前仅保留右下方向动画。", GUILayout.Height(48));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.skin.font = previousFont;
        }

        private void Step(int direction)
        {
            paused = true;
            time = Mathf.Clamp(time + direction / clips[selected].frameRate, 0f, clips[selected].length);
            Sample();
        }

        private void OnDestroy()
        {
            if (chineseFont) Destroy(chineseFont);
            if (backgroundRenderer) Destroy(backgroundRenderer.gameObject);
            if (backgroundSprite) Destroy(backgroundSprite);
        }
    }
}
