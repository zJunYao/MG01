using System;
using System.Collections.Generic;
using System.Linq;
using MG01.Demo.Application;
using MG01.Demo.Configuration;
using MG01.Demo.Domain;
using MG01.Demo.Infrastructure;
using MG01.Demo.Views;
using UnityEngine;
using UnityEngine.UI;
using static LT;
using static MG01.Demo.Views.DemoViewFactory;

namespace MG01.Demo
{
    /// <summary>配置门禁、场景、资源与页面的唯一协调器；表现只提交会话凭证，不复制规则。</summary>
    public sealed class DemoCoordinator : MonoBehaviour, IDemoBattleBinding
    {
        private DemoResourceScope resources;
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private readonly List<GameObject> activeEffects = new List<GameObject>();
        private BattleSession session;
        private DemoBattleView view;
        private Action exited;
        private GameObject canvasObject;
        private Text loading;
        private bool closing, loadingStarted, started;
        private float deadline;
        private int requests;
        private string failure;
        private DemoBattleSceneScope sceneScope;
        private BattlePresentationDriver driver;
        private SceneModelView modelView;
        private BattleSceneBinding sceneBinding;
        private DemoBattleJournal journal;
        public string JournalPath => journal?.Path;
        public string JournalError => journal?.Error;
        public DemoConfigCatalog Catalog { get; private set; }
        public BattleSnapshot Snapshot => session?.Snapshot;
        public IReadOnlyList<EnemyIntent> Intents => session.GetEnemyIntents();
        public DemoBattleView View => view;
        public string Failure => failure;
        public BattleSceneBinding SceneBinding => sceneBinding;
        public BattlePresentationDriver Presentation => driver;
        public Sprite Sprite(string key) => sprites.TryGetValue(key, out var sprite) ? sprite : null;
        public void Begin(Action onExit)
        {
            exited = onExit; deadline = Time.realtimeSinceStartup + 30;
            resources = new DemoResourceScope(new FrameworkDemoAssetLoader());
            canvasObject = new GameObject("DemoDisplayRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            // 沿用主菜单现有中文字体，不引入平台相关系统字体。
            var menu = FindObjectsOfType<Text>().FirstOrDefault(text => text.font != null);
            var font = menu != null ? menu.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            loading = Text("LoadingState", canvasObject.transform, new Vector2(500, -400), new Vector2(1100, 220), "正在校验配置和加载Demo资源……", font, 30);
            Button("LoadingExit", canvasObject.transform, new Vector2(1500, -900), new Vector2(300, 80), "返回主菜单", font).onClick.AddListener(Exit);
        }
        private void Update()
        {
            if (closing || failure != null) return;
            try
            {
                if (!loadingStarted)
                {
                    var config = DemoConfigurationSystem.Instance;
                    if (config != null && config.TryGetCatalog(out var catalog, out _))
                    { Catalog = catalog; loadingStarted = true; LoadResources(); }
                    else if (config != null && config.Status == DemoConfigurationStatus.Failed) Fail(config.Error);
                }
                if (!started && Time.realtimeSinceStartup >= deadline) Fail("Demo配置、页面或资源加载超时");
                if (!started || session == null || failure != null) return;
                var step = session.Snapshot.CurrentStep;
                bool preparing = session.Snapshot.Phase == BattlePhase.Preparation;
                modelView.SetVisible(preparing); sceneBinding.SetPreparation(preparing);
                if (preparing) driver.PrepareHeroTurn();
                if (step != null && driver.Current == null) driver.Begin(step);
                driver.Tick(Time.unscaledDeltaTime);
                view.SetHeroQueueReady(driver.HeroQueueReady);
                session.DrainEvents();
            }
            catch (Exception exception) { Fail(exception.Message); Debug.LogException(exception); }
        }
        private void LoadResources()
        {
            var records = Catalog.Table(ConfigSchema.Resources).Where(row => row.Text("kind") == "sprite" ||
                new[] { "role_bar", "actor_hud", "item_queue", "staff_effect", "hat_effect" }.Contains(row.Key)).ToArray();
            requests = records.Length;
            foreach (var record in records)
            {
                resources.Load(record.Text("path"), lease =>
                {
                    if (closing || failure != null) return;
                    try
                    {
                        if (lease == null) throw new InvalidOperationException("资源加载失败：" + record.Text("path"));
                        if (record.Text("kind") == "sprite")
                        {
                            var sprite = DemoResourceScope.FindSprite(lease, record.Text("sub_asset"));
                            if (sprite == null) throw new InvalidOperationException("未找到Sprite子资源：" + record.Key);
                            sprites.Add(record.Key, sprite);
                        }
                        else prefabs.Add(record.Key, lease.Asset as GameObject ?? throw new InvalidOperationException("资源不是预制体：" + record.Key));
                        if (--requests == 0)
                        {
                            sceneScope = new DemoBattleSceneScope();
                            sceneScope.Load(binding => { if (closing || failure != null) return; sceneBinding = binding; StartBattle(); }, Fail);
                        }
                    }
                    catch (Exception exception) { Fail(exception.Message); }
                });
            }
        }
        private string ActorName(string key)
        {
            var actor = session.Snapshot.Actors.Single(value => value.Key == key);
            return Catalog.Get(ConfigSchema.Actors, actor.DefinitionKey).Text("name");
        }
        private void StartBattle()
        {
            if (closing || failure != null) return;
            var page = LOAD<DemoStagePage>(); if (page == null) throw new InvalidOperationException("框架未能创建DemoStagePage");
            SHOW<DemoStagePage>(); HIDE<UIMain>();
            var stage = page.Root;
            // 页面仍由框架卸载；只改变实例组合关系，不改通用预制体。
            stage.SetParent(canvasObject.transform, false);
            StretchToScreen((RectTransform)stage);
            var stageCanvas = stage.GetComponent<Canvas>(); stageCanvas.renderMode = RenderMode.ScreenSpaceOverlay; stageCanvas.worldCamera = null; stageCanvas.overrideSorting = false;
            stage.GetComponent<CanvasScaler>().enabled = false;
            foreach (var graphic in stage.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            foreach (var button in stage.GetComponentsInChildren<Button>(true)) button.targetGraphic.raycastTarget = true;
            var role = resources.Instantiate(prefabs["role_bar"], canvasObject.transform);
            // 状态栏与备战页共用屏幕缩放，按兄弟顺序叠加，且不随备战页隐藏。
            var roleCanvas = role.GetComponent<Canvas>(); roleCanvas.renderMode = RenderMode.ScreenSpaceOverlay; roleCanvas.worldCamera = null;
            role.GetComponent<CanvasScaler>().enabled = false;
            foreach (var graphic in role.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            foreach (var canvas in role.GetComponentsInChildren<Canvas>()) { canvas.overrideSorting = false; }
            StretchToScreen((RectTransform)role.transform);
            role.transform.SetSiblingIndex(stage.GetSiblingIndex() + 1);
            var queue = resources.Instantiate(prefabs["item_queue"], canvasObject.transform);
            Place((RectTransform)queue.transform, new Vector2(680, -600), new Vector2(800, 140));
            foreach (var graphic in queue.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            // 保留完整备战页面，角色场景位于背景之上、背包和操作面板之下。
            stage.Find("bg_scene").gameObject.SetActive(true);
            stage.Find("bg_fight").gameObject.SetActive(true); stage.Find("bg_stage").gameObject.SetActive(true);
            session = new BattleSession(Catalog, Catalog.Table(ConfigSchema.Battles).Single().Key);
            sceneBinding.SetPreparation(true);
            journal = new DemoBattleJournal(Catalog.Version, System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "DemoLogs", session.Snapshot.SessionId + ".jsonl"));
            journal.Record("会话开始", null, session.Snapshot);
            view = canvasObject.AddComponent<DemoBattleView>(); view.Bind(this, stage, canvasObject.transform, role.transform, queue.transform, prefabs["actor_hud"]);
            var modelRoot = stage.Find("ModelViewRoot"); modelRoot.gameObject.SetActive(true);
            StretchToScreen((RectTransform)modelRoot);
            stage.Find("bg_scene").SetAsFirstSibling(); modelRoot.SetSiblingIndex(1);
            modelView = modelRoot.GetComponent<SceneModelView>();
            modelView.ShowScene(new SceneModelViewSettings { modelLayers = 1 << sceneBinding.modelLayer,
                cameraOffset = Vector3.zero, orthographicSize = sceneBinding.battleCamera.orthographicSize,
                nearClipPlane = sceneBinding.battleCamera.nearClipPlane, farClipPlane = sceneBinding.battleCamera.farClipPlane }, sceneBinding.battleCamera.transform);
            view.BindProjection(sceneBinding, modelView);
            driver = new BattlePresentationDriver(sceneBinding, Catalog, CommitPresentationHit, CompletePresentation, PlayItemEffect);
            loading.gameObject.SetActive(false); canvasObject.transform.Find("LoadingExit").gameObject.SetActive(false);
            started = true; session.DrainEvents();
        }
        private static void StretchToScreen(RectTransform rect)
        {
            rect.localRotation = Quaternion.identity; rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var position = rect.anchoredPosition3D; position.z = 0; rect.anchoredPosition3D = position;
        }
        private void CommitPresentationHit(ActionStep step)
        {
            var before = session.Snapshot;
            if (!session.TryCommitHit(step.SessionId, step.StepId, out var reason)) throw new InvalidOperationException(reason);
            journal.Record("命中", before, session.Snapshot, step);
            driver.React(step, session.Snapshot); view.Refresh();
            view.Message(ActorName(step.ActorKey) + " → " + ActorName(step.TargetKey) + "，数值 " + step.Value);
            if (session.Snapshot.CurrentStep == null) driver.Cancel();
        }
        private void CompletePresentation(ActionStep step)
        {
            var before = session.Snapshot;
            if (!session.TryCompletePresentation(step.SessionId, step.StepId, out var reason)) throw new InvalidOperationException(reason);
            journal.Record("表现完成", before, session.Snapshot, step);
            view.Refresh();
        }
        private float PlayItemEffect(ActionStep step)
        {
            string key = Catalog.Get(ConfigSchema.Presentations, step.PresentationKey).Text("effect_key");
            if (key != "staff_effect" && key != "hat_effect") return 0;
            var target = sceneBinding.Get(step.TargetKey).animator.GetComponent<SpriteRenderer>();
            var effect = resources.Instantiate(prefabs[key], sceneBinding.transform);
            // 法杖落雷缩小到原尺寸的一半，适配Demo角色比例。
            if (key == "staff_effect") effect.transform.localScale *= .5f;
            activeEffects.RemoveAll(instance => instance == null); activeEffects.Add(effect);
            // 两种素材均为底部轴点；雷云触地光斑距底边约42像素。
            effect.transform.position = new Vector3(target.bounds.center.x,
                target.bounds.min.y - (key == "hat_effect" ? .42f * effect.transform.lossyScale.y : 0), target.transform.position.z);
            foreach (var child in effect.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = sceneBinding.modelLayer;
            foreach (var renderer in effect.GetComponentsInChildren<SpriteRenderer>(true))
            { renderer.sortingLayerID = target.sortingLayerID; renderer.sortingOrder = target.sortingOrder + 20; }
            var animator = effect.GetComponent<Animator>(); animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.speed = (float)Catalog.Get(ConfigSchema.Presentations, step.PresentationKey).Decimal("speed");
            return animator.runtimeAnimatorController.animationClips.Max(clip => clip.length);
        }
        private void ClearEffects()
        {
            foreach (var effect in activeEffects) if (effect != null) { effect.SetActive(false); Destroy(effect); }
            activeEffects.Clear();
        }
        private void Fail(string reason)
        {
            if (failure != null || closing) return;
            journal?.Record("异常停止", session?.Snapshot, session?.Snapshot, detail: reason);
            failure = reason; session?.Close(); driver?.Cancel(); ClearEffects(); HIDE<DemoStagePage>();
            if (started) view.ShowFailure(reason);
            else
            {
                loading.gameObject.SetActive(true); loading.text = "Demo无法继续：\n" + reason + "\n请返回主菜单后重试。";
                canvasObject.transform.Find("LoadingExit").gameObject.SetActive(true);
                loading.transform.SetAsLastSibling(); canvasObject.transform.Find("LoadingExit").SetAsLastSibling();
            }
            Debug.LogError("Demo异常门禁：" + reason);
        }
        public bool Move(string key, ItemContainer container, int column, int row, int slot, out string reason) => session.TryMove(key, container, column, row, slot, out reason);
        public bool Rotate(string key, out string reason) => session.TryRotate(key, out reason);
        public bool SelectTarget(string key, out string reason) => session.TrySelectTarget(key, out reason);
        public bool CycleTarget(out string reason) => session.TryCycleTarget(out reason);
        public bool Confirm(bool dragging, out string reason)
        {
            var before = session.Snapshot; bool accepted = session.TryConfirm(dragging, out reason);
            journal.Record(accepted ? "确认" : "确认拒绝", before, session.Snapshot, detail: reason); return accepted;
        }
        public void Restart()
        {
            ClearEffects();
            var before = session.Snapshot; session = session.Restart(); journal.Record("重开", before, session.Snapshot); driver.Reset(); failure = null;
            session.DrainEvents(); view.ClearFailure(); view.Refresh(); view.Message("已重新开始");
        }
        public void Exit()
        {
            if (closing) return; closing = true; Cleanup(); var callback = exited; exited = null; callback?.Invoke(); Destroy(gameObject);
        }
        private void Cleanup()
        {
            ClearEffects();
            journal?.Record("退出", session?.Snapshot, session?.Snapshot);
            view?.Unbind(); session?.Close(); HIDE<DemoStagePage>(); UNLOAD<DemoStagePage>();
            driver?.Dispose(); modelView?.Clear(); sceneScope?.Dispose();
            if (canvasObject != null) { canvasObject.SetActive(false); Destroy(canvasObject); canvasObject = null; }
            resources?.Dispose(); resources = null; sprites.Clear(); prefabs.Clear();
        }
        private void OnDestroy() { if (!closing) { closing = true; Cleanup(); } }
    }
}
