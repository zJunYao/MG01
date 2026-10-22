using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MG01.Demo.Domain;
using MG01.Demo.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static MG01.Demo.Tests.DemoPlayableTests;

namespace MG01.Demo.Tests
{
    /// <summary>真实入口、UGUI射线与实际Playable；不调用命中接口、不跳过表现。</summary>
    public sealed class DemoRoutePresentationTests
    {
        [UnityTest] public IEnumerator 五路线实际页面与演出逐轮回归()
        {
            // 同一Test Runner批次内复用框架启动器；框架明确禁止在同一运行中二次启动热更。
            if (DemoConfigurationSystem.Instance == null || DemoConfigurationSystem.Instance.Status != DemoConfigurationStatus.Ready)
                SceneManager.LoadScene("Assets/Resources/Scene/start.unity");
            yield return null;
            float end = Time.realtimeSinceStartup + 40;
            while ((DemoConfigurationSystem.Instance == null || DemoConfigurationSystem.Instance.Status != DemoConfigurationStatus.Ready ||
                Object.FindObjectsOfType<Canvas>().All(canvas => canvas.name != "UIMain")) && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(DemoConfigurationSystem.Instance.Status, Is.EqualTo(DemoConfigurationStatus.Ready)); yield return null;
            string[] names = { "初始配置先战士", "全部道具先战士", "全部道具先首领", "不装符石先首领", "不装泡泡先首领" };
            int[][] expected = { new[] {18,11,11,8,8}, new[] {28,25,25,25}, new[] {27,26,26,26}, new[] {27,26,26,26,26}, new[] {18,8,4,4} };
            string output = "Assets/Demo/Documentation/Evidence/Phase5"; Directory.CreateDirectory(output);
            for (int route = 0; route < names.Length; ++route)
            {
                Click(Button("btn_new")); end = Time.realtimeSinceStartup + 30;
                while ((DemoModule.Active.View == null || DemoModule.Active.SceneBinding == null) && DemoModule.Active.Failure == null && Time.realtimeSinceStartup < end) yield return null;
                var coordinator = DemoModule.Active; Assert.That(coordinator.Failure, Is.Null); Assert.That(coordinator.View, Is.Not.Null); yield return null;
                var id = coordinator.Snapshot.SessionId; int handle = coordinator.SceneBinding.gameObject.scene.handle;
                if (route > 0)
                {
                    Drag(Item("runestone_01"), coordinator.View.BagGrid, new Vector2(161.5f, -94.5f)); yield return null;
                    Drag(Item("potion_01"), coordinator.View.BagGrid, new Vector2(27.5f, -61)); yield return null;
                }
                if (route == 3 || route == 4)
                { Drag(Item(route == 3 ? "runestone_01" : "bubble_01"), coordinator.View.ReserveGrid, new Vector2(153.5f, -27.5f)); yield return null; }
                if (route >= 2) { Click(Button("Target_chief")); yield return null; }
                yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(output + "/" + names[route] + "-备战.png"); yield return null;
                var actual = new List<int>();
                for (int round = 0; round < expected[route].Length; ++round)
                {
                    Assert.That(coordinator.Snapshot.Phase, Is.EqualTo(BattlePhase.Preparation)); Click(Button("btn_attack"));
                    end = Time.realtimeSinceStartup + 45;
                    while (coordinator.Snapshot.CurrentStep != null && coordinator.Failure == null && Time.realtimeSinceStartup < end) yield return null;
                    Assert.That(coordinator.Failure, Is.Null); Assert.That(coordinator.Snapshot.CurrentStep, Is.Null, "表现超时");
                    Assert.That(coordinator.Snapshot.SessionId, Is.EqualTo(id)); Assert.That(coordinator.SceneBinding.gameObject.scene.handle, Is.EqualTo(handle));
                    actual.Add(coordinator.Snapshot.Hero.Health); yield return null;
                }
                Assert.That(actual, Is.EqualTo(expected[route]), names[route]); Assert.That(coordinator.Snapshot.Phase, Is.EqualTo(BattlePhase.Victory));
                Assert.That(coordinator.Snapshot.Round, Is.EqualTo(expected[route].Length)); Assert.That(coordinator.JournalError, Is.Null);
                var lines = File.ReadAllLines(coordinator.JournalPath);
                var entries = lines.Select(line => JsonUtility.FromJson<DemoBattleJournal.Entry>(line)).ToArray();
                Assert.That(entries.Any(entry => entry.kind == "命中" && entry.after.phase == "Victory"), Is.True);
                Assert.That(entries.Where(entry => entry.kind == "命中").Select(entry => entry.step).Distinct().Count(), Is.EqualTo(entries.Count(entry => entry.kind == "命中")));
                File.WriteAllLines(output + "/" + names[route] + ".jsonl", lines);
                File.WriteAllText(output + "/" + names[route] + "-结果.txt", "实际UGUI与Playable回归\n配置=" + coordinator.Catalog.Version + "\n逐轮生命=" + string.Join("→", actual) + "\n结果=" + coordinator.Snapshot.Phase + "\n轮数=" + coordinator.Snapshot.Round);
                yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(output + "/" + names[route] + "-胜利.png"); yield return null;
                Click(Button("ExitButton")); yield return null;
                end = Time.realtimeSinceStartup + 5;
                while (SceneManager.GetSceneByPath(DemoBattleSceneScope.Path).isLoaded && Time.realtimeSinceStartup < end) yield return null;
                yield return null;
            }
        }
        [UnityTearDown] public IEnumerator Cleanup() { DemoModule.Active?.Exit(); yield return null; }
    }
}
