using Obfuz;

namespace MG01.Demo
{
    /// <summary>显式使用UIStage，常驻展示由独立根持有。框架只负责页面资源与显隐。</summary>
    [ObfuzIgnore(ObfuzScope.TypeName)]
    public sealed class DemoStagePage : LayoutScript
    {
        public override void assignWindow() { }
        public UnityEngine.Transform Root => getRoot().getGameObject().transform;
    }
}
