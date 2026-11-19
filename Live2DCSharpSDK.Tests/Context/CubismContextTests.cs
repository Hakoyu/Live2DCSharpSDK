using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Id;
using Live2DCSharpSDK.Tests.Id;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Context;

/// <summary>
/// <see cref="CubismContext"/> 的纯托管生命周期契约.
/// 这组用例不调用 StartUp (它会注册进程级 Core 回调, 属于 B 档),
/// 只验证 Create / Dispose / 访问器的托管行为.
/// </summary>
[TestClass]
public sealed class CubismContextTests
{
    /// <summary>
    /// 测试用的空日志函数, 避免任何路径回落到 Core 日志回调.
    /// </summary>
    private static readonly LogFunction s_noopLog = static _ => { };

    /// <summary>
    /// Create 绑定传入的分配器与配置, 并沿用配置里的日志级别; 创建本身不启动上下文.
    /// </summary>
    [TestMethod]
    public void Create_BindsAllocatorOptionAndLogLevel()
    {
        TestAllocator allocator = new();
        CubismOption option = new()
        {
            LogFunction = s_noopLog,
            LoggingLevel = LogLevel.Warning
        };

        CubismContext context = CubismContext.Create(allocator, option);
        try
        {
            Assert.AreSame(allocator, context.Allocator);
            Assert.AreSame(option, context.Option);
            Assert.AreEqual(LogLevel.Warning, context.LogLevel);
            Assert.IsFalse(context.IsStarted);
            Assert.IsNotNull(context.IdManager);
            Assert.IsFalse(ReferenceEquals(CubismContext.Current, context));
            Assert.AreEqual(0, allocator.AllocateCallCount);
            Assert.AreEqual(0, allocator.AllocateAlignedCallCount);
        }
        finally
        {
            context.Dispose();
        }
    }

    /// <summary>
    /// Create 的两个参数为 null 时抛 ArgumentNullException.
    /// </summary>
    [TestMethod]
    public void Create_WithNullArguments_ThrowsArgumentNullException()
    {
        CubismOption option = new()
        {
            LogFunction = s_noopLog,
            LoggingLevel = LogLevel.Info
        };

        Assert.ThrowsExactly<ArgumentNullException>(() => CubismContext.Create(null!, option));
        Assert.ThrowsExactly<ArgumentNullException>(() => CubismContext.Create(new TestAllocator(), null!));
    }

    /// <summary>
    /// Dispose 可以重复调用, 不抛异常.
    /// </summary>
    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        CubismContext context = CreateContext();

        context.Dispose();
        context.Dispose();
        context.Dispose();

        Assert.IsFalse(context.IsStarted);
    }

    /// <summary>
    /// Dispose 之后读取 Allocator / Option 抛 InvalidOperationException.
    /// </summary>
    [TestMethod]
    public void AfterDispose_AllocatorAndOptionThrowInvalidOperationException()
    {
        CubismContext context = CreateContext();
        context.Dispose();

        Assert.ThrowsExactly<InvalidOperationException>(() => _ = context.Allocator);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = context.Option);
    }

    /// <summary>
    /// Dispose 之后调用 StartUp 抛 ObjectDisposedException, 不会走到 Core 注册.
    /// </summary>
    [TestMethod]
    public void AfterDispose_StartUpThrowsObjectDisposedException()
    {
        CubismContext context = CreateContext();
        context.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => context.StartUp());
    }

    /// <summary>
    /// Dispose 不替换 IdManager: 仍是原实例, 仍可登记, 且与其它上下文互不共享.
    /// </summary>
    [TestMethod]
    public void Dispose_KeepsIdManagerAndIsolatesFromOtherContexts()
    {
        CubismContext first = CreateContext();
        CubismContext second = CreateContext();
        try
        {
            CubismIdManager firstIds = first.IdManager;
            firstIds.GetId("ParamAngleX");

            first.Dispose();

            Assert.AreSame(firstIds, first.IdManager);
            Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(first.IdManager));
            CollectionAssert.AreEqual(
                new[] { "ParamAngleX" },
                CubismIdManagerProbe.GetRegisteredIds(first.IdManager).ToArray());

            Assert.AreNotSame(firstIds, second.IdManager);
            Assert.AreNotSame(firstIds, CubismContext.Default.IdManager);
            Assert.AreEqual(0, CubismIdManagerProbe.GetRegisteredCount(second.IdManager));

            second.IdManager.GetId("ParamAngleY");

            Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(first.IdManager));
            Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(second.IdManager));
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }
    }

    /// <summary>
    /// 全局默认上下文没有绑定分配器与配置, 读取时抛 InvalidOperationException.
    /// </summary>
    [TestMethod]
    public void Default_WithoutCreate_HasNoAllocatorOrOption()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = CubismContext.Default.Allocator);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = CubismContext.Default.Option);
    }

    /// <summary>
    /// 创建一个使用空分配器与空日志函数的上下文.
    /// </summary>
    /// <returns>尚未启动的上下文实例</returns>
    private static CubismContext CreateContext()
    {
        CubismOption option = new()
        {
            LogFunction = s_noopLog,
            LoggingLevel = LogLevel.Info
        };

        return CubismContext.Create(new TestAllocator(), option);
    }
}
