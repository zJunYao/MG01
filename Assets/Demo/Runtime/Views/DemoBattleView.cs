using System;
using System.Collections.Generic;
using System.Linq;
using MG01.Demo.Application;
using MG01.Demo.Configuration;
using MG01.Demo.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static MG01.Demo.Views.DemoViewFactory;

namespace MG01.Demo.Views
{
    /// <summary>页面组合绑定。模型更新只通过命令；拖动期间不改占格，常驻显示根不随整理页隐藏。</summary>
    public sealed class DemoBattleView : MonoBehaviour
    {
        private IDemoBattleBinding binding;
        private Transform stage, display, roleBar, queue;
        private RectTransform bagGrid, reserveGrid, itemLayer, dragLayer;
        private GridLayoutGroup bagLayout, reserveLayout;
        private readonly Dictionary<string, DemoItemPointer> items = new Dictionary<string, DemoItemPointer>();
        private readonly Dictionary<string, Transform> huds = new Dictionary<string, Transform>();
        private readonly List<Transform> queueItems = new List<Transform>();
        private readonly List<string> visibleQueue = new List<string>();
        private PointerGesture gesture;
        private DemoItemPointer pressed;
        private RectTransform ghost;
        private Vector2 grabOffset;
        private Vector2 grabFraction;
        private Camera eventCamera;
        private Font font;
        private Text detailName, detail, notice, status, resultText;
        private GameObject result;
        private Button confirm, cycle;
        private string selected;
        private BattleSceneBinding sceneBinding;
        private SceneModelView sceneView;
        private bool faulted;
        private bool heroQueueReady;
        public void SetHeroQueueReady(bool ready)
        {
            heroQueueReady = ready;
            if (binding != null) queue.gameObject.SetActive(!faulted && ready && binding.Snapshot.Phase == BattlePhase.HeroExecution);
        }
        public void BindProjection(BattleSceneBinding scene, SceneModelView modelView)
        {
            sceneBinding = scene; sceneView = modelView;
        }
        public bool Dragging => gesture != null && gesture.Dragging;
        public IReadOnlyList<string> VisibleQueue => visibleQueue.AsReadOnly();
        public bool Bound => binding != null;
        public string LastMessage => notice == null ? "" : notice.text;
        public RectTransform BagGrid => bagGrid;
        public RectTransform ReserveGrid => reserveGrid;

