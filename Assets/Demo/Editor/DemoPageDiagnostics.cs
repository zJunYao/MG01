using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MG01.Demo.Editor
{
    public static class DemoPageDiagnostics
    {
        [MenuItem("Demo/页面/导出原预制体布局")]
        public static void Export()
        {
            var text = new StringBuilder();
            foreach (string name in new[] { "UIStage", "UIMainRole", "ActorHeadHud", "HeroAttackQueue" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameResources/UI/UIPrefab/" + name + ".prefab");
                foreach (var rect in prefab.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect.parent != prefab.transform && rect != prefab.transform && rect.GetComponent<GridLayoutGroup>() == null) continue;
                    text.Append(name).Append('/').Append(rect.name).Append(" 位置=").Append(rect.anchoredPosition).Append(" 尺寸=").Append(rect.sizeDelta)
                        .Append(" 锚=").Append(rect.anchorMin).Append('/').Append(rect.anchorMax).Append(" 枢轴=").Append(rect.pivot).AppendLine();
                    var grid = rect.GetComponent<GridLayoutGroup>();
                    if (grid != null) text.Append("单元=").Append(grid.cellSize).Append(" 间距=").Append(grid.spacing).Append(" 约束=").Append(grid.constraintCount).AppendLine();
                }
            }
            Directory.CreateDirectory("Assets/Demo/Documentation/Evidence");
            File.WriteAllText("Assets/Demo/Documentation/Evidence/页面原布局.txt", text.ToString()); Debug.Log("Demo页面布局已导出");
        }
    }
}
