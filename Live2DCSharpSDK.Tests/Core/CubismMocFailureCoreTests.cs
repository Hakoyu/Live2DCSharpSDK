using System.Text;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Core;

/// <summary>
/// B3: <see cref="CubismMoc"/> 的失败路径必须归还全部非托管内存.
///
/// 修复前构造函数只有 "一致性校验失败" 一条路径会释放 moc 缓冲区:
/// <c>csmReviveMocInPlace</c> 返回空指针, 模型内存分配失败或中途抛出异常时,
/// moc 缓冲区 (以及已经分配的 model 缓冲区) 都会泄漏.
/// 用例用 <see cref="TrackingAllocator"/> 的存活块计数把泄漏变成明确的断言失败,
/// 并用 <see cref="TrackingAllocator.DoubleFreeCount"/> 守住 "所有权转移后又被 catch 重复释放" 的回归.
/// 只解析 moc3 与读写模型元数据, 不创建 renderer, 不调用任何绘制 API.
/// </summary>
[TestClass]
[TestCategory("RequiresCore")]
public sealed class CubismMocFailureCoreTests
{
    /// <summary>
    /// 截断长度: 只保留 moc3 头部, Core 的 <c>csmReviveMocInPlace</c> 会明确拒绝.
    /// </summary>
    private const int TruncatedLength = 64;

    /// <summary>
    /// 损坏数据的起始偏移: 跳过 64 字节的 moc3 头部, 从偏移表开始清零.
    /// </summary>
    private const int CorruptedOffset = 64;

    /// <summary>
    /// 损坏数据的长度 (字节).
    /// </summary>
    private const int CorruptedLength = 256;

    /// <summary>
    /// 截断的 moc3 字节: 构造函数必须抛异常, 且分配器看不到任何残留块或重复释放.
    /// </summary>
    /// <param name="relativePath">相对夹具根目录的 moc3 路径</param>
    [TestMethod]
    [DataRow(ModelFixtures.HaruRelativePath)]
    [DataRow(ModelFixtures.RenRelativePath)]
    public void TruncatedMoc3_ThrowsAndReleasesEveryBlock(string relativePath)
    {
        byte[] bytes = ModelFixtures.ReadBytes(ModelFixtures.Require(relativePath));
        byte[] truncated = bytes[..TruncatedLength];

        AssertConstructorFailure(truncated, false, "MOC3 revive failed.", relativePath);
    }

    /// <summary>
    /// 非 moc3 内容: Core 拒绝还原, 构造函数必须抛异常并归还 moc 缓冲区.
    /// </summary>
    [TestMethod]
    public void NonMoc3Content_ThrowsAndReleasesEveryBlock()
    {
        byte[] text = Encoding.ASCII.GetBytes("This is not a moc3 file, only plain text.");

        AssertConstructorFailure(text, false, "MOC3 revive failed.", "plain text");
    }

    /// <summary>
    /// 不受支持的 moc3 版本 (头部偏移 4): Core 拒绝还原, 构造函数必须抛异常并归还 moc 缓冲区.
    /// </summary>
    /// <param name="relativePath">相对夹具根目录的 moc3 路径</param>
    [TestMethod]
    [DataRow(ModelFixtures.HaruRelativePath)]
    [DataRow(ModelFixtures.RenRelativePath)]
    public void UnsupportedMocVersion_ThrowsAndReleasesEveryBlock(string relativePath)
    {
        byte[] bytes = ModelFixtures.ReadBytes(ModelFixtures.Require(relativePath));

        // moc3 头部偏移 4 是版本号, 0xFF 超出 Core 支持的所有版本.
        bytes[4] = 0xFF;

        AssertConstructorFailure(bytes, false, "MOC3 revive failed.", relativePath);
    }