        public void Bind(IDemoBattleBinding source, Transform stageRoot, Transform displayRoot, Transform roleRoot, Transform queueRoot, GameObject hudPrefab)
        {
            binding = source; stage = stageRoot; display = displayRoot; roleBar = roleRoot; queue = queueRoot;
            var interaction = source.Catalog.Table(ConfigSchema.Interaction).Single();
            gesture = new PointerGesture(interaction.Decimal("double_click_seconds"), interaction.Decimal("drag_threshold"));
            detailName = Require<Text>(stage, "bg_info/txt_name"); detail = Require<Text>(stage, "bg_info/txt_info"); font = detail.font;
            bagGrid = Require<RectTransform>(stage, "bag/ItemGrid"); reserveGrid = Require<RectTransform>(stage, "case/ItemGrid");
            bagLayout = bagGrid.GetComponent<GridLayoutGroup>(); reserveLayout = reserveGrid.GetComponent<GridLayoutGroup>();
            ConfigureGrid(bagGrid, bagLayout, Battle.Integer("columns"), Battle.Integer("rows"));
            ConfigureGrid(reserveGrid, reserveLayout, Battle.Integer("reserve_columns"), Mathf.CeilToInt((float)Battle.Integer("reserve_capacity") / Battle.Integer("reserve_columns")));
            itemLayer = Rect("DemoItems", stage, Vector2.zero, new Vector2(1920, 1080));
            dragLayer = Rect("DragLayer", transform, Vector2.zero, new Vector2(1920, 1080));
            confirm = Require<Button>(stage, "btn_attack"); cycle = Require<Button>(stage, "btn_change");
            confirm.onClick.AddListener(Confirm); cycle.onClick.AddListener(Cycle);
            notice = Text("Notice", transform, new Vector2(40, -175), new Vector2(540, 125), "单击详情 · 双击旋转\n拖动整理，确认后自动执行", font);
            status = Text("BattleStatus", display, new Vector2(620, -170), new Vector2(1100, 55), "", font, 28);
            Button("ExitButton", display, new Vector2(1690, -25), new Vector2(200, 60), "返回主菜单", font).onClick.AddListener(Exit);
            roleBar.Find("bg_state/btn_bag").gameObject.SetActive(false); roleBar.Find("bg_state/btn_cast").gameObject.SetActive(false);
            foreach (var actor in binding.Snapshot.Actors.OrderBy(actor => actor.ActionOrder))
            {
                if (actor.Key == binding.Snapshot.Hero.Key) continue;
                var anchor = Rect("Actor_" + actor.Key, display, Vector2.zero, Vector2.zero);
                var hud = Instantiate(hudPrefab, anchor, false); hud.name = "ActorHud_" + actor.Key;
                huds.Add(actor.Key, hud.transform);
                foreach (var graphic in hud.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            }
            var template = queue.Find("QueueItems/QueueItem_01");
            foreach (Transform child in queue.Find("QueueItems")) { child.gameObject.SetActive(false); }
            queue.Find("SequenceArrows").gameObject.SetActive(false);
            // 按实际有效项扩展模板，不把原预制体的四格当职业容量。
            queueItems.Add(template);
            Place((RectTransform)queue.Find("QueueItems"), Vector2.zero, new Vector2(800, 100));
            result = Rect("BattleResult", transform, new Vector2(640, -300), new Vector2(800, 470)).gameObject;
            result.AddComponent<Image>().color = new Color(.035f, .07f, .09f, 1);
            resultText = Text("ResultText", result.transform, new Vector2(70, -45), new Vector2(660, 210), "", font, 36);
            Button("RestartButton", result.transform, new Vector2(70, -310), new Vector2(300, 80), "重新开始", font).onClick.AddListener(Restart);
            Button("MenuButton", result.transform, new Vector2(430, -310), new Vector2(300, 80), "返回主菜单", font).onClick.AddListener(Exit);
            result.SetActive(false); Refresh();
        }
        public void Refresh()
        {
            if (binding == null || faulted) return;
            var snapshot = binding.Snapshot; bool preparing = snapshot.Phase == BattlePhase.Preparation;
            if (!preparing) CancelPointer();
            stage.gameObject.SetActive(preparing); confirm.interactable = preparing && !Dragging; cycle.interactable = preparing;
            status.text = "第" + snapshot.Round + "轮　" + PhaseName(snapshot.Phase) + "　当前：" + Name(snapshot.ActingKey);
            foreach (string key in items.Keys.Where(key => !snapshot.Items.Any(item => item.Key == key)).ToArray())
            { Destroy(items[key].gameObject); items.Remove(key); if (selected == key) selected = null; }
            foreach (var item in snapshot.Items)
            {
                if (!items.TryGetValue(item.Key, out var view))
                {
                    var itemRect = Rect("Item_" + item.Key, itemLayer, Vector2.zero, Vector2.one);
                    var image = itemRect.gameObject.AddComponent<Image>(); image.color = new Color(.25f, .43f, .5f, .35f);
                    var icon = Rect("Icon", itemRect, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>(); icon.raycastTarget = false; icon.preserveAspect = true;
                    view = itemRect.gameObject.AddComponent<DemoItemPointer>(); view.Owner = this; view.ItemKey = item.Key; items.Add(item.Key, view);
                }
                var definition = binding.Catalog.Get(ConfigSchema.Items, item.DefinitionKey);
                var grid = item.Container == ItemContainer.Backpack ? bagGrid : reserveGrid;
                var layout = item.Container == ItemContainer.Backpack ? bagLayout : reserveLayout;
                var rectShape = GridRect.For(item, definition);
                int columns = item.Container == ItemContainer.Backpack ? Battle.Integer("columns") : Battle.Integer("reserve_columns");
                int column = item.Container == ItemContainer.Backpack ? item.Column : item.Slot % columns;
                int row = item.Container == ItemContainer.Backpack ? item.Row : item.Slot / columns;
                var position = CellPosition(layout, column, row);
                var size = item.Container == ItemContainer.Backpack ? new Vector2(rectShape.Width * (layout.cellSize.x + layout.spacing.x) - layout.spacing.x,
                    rectShape.Height * (layout.cellSize.y + layout.spacing.y) - layout.spacing.y) : layout.cellSize;
                var rect = (RectTransform)view.transform; rect.SetParent(grid, false); Place(rect, position, size);
                view.GetComponent<Image>().color = selected == item.Key ? new Color(.9f, .7f, .15f, .65f) : new Color(.25f, .43f, .5f, .35f);
                var iconRect = (RectTransform)view.transform.Find("Icon"); Place(iconRect, size * new Vector2(.5f, -.5f), size - Vector2.one * 6);
                iconRect.pivot = new Vector2(.5f, .5f); iconRect.localEulerAngles = new Vector3(0, 0, item.Container == ItemContainer.Backpack ? -90 * item.Rotation : 0);
                if (item.Container == ItemContainer.Backpack && item.Rotation % 2 == 1) iconRect.sizeDelta = new Vector2(size.y - 6, size.x - 6);
                string iconKey = definition.Text("icon_key");
                iconRect.GetComponent<Image>().sprite = binding.Sprite(item.Container == ItemContainer.Backpack ? iconKey.Replace("_icon", "_weapon") : iconKey);
                // 动态道具不得参与GridLayoutGroup自动排格。
                var element = view.GetComponent<LayoutElement>() ?? view.gameObject.AddComponent<LayoutElement>(); element.ignoreLayout = true;
            }
            RefreshDetails(snapshot);
            foreach (var actor in snapshot.Actors)
            {
                if (huds.TryGetValue(actor.Key, out var hud))
                {
                    Require<Text>(hud, "HealthGroup/HealthText").text = actor.Health + "/" + actor.MaxHealth;
                    var slider = Require<Slider>(hud, "HealthGroup/HealthBar"); slider.interactable = false; slider.maxValue = actor.MaxHealth; slider.value = actor.Health;
                    Require<Text>(hud, "StatusList/StatusItem_01/ValueText").text = actor.Shield.ToString();
                }
                var role = roleBar.Find("fight_list/characters_" + actor.ActionOrder);
                role.Find("icon").GetComponent<Image>().sprite = binding.Sprite(binding.Catalog.Get(ConfigSchema.Actors, actor.DefinitionKey).Text("portrait_key"));
                role.Find("icon").GetComponent<Image>().color = actor.Alive ? Color.white : Color.gray;
                role.Find("now_time").gameObject.SetActive(snapshot.ActingKey == actor.Key);
            }
            Require<Text>(roleBar, "bg_state/ctrl_hp/txt_hp").text = snapshot.Hero.Health + "/" + snapshot.Hero.MaxHealth + "　盾" + snapshot.Hero.Shield;
            var hp = Require<Slider>(roleBar, "bg_state/ctrl_hp/sld_hp"); hp.maxValue = snapshot.Hero.MaxHealth; hp.value = snapshot.Hero.Health; hp.interactable = false;
            Require<Text>(roleBar, "bg_state/ctrl_name/txt_name").text = "主角";
            RefreshQueue(snapshot);
            bool ended = snapshot.Phase == BattlePhase.Victory || snapshot.Phase == BattlePhase.Defeat;
            result.SetActive(ended); if (ended) result.transform.SetAsLastSibling();
            if (ended) resultText.text = (snapshot.Phase == BattlePhase.Victory ? "战斗胜利" : "战斗失败") + "\n轮数：" + snapshot.Round + "\n剩余生命：" + snapshot.Hero.Health + "/" + snapshot.Hero.MaxHealth;
        }
        private ConfigRecord Battle => binding.Catalog.Table(ConfigSchema.Battles).Single();
        private string Name(string key)
        { var actor = binding.Snapshot.Actors.FirstOrDefault(value => value.Key == key); return actor == null ? "无" : binding.Catalog.Get(ConfigSchema.Actors, actor.DefinitionKey).Text("name"); }
        private static string PhaseName(BattlePhase phase) => phase == BattlePhase.Preparation ? "备战整理" : phase == BattlePhase.HeroExecution ? "主角执行" : phase == BattlePhase.EnemyExecution ? "敌人执行" : phase == BattlePhase.Victory ? "胜利" : phase == BattlePhase.Defeat ? "失败" : "已退出";
        private void RefreshDetails(BattleSnapshot snapshot)
        {
            var item = snapshot.Items.FirstOrDefault(value => value.Key == selected);
            var detailIcon = Require<Image>(stage, "bg_info/bg/bg_item");
            if (item == null) { detailName.text = "道具详情"; detail.text = "单击查看\n拖动移动；背包内双击旋转90°"; detailIcon.enabled = false; return; }
            var definition = binding.Catalog.Get(ConfigSchema.Items, item.DefinitionKey);
            var effect = binding.Catalog.Get(ConfigSchema.Effects, definition.Text("effect_key"));
            detailIcon.enabled = true;
            detailIcon.sprite = binding.Sprite(definition.Text("icon_key").Replace("_icon", "_weapon"));
            detailIcon.preserveAspect = true;
            detailIcon.SetNativeSize();
            detail.fontSize = 24; detail.resizeTextForBestFit = true; detail.resizeTextMinSize = 16; detail.resizeTextMaxSize = 24;
            int bonus = AdjacencyService.DamageBonus(binding.Catalog, snapshot.Items, item);
            var rect = GridRect.For(item, definition); detailName.text = definition.Text("name");
            detail.text = definition.Text("detail_template").Replace("{value}", effect.Integer("value").ToString()) + "\n尺寸 " + rect.Width + "×" + rect.Height +
                (bonus > 0 ? "\n相邻增伤 +" + bonus + "，实际伤害 " + (effect.Integer("value") + bonus) : "");
        }
        private void RefreshQueue(BattleSnapshot snapshot)
        {
            visibleQueue.Clear(); queue.gameObject.SetActive(heroQueueReady && snapshot.Phase == BattlePhase.HeroExecution);
            IReadOnlyList<ActionPlan> plans = snapshot.Phase == BattlePhase.HeroExecution ? snapshot.RemainingPlan
                .Where(plan => snapshot.Items.Any(item => item.Key == plan.ItemKey) && (plan.Kind != "heal" || snapshot.Hero.Health < snapshot.Hero.MaxHealth)).ToArray() : Array.Empty<ActionPlan>();
            // 横线与图标共用实际队列宽度，避免继续围绕固定800宽容器居中。
            float queueWidth = plans.Count > 0 ? (plans.Count - 1) * 100 + 84 : 0;
            ((RectTransform)queue).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, queueWidth);
            ((RectTransform)queue.Find("QueueItems")).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, queueWidth);
            var line = (RectTransform)queue.Find("QueueLine");
            line.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, queueWidth);
            line.anchoredPosition = new Vector2(0, line.anchoredPosition.y);
            while (queueItems.Count < plans.Count) queueItems.Add(Instantiate(queueItems[0], queueItems[0].parent, false));
            for (int i = 0; i < queueItems.Count; ++i)
            {
                var view = queueItems[i]; view.gameObject.SetActive(i < plans.Count); if (i >= plans.Count) continue;
                Place((RectTransform)view, new Vector2(i * 100, 0), new Vector2(84, 84));
                var item = snapshot.Items.Single(value => value.Key == plans[i].ItemKey); visibleQueue.Add(item.Key);
                Require<Image>(view, "ItemIcon").sprite = binding.Sprite(binding.Catalog.Get(ConfigSchema.Items, item.DefinitionKey).Text("icon_key"));
                view.Find("CurrentHighlight").gameObject.SetActive(snapshot.CurrentStep?.ItemKey == item.Key);
            }
        }
        private static Vector2 CellPosition(GridLayoutGroup layout, int column, int row) => new Vector2(layout.padding.left + column * (layout.cellSize.x + layout.spacing.x), -layout.padding.top - row * (layout.cellSize.y + layout.spacing.y));
        private static void ConfigureGrid(RectTransform grid, GridLayoutGroup layout, int columns, int rows)
        {
            // 仅转换交互坐标原点，补偿枢轴变化，保留预制体的锚点、尺寸和实际格子位置。
            var pivot = new Vector2(0, 1);
            var offset = Vector2.Scale(pivot - grid.pivot, grid.rect.size);
            grid.pivot = pivot; grid.anchoredPosition += offset;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = columns;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft; layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperLeft;
        }
        private Vector2 Reference(Vector2 screen, Camera camera)
        { RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, camera, out var point); return point; }
        public void PointerDown(DemoItemPointer item, PointerEventData data)
        {
            if (binding == null || binding.Snapshot.Phase != BattlePhase.Preparation) return;
            eventCamera = data.pressEventCamera;
            var point = Reference(data.position, eventCamera);
            if (!gesture.Down(item.ItemKey, data.pointerId, point.x, point.y)) return;
            pressed = item; selected = item.ItemKey;
            var rect = (RectTransform)item.transform; RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, eventCamera, out var local);
            grabOffset = new Vector2(local.x, -local.y); grabFraction = new Vector2(grabOffset.x / rect.rect.width, grabOffset.y / rect.rect.height);
            Refresh();
        }
        public void PointerMove(DemoItemPointer item, PointerEventData data)
        {
            if (binding == null || pressed != item) return;
            var point = Reference(data.position, eventCamera);
            if (!gesture.Move(data.pointerId, point.x, point.y)) return;
            if (ghost == null)
            {
                ghost = Instantiate((RectTransform)item.transform, dragLayer, false); ghost.name = "DragPreview";
                foreach (var graphic in ghost.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
                var pointer = ghost.GetComponent<DemoItemPointer>(); pointer.Owner = null; pointer.enabled = false;
                ghost.gameObject.AddComponent<CanvasGroup>().alpha = .7f;
            }
            Vector2 origin = point - ((RectTransform)transform).rect.position;
            ghost.anchoredPosition = new Vector2(origin.x - grabOffset.x, origin.y + grabOffset.y - ((RectTransform)transform).rect.height);
            confirm.interactable = false;
        }
        public void PointerUp(DemoItemPointer item, PointerEventData data)
        {
            if (binding == null || pressed != item || gesture.PointerId != data.pointerId) return;
            PointerMove(item, data);
            bool twice = gesture.Up(data.pointerId, Time.unscaledTimeAsDouble, out bool dragged);
            string reason = null;
            if (dragged)
            {
                if (Inside(reserveGrid, data.position, out var local))
                {
                    int col = Mathf.FloorToInt((local.x - reserveLayout.padding.left) / (reserveLayout.cellSize.x + reserveLayout.spacing.x));
                    int row = Mathf.FloorToInt((-local.y - reserveLayout.padding.top) / (reserveLayout.cellSize.y + reserveLayout.spacing.y));
                    int columns = Battle.Integer("reserve_columns"); int slot = row * columns + col;
                    if (col < 0 || col >= columns || row < 0 || slot >= Battle.Integer("reserve_capacity")) reason = "备用区落点无效";
                    else binding.Move(item.ItemKey, ItemContainer.Reserve, -1, -1, slot, out reason);
                }
                else if (Inside(bagGrid, data.position, out local))
                {
                    var instance = binding.Snapshot.Items.Single(value => value.Key == item.ItemKey);
                    var shape = GridRect.For(instance, binding.Catalog.Get(ConfigSchema.Items, instance.DefinitionKey));
                    float width = shape.Width * (bagLayout.cellSize.x + bagLayout.spacing.x) - bagLayout.spacing.x;
                    float height = shape.Height * (bagLayout.cellSize.y + bagLayout.spacing.y) - bagLayout.spacing.y;
                    int col = Mathf.RoundToInt((local.x - grabFraction.x * width - bagLayout.padding.left) / (bagLayout.cellSize.x + bagLayout.spacing.x));
                    int row = Mathf.RoundToInt((-local.y - grabFraction.y * height - bagLayout.padding.top) / (bagLayout.cellSize.y + bagLayout.spacing.y));
                    binding.Move(item.ItemKey, ItemContainer.Backpack, col, row, -1, out reason);
                }
                else reason = "落点不在容器内，已回退";
            }
            else if (twice) binding.Rotate(item.ItemKey, out reason);
            ClearDragOnly(); Refresh(); if (reason != null) Message(reason);
        }
        private bool Inside(RectTransform rect, Vector2 screen, out Vector2 local)
        { RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, eventCamera, out local); return rect.rect.Contains(local); }
        public void CancelPointer()
        {
            gesture?.Cancel(); pressed = null; if (ghost != null) Destroy(ghost.gameObject); ghost = null;
            if (confirm != null && binding != null) confirm.interactable = binding.Snapshot.Phase == BattlePhase.Preparation;
        }
        private void ClearDragOnly()
        { pressed = null; if (ghost != null) Destroy(ghost.gameObject); ghost = null; }
        public void Message(string message) { if (notice != null) notice.text = message; }
        public void ShowFailure(string reason)
        {
            CancelPointer(); faulted = true; stage.gameObject.SetActive(false); queue.gameObject.SetActive(false);
            result.SetActive(true); result.transform.SetAsLastSibling(); resultText.text = "演出已停止\n" + reason + "\n可重新开始或返回主菜单";
        }
        public void ClearFailure()
        {
            faulted = false;
        }
        private void Confirm() { if (binding == null) return; if (binding.Confirm(Dragging, out string reason)) { CancelPointer(); Message("已确认，本轮道具与目标锁定"); Refresh(); } else Message(reason); }
        private void Cycle() { if (binding == null) return; if (!binding.CycleTarget(out string reason)) Message(reason); Refresh(); }
        private void Select(string key) { if (binding == null) return; if (!binding.SelectTarget(key, out string reason)) Message(reason); Refresh(); }
        private void Restart() { CancelPointer(); selected = null; binding?.Restart(); Refresh(); }
        private void Exit() { CancelPointer(); binding?.Exit(); }
        public void Unbind() { CancelPointer(); binding = null; }
        private void OnDisable() { CancelPointer(); }
        private void OnApplicationFocus(bool focus) { if (!focus) { CancelPointer(); if (confirm != null && binding != null) confirm.interactable = binding.Snapshot.Phase == BattlePhase.Preparation; } }
        private void OnApplicationPause(bool paused) { if (paused) CancelPointer(); }
        private void LateUpdate()
        {
            if (binding != null && sceneBinding != null)
            {
                bool preparing = binding.Snapshot.Phase == BattlePhase.Preparation;
                var camera = preparing ? sceneView.CaptureCamera : sceneBinding.battleCamera;
                if (camera != null)
                foreach (var actor in sceneBinding.actors)
                {
                    bool heroQueue = actor.key == binding.Snapshot.Hero.Key && queue.gameObject.activeSelf;
                    if (!huds.TryGetValue(actor.key, out var hud) && !heroQueue) continue;
                    var renderer = actor.animator.GetComponent<SpriteRenderer>();
                    var point = camera.WorldToViewportPoint(new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, renderer.bounds.center.z));
                    Vector2 screen;
                    if (preparing)
                    {
                        var output = sceneView.Output.rectTransform;
                        var local = new Vector3(output.rect.xMin + point.x * output.rect.width, output.rect.yMin + point.y * output.rect.height);
                        screen = RectTransformUtility.WorldToScreenPoint(null, output.TransformPoint(local));
                    }
                    else screen = new Vector2(point.x * Screen.width, point.y * Screen.height);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)display, screen, null, out var displayPoint);
                    if (heroQueue)
                    {
                        // 队列按当前可见道具宽度居中，底部距离主角头顶24个UI单位。
                        float width = visibleQueue.Count > 0 ? (visibleQueue.Count - 1) * 100 + 84 : 0;
                        var position = displayPoint - ((RectTransform)display).rect.position;
                        ((RectTransform)queue).anchoredPosition = new Vector2(position.x - width * .5f,
                            position.y - ((RectTransform)display).rect.height + 24 + 84);
                        continue;
                    }
                    var root = (RectTransform)hud.parent;
                    // 仅移动跟随锚点，保留HUD预制体自身的尺寸、缩放与布局。
                    root.anchoredPosition = displayPoint - ((RectTransform)display).rect.position + new Vector2(0, -((RectTransform)display).rect.height - 15f);
                }
            }
            // 窗口外释放、事件目标失效等情况没有PointerUp；不允许悬挂拖动锁死确认。
            if (pressed != null && gesture.PointerId < 0 && !Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0)) CancelPointer();
        }
        private void OnDestroy() { Unbind(); }
    }
}
