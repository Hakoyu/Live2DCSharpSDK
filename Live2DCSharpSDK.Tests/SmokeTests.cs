using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Motion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests;

/// <summary>
/// 工程骨架的冒烟测试.
///
/// 只验证测试宿主能启动, 测试程序集能解析 Framework / App 引用, 以及 InternalsVisibleTo 生效;
/// 不加载 Core, 不创建任何渲染资源, 因此属于 A 档 (纯托管).
/// </summary>
[TestClass]
public sealed class SmokeTests
{
    /// <summary>
    /// 测试宿主能启动并执行断言.
    /// </summary>
    [TestMethod]
    public void TestHostRuns()
    {
        // 故意断言恒真: 冒烟测试只验证测试宿主能启动并执行断言, 不表达业务含义.
#pragma warning disable MSTEST0032 // 恒真断言是冒烟测试的本意
        Assert.IsTrue(true);
#pragma warning restore MSTEST0032
    }

    /// <summary>
    /// 测试程序集引用了 Framework 与 App, 且没有引入 OpenGL / Vulkan.
    /// </summary>
    [TestMethod]
    public void ReferencesOnlyManagedProjects()
    {
        Assert.AreEqual("Live2DCSharpSDK.Framework", typeof(CubismContext).Assembly.GetName().Name);
        Assert.AreEqual(
            "Live2DCSharpSDK.App",
            typeof(ModelResourceLocator).Assembly.GetName().Name
        );

        string[] renderers = typeof(SmokeTests)
            .Assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .Where(name =>
                name.StartsWith("Live2DCSharpSDK.OpenGL", StringComparison.Ordinal)
                || name.StartsWith("Live2DCSharpSDK.Vulkan", StringComparison.Ordinal)
            )
            .ToArray();

        Assert.AreEqual(
            0,
            renderers.Length,
            $"Test project must not reference OpenGL/Vulkan: {string.Join(", ", renderers)}"
        );
    }

    /// <summary>
    /// Framework 与 App 的 InternalsVisibleTo 生效: 测试程序集能访问 internal 成员.
    /// </summary>
    [TestMethod]
    public void InternalsVisibleToIsConfigured()
    {
        // Framework: ICubismUpdater.ExecutionOrder 是 internal, 只有 IVT 生效才能编译并读取.
        var updater = new ProbeUpdater((int)CubismUpdateOrder.Breath);
        Assert.AreEqual((int)CubismUpdateOrder.Breath, updater.ExecutionOrder);

        // App: LAppDelegate.Latest 是 internal, 测试进程里尚未创建 delegate, 应为 null.
        Assert.IsNull(LAppDelegate.Latest);
    }

    /// <summary>
    /// 供 IVT 检查使用的空更新处理实现, 不访问模型.
    /// </summary>
    private sealed class ProbeUpdater : ICubismUpdater
    {
        /// <summary>
        /// 以指定执行顺序创建.
        /// </summary>
        /// <param name="executionOrder">执行顺序</param>
        public ProbeUpdater(int executionOrder)
            : base(executionOrder) { }

        /// <summary>
        /// 空实现, 不触碰模型.
        /// </summary>
        /// <param name="model">更新目标的模型</param>
        /// <param name="deltaTimeSeconds">增量时间[秒]</param>
        public override void OnLateUpdate(CubismModel model, float deltaTimeSeconds) { }
    }
}
