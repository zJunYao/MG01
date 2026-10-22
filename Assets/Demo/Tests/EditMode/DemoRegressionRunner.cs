using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MG01.Demo.Tests
{
    /// <summary>直接使用Unity官方测试API，MCP测试作业异常中断时也可运行并落盘结果。不修改插件缓存。</summary>
    [InitializeOnLoad]
    public sealed class DemoRegressionRunner : ICallbacks
    {
        private static readonly TestRunnerApi api;
        private const string Key = "MG01.Demo.RegressionMode";
        static DemoRegressionRunner()
        { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new DemoRegressionRunner()); }
        [MenuItem("Demo/验证/运行编辑模式回归并保存结果")]
        public static void Edit() => Run(TestMode.EditMode, "Demo.EditModeTests");
        [MenuItem("Demo/验证/运行播放模式回归并保存结果")]
        public static void Play() => Run(TestMode.PlayMode, "Demo.PlayModeTests");
        private static void Run(TestMode mode, string assembly)
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("请先退出Play");
            SessionState.SetString(Key, mode.ToString());
            api.Execute(new ExecutionSettings(new Filter { testMode = mode, assemblyNames = new[] { assembly } }));
        }
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            string mode = SessionState.GetString(Key, ""); if (mode.Length == 0) return;
            string folder = "Assets/Demo/Documentation/Evidence/Phase5"; Directory.CreateDirectory(folder);
            File.WriteAllText(folder + "/" + mode + "-测试结果.xml", result.ToXml().OuterXml);
            File.WriteAllText(folder + "/" + mode + "-测试摘要.txt", "状态=" + result.ResultState + "\n通过=" + result.PassCount + "\n失败=" + result.FailCount + "\n跳过=" + result.SkipCount + "\n耗时秒=" + result.Duration);
            SessionState.EraseString(Key); Debug.Log("Demo回归完成：" + mode + " " + result.ResultState);
        }
    }
}
