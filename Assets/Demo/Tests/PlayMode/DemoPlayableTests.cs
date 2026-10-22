using System.Collections;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using MG01.Demo.Domain;
using MG01.Demo.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UGUIEventSystem = UnityEngine.EventSystems.EventSystem;

namespace MG01.Demo.Tests
{
    /// <summary>实际加载框架启动场景，使用GraphicRaycaster与UGUI事件，不绕过页面直接调用战斗命令。</summary>
    public sealed class DemoPlayableTests
    {
        private DemoCoordinator coordinator;
        private readonly List<string> presentationLog = new List<string>();
        private int sceneHandle;
        private bool sawApproach, sawSecondHit, sawCharge;
        private long lastStep;
        private static Camera CameraFor(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            return canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
        private static Vector2 Screen(RectTransform rect, Vector2 local) => RectTransformUtility.WorldToScreenPoint(CameraFor(rect), rect.TransformPoint(local));
        private static PointerEventData Event(Vector2 point, GameObject pressed)
        { return new PointerEventData(UGUIEventSystem.current) { pointerId = -1, position = point, button = PointerEventData.InputButton.Left, pointerPress = pressed, rawPointerPress = pressed }; }
        private static GameObject Raycast(Vector2 point)
        {
            var results = new System.Collections.Generic.List<RaycastResult>(); UGUIEventSystem.current.RaycastAll(Event(point, null), results);
            return results.FirstOrDefault().gameObject;
        }
        internal static void Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform; var point = Screen(rect, rect.rect.center);
            var target = Raycast(point); Assert.That(target, Is.Not.Null, "按钮没有命中UGUI射线：" + button.name + " 坐标=" + point + " 相机=" + CameraFor(rect) + " 帧=" + Time.frameCount +
                " 图形=" + button.targetGraphic.raycastTarget + " 深度=" + button.targetGraphic.depth + " 裁剪=" + button.targetGraphic.canvasRenderer.cull +
                " 射线器=" + string.Join(";", button.GetComponentsInParent<GraphicRaycaster>().Select(value => value.enabled + ":" + value.eventCamera)));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(target), Is.EqualTo(button.gameObject), "按钮被其他UI遮挡：" + button.name);
            var data = Event(point, target); ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }
        private static void ItemClick(DemoItemPointer item)
        {
            var rect = (RectTransform)item.transform; var point = Screen(rect, rect.rect.center); var target = Raycast(point);
            Assert.That(target, Is.EqualTo(item.gameObject), "道具射线被遮挡"); var data = Event(point, target);
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        }
        internal static void Drag(DemoItemPointer item, RectTransform destination, Vector2 local, bool release = true)
        {
            var rect = (RectTransform)item.transform; var start = Screen(rect, rect.rect.center);
            var target = Raycast(start); Assert.That(target, Is.EqualTo(item.gameObject), "拖动起点被遮挡");
            var data = Event(start, target); ExecuteEvents.Execute(target, data, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler); data.position = Screen(destination, local);
            ExecuteEvents.Execute(target, data, ExecuteEvents.dragHandler);
            if (release) ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        }
        internal static DemoItemPointer Item(string key) => Object.FindObjectsOfType<DemoItemPointer>().Single(value => value.ItemKey == key && value.Owner != null);
        internal static Button Button(string name) => Object.FindObjectsOfType<Button>().Single(value => value.name == name);
        private static IEnumerator Capture(string name)
        {
            Directory.CreateDirectory("Assets/Demo/Documentation/Evidence/Phase4");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Assets/Demo/Documentation/Evidence/Phase4/" + name + ".png"); yield return null;
        }
        private IEnumerator WaitPreparation()
        {
            float end = Time.realtimeSinceStartup + 30;
            while (coordinator.Snapshot.CurrentStep != null && Time.realtimeSinceStartup < end)
            {
                Assert.That(coordinator.SceneBinding.gameObject.scene.handle, Is.EqualTo(sceneHandle), "轮间不得重载场景");
                var step = coordinator.Snapshot.CurrentStep; var actor = coordinator.SceneBinding.Get(step.ActorKey);
                if (lastStep != step.StepId)
                {
                    presentationLog.Add("轮=" + coordinator.Snapshot.Round + " 步=" + step.StepId + " 行动=" + step.ActorKey + "/" + step.Kind + " 第" + step.HitIndex + "击 生命=" + coordinator.Snapshot.Hero.Health);
                    if (step.ActorKey == "warrior" && step.HitIndex == 2)
                    { sawSecondHit = true; Assert.That(Vector3.Distance(actor.animator.transform.position, actor.home), Is.GreaterThan(.1f), "第二击开始前不应返回原位再靠近"); }
                    lastStep = step.StepId;
                }
                if (step.ActorKey != "hero" && step.Kind == "attack" && Vector3.Distance(actor.animator.transform.position, actor.home) > .1f) sawApproach = true;
                if (step.Kind == "charge")
                { sawCharge = true; Assert.That(actor.animator.transform.position, Is.EqualTo(actor.home), "蓄力不能近身"); }
                Assert.That(coordinator.SceneBinding.Get("hero").animator.transform.position, Is.EqualTo(coordinator.SceneBinding.Get("hero").home), "主角施法不得移动");
                yield return null;
            }
            Assert.That(coordinator.Failure, Is.Null);
            Assert.That(coordinator.Snapshot.CurrentStep, Is.Null, "角色表现超时");
            if (coordinator.Snapshot.Phase == BattlePhase.Preparation)
                foreach (var actor in coordinator.SceneBinding.actors) Assert.That(actor.animator.transform.position, Is.EqualTo(actor.home), "轮末存活角色应返回原位");
            // SetActive后等待一帧图形重新注册，模拟用户下一帧点击而非同帧测试重入。
            yield return null;
        }
        [UnityTest] public IEnumerator 真实页面拖放双击锁定胜利重开退出闭环()
        {
            // 启动场景由框架创建资源和页面系统，不使用测试专属战斗宿主。
            SceneManager.LoadScene("Assets/Resources/Scene/start.unity"); yield return null;
            float end = Time.realtimeSinceStartup + 40;
            while ((DemoConfigurationSystem.Instance == null || DemoConfigurationSystem.Instance.Status != DemoConfigurationStatus.Ready ||
                Object.FindObjectsOfType<Canvas>().All(value => value.name != "UIMain")) && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(DemoConfigurationSystem.Instance.Status, Is.EqualTo(DemoConfigurationStatus.Ready));
            yield return null; Canvas.ForceUpdateCanvases();
            Click(Button("btn_new"));
            while ((DemoModule.Active.Snapshot == null || DemoModule.Active.View == null) && Time.realtimeSinceStartup < end) yield return null;
            coordinator = DemoModule.Active; Assert.That(coordinator.Failure, Is.Null); Assert.That(coordinator.View, Is.Not.Null); yield return null;
            var view = coordinator.View; var sessionId = coordinator.Snapshot.SessionId;
            sceneHandle = coordinator.SceneBinding.gameObject.scene.handle;
            Assert.That(SceneManager.GetSceneByPath(DemoBattleSceneScope.Path).isLoaded, Is.True);
            var staff = Item("staff_01"); ItemClick(staff); yield return null; ItemClick(staff);
            Assert.That(coordinator.Snapshot.Items.Single(item => item.Key == "staff_01").Rotation, Is.EqualTo(1));
            // 非法重叠拖放完整回退，拖动不成为双击。
            var old = coordinator.Snapshot.Items.Single(item => item.Key == "staff_01");
            Drag(staff, view.BagGrid, new Vector2(240, -120)); yield return null;
            var actual = coordinator.Snapshot.Items.Single(item => item.Key == "staff_01"); Assert.That(actual.Column, Is.EqualTo(old.Column)); Assert.That(actual.Rotation, Is.EqualTo(1));
            // 旋回竖直；四个90度恢复原朝向。
            for (int i = 0; i < 3; ++i) { ItemClick(staff); ItemClick(staff); yield return null; }
            Assert.That(coordinator.Snapshot.Items.Single(item => item.Key == "staff_01").Rotation, Is.Zero);
            // 把符石从备用区中心抓取后放在零起算(2,1)，药水(0,0)。
            Drag(Item("runestone_01"), view.BagGrid, new Vector2(2 * 67 + 27.5f, -67 - 27.5f)); yield return null;
            Drag(Item("potion_01"), view.BagGrid, new Vector2(27.5f, -(2 * 67 - 12) / 2f)); yield return null;
            Assert.That(coordinator.Snapshot.Items.All(item => item.Container == ItemContainer.Backpack), Is.True);
            Assert.That(coordinator.Snapshot.Items.Single(item => item.Key == "runestone_01").Column, Is.EqualTo(2));
            yield return Capture("全部道具备战");
            Click(Button("Target_chief")); Assert.That(coordinator.Snapshot.TargetKey, Is.EqualTo("chief"));
            Click(Button("btn_change")); Assert.That(coordinator.Snapshot.TargetKey, Is.EqualTo("warrior"));
            // 持续拖动时确认按钮不可交互，取消后恢复。
            Drag(staff, view.BagGrid, new Vector2(100, -160), false); Assert.That(view.Dragging, Is.True);
            Assert.That(Button("btn_attack").interactable, Is.False); view.CancelPointer(); view.Refresh();
            Click(Button("btn_attack")); yield return null;
            Assert.That(coordinator.Snapshot.Phase, Is.EqualTo(BattlePhase.HeroExecution));
            Assert.That(view.VisibleQueue, Is.EqualTo(new[] { "potion_01", "staff_01", "hat_01", "bubble_01" }));
            Assert.That(Object.FindObjectsOfType<Button>().All(value => value.name != "btn_attack"), Is.True, "整理页执行期间应隐藏");
            Assert.That(Object.FindObjectsOfType<Text>().Any(value => value.name == "BattleStatus"), Is.True);
            yield return Capture("主角执行与队列");
            yield return WaitPreparation(); Assert.That(coordinator.Snapshot.Hero.Health, Is.EqualTo(28));
            Assert.That(coordinator.Snapshot.Items.All(item => item.Key != "potion_01"), Is.True);
            Assert.That(coordinator.Snapshot.SessionId, Is.EqualTo(sessionId));
            for (int round = 0; round < 5 && coordinator.Snapshot.Phase == BattlePhase.Preparation; ++round)
            { Click(Button("btn_attack")); yield return WaitPreparation(); }
            Assert.That(coordinator.Snapshot.Phase, Is.EqualTo(BattlePhase.Victory)); Assert.That(coordinator.Snapshot.Round, Is.EqualTo(4));
            Assert.That(coordinator.Snapshot.Hero.Health, Is.EqualTo(25));
            yield return Capture("第四轮胜利");
            Click(Button("RestartButton")); yield return null;
            Assert.That(coordinator.Snapshot.Phase, Is.EqualTo(BattlePhase.Preparation)); Assert.That(coordinator.Snapshot.Items.Count, Is.EqualTo(5));
            Assert.That(coordinator.Snapshot.SessionId, Is.Not.EqualTo(sessionId));
            // 空背包：通过真实拖放将三件初始装备移回备用槽3/4/5。
            Drag(Item("staff_01"), view.ReserveGrid, new Vector2(2 * 63 + 27.5f, -27.5f)); yield return null;
            Drag(Item("hat_01"), view.ReserveGrid, new Vector2(3 * 63 + 27.5f, -27.5f)); yield return null;
            Drag(Item("bubble_01"), view.ReserveGrid, new Vector2(27.5f, -63 - 27.5f)); yield return null;
            Assert.That(coordinator.Snapshot.Items.All(item => item.Container == ItemContainer.Reserve), Is.True);
            Click(Button("btn_attack")); yield return null; Assert.That(coordinator.Snapshot.Phase, Is.EqualTo(BattlePhase.EnemyExecution));
            Assert.That(view.VisibleQueue, Is.Empty); yield return WaitPreparation();
            Assert.That(coordinator.Snapshot.Hero.Health, Is.EqualTo(8)); Assert.That(coordinator.Snapshot.Round, Is.EqualTo(2));
            Assert.That(sawApproach && sawSecondHit && sawCharge, Is.True, "空背包路线应覆盖近身、连续二击与原地蓄力");
            // 失焦取消拖动，不提交落点，也不留下确认锁定。
            var potion = Item("potion_01"); Drag(potion, view.BagGrid, new Vector2(50, -50), false);
            Assert.That(view.Dragging, Is.True); view.SendMessage("OnApplicationFocus", false); yield return null;
            Assert.That(view.Dragging, Is.False); Assert.That(Button("btn_attack").interactable, Is.True);
            Assert.That(coordinator.Snapshot.Items.Single(item => item.Key == "potion_01").Container, Is.EqualTo(ItemContainer.Reserve));
            yield return Capture("空背包第二轮");
            Click(Button("btn_attack")); yield return WaitPreparation();
            Assert.That(coordinator.Snapshot.Phase, Is.EqualTo(BattlePhase.Defeat));
            int stoppedHits = coordinator.Presentation.HitCount; yield return new WaitForSecondsRealtime(.3f);
            Assert.That(coordinator.Presentation.HitCount, Is.EqualTo(stoppedHits), "死亡后不得继续命中");
            Assert.That(coordinator.Snapshot.Hero.Health, Is.Zero); yield return Capture("空背包失败");
            Click(Button("RestartButton")); yield return null;
            Assert.That(coordinator.Snapshot.Hero.Health, Is.EqualTo(18)); Assert.That(coordinator.Snapshot.Items.Count, Is.EqualTo(5));
            Click(Button("ExitButton")); yield return null;
            Assert.That(DemoModule.Active, Is.Null); Assert.That(Object.FindObjectOfType<DemoBattleView>(), Is.Null);
            end = Time.realtimeSinceStartup + 5;
            while (SceneManager.GetSceneByPath(DemoBattleSceneScope.Path).isLoaded && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(SceneManager.GetSceneByPath(DemoBattleSceneScope.Path).isLoaded, Is.False, "退出必须卸载独立场景");
            // 同一编辑器运行内再次进入，检查退出释放后重新加载和订阅不会残留。
            yield return null; Click(Button("btn_new"));
            end = Time.realtimeSinceStartup + 20;
            while ((DemoModule.Active.Snapshot == null || DemoModule.Active.View == null) && Time.realtimeSinceStartup < end) yield return null;
            coordinator = DemoModule.Active; yield return null; Assert.That(coordinator.Snapshot.Round, Is.EqualTo(1));
            Assert.That(coordinator.SceneBinding.gameObject.scene.handle, Is.Not.EqualTo(sceneHandle), "再次进入创建新场景");
            // 实际禁用正在播放的角色，必须停止而不是自动跳过结算；随后从错误面板重开。
            Click(Button("btn_attack")); yield return null;
            var invalidStep = coordinator.Snapshot.CurrentStep; int healthBefore = coordinator.Snapshot.Hero.Health;
            coordinator.SceneBinding.Get(invalidStep.ActorKey).animator.enabled = false;
            LogAssert.Expect(LogType.Error, "Demo异常门禁：动作组件失效：" + invalidStep.ActorKey);
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException: 动作组件失效："));
            yield return null; Assert.That(coordinator.Failure, Is.Not.Null);
            Assert.That(coordinator.Snapshot.Hero.Health, Is.EqualTo(healthBefore), "异常不能补结算");
            coordinator.SceneBinding.Get(invalidStep.ActorKey).animator.enabled = true;
            yield return null; Click(Button("RestartButton")); yield return null;
            Assert.That(coordinator.Failure, Is.Null); Assert.That(coordinator.Snapshot.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(coordinator.Snapshot.SessionId, Is.Not.EqualTo(invalidStep.SessionId));
            Directory.CreateDirectory("Assets/Demo/Documentation/Evidence/Phase4");
            File.WriteAllLines("Assets/Demo/Documentation/Evidence/Phase4/实际演出步骤.txt", presentationLog);
            Click(Button("ExitButton")); yield return null; Assert.That(DemoModule.Active, Is.Null);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { if (DemoModule.Active != null) DemoModule.Active.Exit(); yield return null; }
    }
}
