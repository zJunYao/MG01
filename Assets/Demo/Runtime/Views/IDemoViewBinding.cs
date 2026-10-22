using MG01.Demo.Configuration;

namespace MG01.Demo.Views
{
    /// <summary>视图绑定只读配置并显式解绑，不依赖HotFix或框架表行。</summary>
    public interface IDemoViewBinding
    {
        void Bind(DemoConfigCatalog catalog);
        void Unbind();
    }
}
