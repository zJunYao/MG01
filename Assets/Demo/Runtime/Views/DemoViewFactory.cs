using UnityEngine;
using UnityEngine.UI;

namespace MG01.Demo.Views
{
    /// <summary>只补充Demo缺失的动态节点，保留通用预制体素材和布局。</summary>
    public static class DemoViewFactory
    {
        public static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            Place(rect, position, size); return rect;
        }
        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one; }
        public static Text Text(string name, Transform parent, Vector2 position, Vector2 size, string content, Font font, int fontSize = 24)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = fontSize; text.color = Color.white; text.raycastTarget = false;
            text.text = content; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        public static Button Button(string name, Transform parent, Vector2 position, Vector2 size, string content, Font font)
        {
            var rect = Rect(name, parent, position, size); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.12f, .24f, .3f, .96f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = Text("Label", rect, Vector2.zero, size, content, font); label.alignment = TextAnchor.MiddleCenter; return button;
        }
        public static T Require<T>(Transform root, string path) where T : Component
        {
            var child = root.Find(path); var component = child == null ? null : child.GetComponent<T>();
            if (component == null) throw new System.InvalidOperationException("Demo页面缺少绑定：" + path + " / " + typeof(T).Name);
            return component;
        }
    }
}
