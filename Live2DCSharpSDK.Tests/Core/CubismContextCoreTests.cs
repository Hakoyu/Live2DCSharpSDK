using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Tests.Id;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Core;

/// <summary>
/// B3: <see cref="CubismContext"/> 的 StartUp / Dispose 生命周期与 Core 隔离.
///
/// 两个上下文各自 StartUp, Current / Allocator / IdManager / LogLevel 互不串扰;
/// Dispose 其中一个之后, 另一个仍能解析 moc 并正常释放.
/// 用例只使用 moc3 解析与模型元数据, 不创建 renderer, 不调用任何绘制 API.
/// 因为会写入进程级的 CubismContext.Current 与 Core 日志回调, 这里显式要求串行执行.
/// </summary>
[TestClass]
[TestCategory("RequiresCore")]
[DoNotParallelize]
public sealed class CubismContextCoreTests
{
    /// <summary>
    /// 两个上下文各自 StartUp 与 Dispose, Current 按启动顺序切换并逐层恢复.
    /// </summary>
    [TestMethod]
    public void TwoContexts_StartUpIndependentlyAndReleaseInIsolation()
    {
        CubismContext? previousCurrent = CubismContext.Current;
        TrackingAllocator firstAllocator = new();
        TrackingAllocator secondAllocator = new();
        CubismContext first = CoreTestEnvironment.CreateContext(firstAllocator);
        CubismContext second = CoreTestEnvironment.CreateContext(secondAllocator);
        using CoreLogFunctionScope coreLogScope = new();
        try
        {
            Assert.IsFalse(first.IsStarted, "A newly created context must not be started.");
            Assert.IsFalse(second.IsStarted, "A newly created context must not be started.");
            Assert.AreNotSame(
                previousCurrent,
                first,
                "Create must not install the context as Current."
            );

            first.StartUp();
            Assert.IsTrue(first.IsStarted, "StartUp must mark the first context as started.");
            Assert.AreSame(
                first,
                CubismContext.Current,
                "StartUp must install the context as Current."
            );
            Assert.IsFalse(
                second.IsStarted,
                "Starting the first context must not start the second one."
            );

            // StartUp 是幂等的: 再次调用不会抛异常, Current 仍然是本上下文.
            first.StartUp();
            Assert.IsTrue(first.IsStarted, "A repeated StartUp must keep the context started.");
            Assert.AreSame(
                first,
                CubismContext.Current,
                "A repeated StartUp must keep the context current."
            );

            second.StartUp();
            Assert.IsTrue(second.IsStarted, "StartUp must mark the second context as started.");
            Assert.AreSame(
                second,
                CubismContext.Current,
                "The second StartUp must install the second context."
            );
            Assert.IsTrue(
                first.IsStarted,
                "Starting the second context must not dispose the first one."
            );

            // Allocator / IdManager / LogLevel 属于各自上下文, 不共享.
            Assert.AreNotSame(
                first.Allocator,
                second.Allocator,
                "Each context must keep its own allocator."
            );
            Assert.AreNotSame(
                first.IdManager,
                second.IdManager,
                "Each context must keep its own id manager."
            );

            first.IdManager.GetId("ParamAngleX");
            Assert.AreEqual(
                1,
                CubismIdManagerProbe.GetRegisteredCount(first.IdManager),
                "The first id manager must see its own registration."
            );
            Assert.AreEqual(
                0,
                CubismIdManagerProbe.GetRegisteredCount(second.IdManager),
                "The second id manager must not see the first registration."
            );

            second.IdManager.GetId("ParamAngleY");
            Assert.AreEqual(
                1,
                CubismIdManagerProbe.GetRegisteredCount(second.IdManager),
                "The second id manager must see its own registration."
            );
            Assert.AreEqual(
                1,
                CubismIdManagerProbe.GetRegisteredCount(first.IdManager),
                "The first id manager must not see the second registration."
            );

            first.LogLevel = LogLevel.Warning;
            second.LogLevel = LogLevel.Verbose;
            Assert.AreEqual(
                LogLevel.Warning,
                first.LogLevel,
                "The first context must keep its own log level."
            );
            Assert.AreEqual(
                LogLevel.Verbose,
                second.LogLevel,
                "The second context must keep its own log level."
            );

            // Dispose 第二个上下文: 第一个仍然可用, Current 恢复为第一个.
            second.Dispose();
            Assert.IsFalse(second.IsStarted, "Dispose must clear the started flag.");
            Assert.IsTrue(
                first.IsStarted,
                "Disposing the second context must not stop the first one."
            );
            Assert.AreSame(
                first,
                CubismContext.Current,
                "Disposing the current context must restore the previous one."
            );
            Assert.ThrowsExactly<InvalidOperationException>(
                () => _ = second.Allocator,
                "A disposed context must not expose an allocator."
            );
            Assert.ThrowsExactly<InvalidOperationException>(
                () => _ = second.Option,
                "A disposed context must not expose an option."
            );
            Assert.ThrowsExactly<ObjectDisposedException>(
                () => second.StartUp(),
                "StartUp on a disposed context must throw ObjectDisposedException."
            );

            // 第一个上下文仍能与 Core 工作: 解析 Haru 并正常释放.
            ModelFixtureEntry fixture = ModelFixtures.Require(ModelFixtures.HaruRelativePath);
            using (CubismMoc moc = new(first, ModelFixtures.ReadBytes(fixture)))
            {
                Assert.AreSame(
                    first,
                    moc.Model.Context,
                    "The surviving context must stay bound to the model it revives."
                );
                Assert.AreEqual(
                    fixture.DrawableCount,
                    moc.Model.GetDrawableCount(),
                    "The surviving context must still revive the fixture model."
                );
                Assert.AreEqual(
                    2,
                    firstAllocator.LiveBlockCount,
                    "The surviving context must allocate the moc buffer and the model memory."
                );
            }

            Assert.AreEqual(
                0,
                firstAllocator.LiveBlockCount,
                "The surviving context must release the moc blocks."
            );
            Assert.AreEqual(
                0,
                firstAllocator.DoubleFreeCount,
                "The surviving context must not free any block twice."
            );
            Assert.AreEqual(
                0,
                secondAllocator.LiveBlockCount,
                "The disposed context never allocated any block."
            );

            // Dispose 第一个: Current 恢复到进入用例前的值.
            first.Dispose();
            Assert.IsFalse(
                first.IsStarted,
                "Dispose must clear the started flag of the first context."
            );
            Assert.AreSame(
                previousCurrent,
                CubismContext.Current,
                "Disposing the last started context must restore the previous Current value."
            );
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }
    }
}