    /// <summary>
    /// 开启一致性校验的损坏数据: 校验失败同样由构造函数的 catch 统一释放缓冲区.
    /// </summary>
    /// <param name="relativePath">相对夹具根目录的 moc3 路径</param>
    [TestMethod]
    [DataRow(ModelFixtures.HaruRelativePath)]
    [DataRow(ModelFixtures.RenRelativePath)]
    public void CorruptedMoc3WithConsistencyCheck_ThrowsAndReleasesEveryBlock(string relativePath)
    {
        byte[] corrupted = CorruptBody(
            ModelFixtures.ReadBytes(ModelFixtures.Require(relativePath))
        );

        // 先确认这份数据确实过不了 Core 的一致性校验, 否则无法证明走的是校验失败路径.
        TrackingAllocator probeAllocator = new();
        CubismContext probeContext = CoreTestEnvironment.CreateContext(probeAllocator);
        try
        {
            Assert.IsFalse(
                CubismMoc.HasMocConsistencyFromUnrevivedMoc(probeContext, corrupted),
                $"The corrupted fixture must be rejected by the moc consistency check for '{relativePath}'."
            );
            Assert.AreEqual(
                0,
                probeAllocator.LiveBlockCount,
                $"The consistency probe must return its buffer for '{relativePath}'."
            );
            Assert.AreEqual(
                0,
                probeAllocator.DoubleFreeCount,
                $"The consistency probe must not free a block twice for '{relativePath}'."
            );
        }
        finally
        {
            probeContext.Dispose();
        }

        AssertConstructorFailure(corrupted, true, "Inconsistent MOC3.", relativePath);
    }

