using MG01.Demo.Configuration;
using NUnit.Framework;

namespace MG01.Demo.Tests
{
    public sealed class ConfigurationBoundaryTests
    {
        [Test]
        public void 核心配置程序集不引用引擎和热更()
        {
            foreach (var assembly in typeof(ConfigSchema).Assembly.GetReferencedAssemblies())
            {
                Assert.That(assembly.Name, Does.Not.StartWith("Unity"));
                Assert.That(assembly.Name, Is.Not.EqualTo("HotFix"));
            }
        }
    }
}