    /// <summary>
    /// model 内存分配抛异常: moc 缓冲区仍必须归还, 且不能触碰未分配的 model 缓冲区.
    /// </summary>
    [TestMethod]
    public void ModelMemoryAllocationFailure_ThrowsAndReleasesMocBuffer()
    {
        byte[] bytes = ModelFixtures.ReadBytes(
            ModelFixtures.Require(ModelFixtures.HaruRelativePath)
        );

        TrackingAllocator tracker = new();
        FailingModelAllocator allocator = new(tracker);
        CubismContext context = CoreTestEnvironment.CreateContext(allocator);
        try
        {
            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
                () =>
                {
                    using CubismMoc moc = new(context, bytes);
                }
            );

            StringAssert.Contains(
                exception.Message,
                FailingModelAllocator.FailureMessage,
                "The constructor must surface the model memory allocation failure."
            );
            Assert.AreEqual(
                0,
                tracker.LiveBlockCount,
                "A failed model allocation must still release the moc buffer."
            );
            Assert.AreEqual(
                0,
                tracker.DoubleFreeCount,
                "A failed model allocation must not free a block twice."
            );
        }
        finally
        {
            context.Dispose();
        }
    }

    /// <summary>
    /// 开启一致性校验的合法 moc3: 两个缓冲都存活到 Dispose, 释放后归零且没有重复释放.
    /// </summary>
    /// <param name="relativePath">相对夹具根目录的 moc3 路径</param>
    [TestMethod]
    [DataRow(ModelFixtures.HaruRelativePath)]
    [DataRow(ModelFixtures.RenRelativePath)]
    public void ValidMoc3WithConsistencyCheck_KeepsTwoBlocksUntilDisposed(string relativePath)
    {
        ModelFixtureEntry fixture = ModelFixtures.Require(relativePath);
        byte[] bytes = ModelFixtures.ReadBytes(fixture);

        TrackingAllocator allocator = new();
        CubismContext context = CoreTestEnvironment.CreateContext(allocator);
        try
        {
            using (CubismMoc moc = new(context, bytes, true))
            {
                Assert.AreEqual(
                    fixture.MocVersion,
                    moc.MocVersion,
                    $"MocVersion must match the recorded value for '{relativePath}'."
                );
                Assert.AreEqual(
                    2,
                    allocator.LiveBlockCount,
                    $"A valid moc must keep exactly the moc buffer and the model memory for '{relativePath}'."
                );
            }

            Assert.AreEqual(
                0,
                allocator.LiveBlockCount,
                $"Disposing the moc must return every block for '{relativePath}'."
            );
            Assert.AreEqual(
                0,
                allocator.DoubleFreeCount,
                $"Disposing the moc must not free a block twice for '{relativePath}'."
            );
        }
        finally
        {
            context.Dispose();
        }
    }

    /// <summary>
    /// 断言构造函数按预期失败, 且失败后分配器没有残留块与重复释放.
    /// </summary>
    /// <param name="data">交给构造函数的 moc 数据</param>
    /// <param name="shouldCheckMocConsistency">是否开启一致性校验</param>
    /// <param name="expectedMessage">预期的异常消息</param>
    /// <param name="label">用例标签, 仅用于错误消息</param>
    private static void AssertConstructorFailure(
        byte[] data,
        bool shouldCheckMocConsistency,
        string expectedMessage,
        string label
    )
    {
        TrackingAllocator allocator = new();
        CubismContext context = CoreTestEnvironment.CreateContext(allocator);
        try
        {
            Exception exception = Assert.ThrowsExactly<Exception>(() =>
            {
                using CubismMoc moc = new(context, data, shouldCheckMocConsistency);
            });

            // 先断言泄漏与重复释放, 这两项才是本次缺陷的核心; 异常消息随后校验.
            Assert.AreEqual(
                0,
                allocator.LiveBlockCount,
                $"A failed construction must return every native block for '{label}'."
            );
            Assert.AreEqual(
                0,
                allocator.DoubleFreeCount,
                $"A failed construction must not free a block twice for '{label}'."
            );
            Assert.AreEqual(
                expectedMessage,
                exception.Message,
                $"The failed construction must report its reason for '{label}'."
            );
        }
        finally
        {
            context.Dispose();
        }
    }

    /// <summary>
    /// 把 moc3 头部之后的偏移表清零, 制造 Core 一定能识别出的数据损坏.
    /// </summary>
    /// <param name="source">原始 moc3 字节</param>
    /// <returns>损坏后的副本</returns>
    private static byte[] CorruptBody(byte[] source)
    {
        byte[] corrupted = (byte[])source.Clone();
        for (int index = CorruptedOffset; index < CorruptedOffset + CorruptedLength; index++)
        {
            corrupted[index] = 0;
        }

        return corrupted;
    }

    /// <summary>
    /// 在第 2 次对齐分配 (model 内存) 上抛异常, 用于验证 moc 缓冲区仍会被归还.
    /// </summary>
    private sealed class FailingModelAllocator : ICubismAllocator
    {
        /// <summary>
        /// 模拟分配失败时抛出的异常消息.
        /// </summary>
        public const string FailureMessage = "Simulated model allocation failure.";

        /// <summary>
        /// 真正分配内存的跟踪分配器.
        /// </summary>
        private readonly TrackingAllocator _inner;

        /// <summary>
        /// 已发生的对齐分配次数.
        /// </summary>
        private int _alignedAllocationCount;

        /// <summary>
        /// 用指定的跟踪分配器包装出会失败的分配器.
        /// </summary>
        /// <param name="inner">真正分配内存的跟踪分配器</param>
        public FailingModelAllocator(TrackingAllocator inner)
        {
            _inner = inner;
        }

        /// <summary>
        /// 分配普通内存, 直接转交跟踪分配器.
        /// </summary>
        /// <param name="size">要分配的字节数</param>
        /// <returns>分配到的地址</returns>
        public IntPtr Allocate(int size) => _inner.Allocate(size);

        /// <summary>
        /// 释放普通内存, 直接转交跟踪分配器.
        /// </summary>
        /// <param name="memory">要释放的地址</param>
        public void Deallocate(IntPtr memory) => _inner.Deallocate(memory);

        /// <summary>
        /// 对齐分配: 第 2 次 (model 内存) 抛异常, 其余转交跟踪分配器.
        /// </summary>
        /// <param name="size">要分配的字节数</param>
        /// <param name="alignment">对齐宽度</param>
        /// <returns>分配到的地址</returns>
        /// <exception cref="InvalidOperationException">第 2 次对齐分配时抛出</exception>
        public IntPtr AllocateAligned(int size, int alignment)
        {
            _alignedAllocationCount++;
            if (_alignedAllocationCount == 2)
            {
                throw new InvalidOperationException(FailureMessage);
            }

            return _inner.AllocateAligned(size, alignment);
        }

        /// <summary>
        /// 释放对齐分配, 直接转交跟踪分配器.
        /// </summary>
        /// <param name="memory">要释放的地址</param>
        public void DeallocateAligned(IntPtr memory) => _inner.DeallocateAligned(memory);
    }
}
