using System.Runtime.InteropServices;
using Live2DCSharpSDK.App;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.App;

/// <summary>
/// A11: <see cref="LAppAllocator"/> 的对齐与非法输入契约.
///
/// 每一条分配都在 finally 中释放, 避免测试自身泄漏; 不测真实 OOM (无法稳定注入),
/// 也不把 0 字节分配的返回值当作契约 (见 PLAN-06 §4 A11).
/// 旧实现用带符号 nint 做地址取余, 在地址最高位为 1 时得到负余数并把 preamble 写到区域之外,
/// 对齐用例就是该缺陷的回归保护.
/// </summary>
[TestClass]
public sealed class LAppAllocatorTests
{
    /// <summary>
    /// 每轮分配的块数: 足以暴露 "同一个地址被重复返回" 的问题, 同时保持用例足够快.
    /// </summary>
    private const int AllocationCount = 1000;

    /// <summary>
    /// 每块请求的字节数 (不取对齐宽度的整数倍, 便于验证地址本身的对齐).
    /// </summary>
    private const int BlockSize = 32;

    /// <summary>
    /// 1 / 8 / 16 / 64 四种对齐下, 每个地址都满足对齐要求, 且 1000 次分配互不相同.
    /// </summary>
    /// <param name="alignment">对齐宽度</param>
    [TestMethod]
    [DataRow(1)]
    [DataRow(8)]
    [DataRow(16)]
    [DataRow(64)]
    public void AllocateAligned_SatisfiesAlignmentAndReturnsDistinctBlocks(int alignment)
    {
        LAppAllocator allocator = new();

        // 连续两轮: 释放后重新分配仍然是 1000 个互不相同的地址, distinct 计数不会增长.
        AssertAllocationRound(allocator, alignment);
        AssertAllocationRound(allocator, alignment);
    }

    /// <summary>
    /// 非 2 的幂与非法对齐值会被归一化为 16, 分配依然成功且满足 16 字节对齐.
    /// </summary>
    /// <param name="alignment">非法或非 2 的幂的对齐值</param>
    [TestMethod]
    [DataRow(24)]
    [DataRow(3)]
    [DataRow(48)]
    [DataRow(0)]
    [DataRow(-8)]
    public void AllocateAligned_NormalizesInvalidAlignmentTo16(int alignment)
    {
        // 归一化会输出 Warning 级日志, 先关日志避免触发 Core 的日志回调 (A 档不加载 Core).
        using SilentLogScope logScope = new();
        LAppAllocator allocator = new();
        IntPtr block = IntPtr.Zero;
        try
        {
            // 若不归一化, NativeMemory.AlignedAlloc 会对非 2 的幂抛 ArgumentOutOfRangeException.
            block = allocator.AllocateAligned(BlockSize, alignment);

            Assert.AreNotEqual(
                IntPtr.Zero,
                block,
                $"The fallback alignment must still return a block (alignment={alignment})."
            );
            Assert.AreEqual(
                0UL,
                (ulong)block.ToInt64() % 16UL,
                $"The fallback alignment must be 16 bytes (alignment={alignment})."
            );
        }
        finally
        {
            allocator.DeallocateAligned(block);
        }
    }

    /// <summary>
    /// 负数尺寸按契约返回 <see cref="IntPtr.Zero"/>, 不抛异常.
    /// </summary>
    /// <param name="size">负数尺寸</param>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public void AllocateWithNegativeSize_ReturnsZero(int size)
    {
        // 负数尺寸会输出 Error 级日志, 先关日志避免触发 Core 的日志回调 (A 档不加载 Core).
        using SilentLogScope logScope = new();
        LAppAllocator allocator = new();

        Assert.AreEqual(IntPtr.Zero, allocator.Allocate(size));
        Assert.AreEqual(IntPtr.Zero, allocator.AllocateAligned(size, 16));
    }

    /// <summary>
    /// 释放 0 地址是 no-op, 不抛异常.
    /// </summary>
    [TestMethod]
    public void DeallocateOfZero_DoesNotThrow()
    {
        LAppAllocator allocator = new();

        // 旧实现在这里读取 preamble[-1], 释放 0 会触发访问违例; 修复后必须是安全的 no-op.
        allocator.Deallocate(IntPtr.Zero);
        allocator.DeallocateAligned(IntPtr.Zero);
    }

    /// <summary>
    /// 普通分配与释放可以完成一次内存读写往返.
    /// </summary>
    [TestMethod]
    public void AllocateAndDeallocate_PerformsMemoryRoundTrip()
    {
        LAppAllocator allocator = new();
        IntPtr block = IntPtr.Zero;
        try
        {
            block = allocator.Allocate(1024);
            Assert.AreNotEqual(IntPtr.Zero, block);

            // 写入首尾两个字节再读回, 确认分配到的区域确实可以访问.
            Marshal.WriteByte(block, 0, 0x5A);
            Marshal.WriteByte(block, 1023, 0xA5);

            Assert.AreEqual((byte)0x5A, Marshal.ReadByte(block, 0));
            Assert.AreEqual((byte)0xA5, Marshal.ReadByte(block, 1023));
        }
        finally
        {
            allocator.Deallocate(block);
        }
    }

    /// <summary>
    /// 执行一轮分配, 校验对齐与唯一性, 并在 finally 中释放全部块.
    /// </summary>
    /// <param name="allocator">被测分配器</param>
    /// <param name="alignment">对齐宽度</param>
    private static void AssertAllocationRound(LAppAllocator allocator, int alignment)
    {
        List<IntPtr> blocks = [];
        try
        {
            for (int index = 0; index < AllocationCount; index++)
            {
                IntPtr block = allocator.AllocateAligned(BlockSize, alignment);
                Assert.AreNotEqual(
                    IntPtr.Zero,
                    block,
                    $"AllocateAligned returned a null block at index {index}."
                );
                blocks.Add(block);
            }

            Assert.AreEqual(AllocationCount, blocks.Count);

            for (int index = 0; index < blocks.Count; index++)
            {
                ulong address = (ulong)blocks[index].ToInt64();
                Assert.AreEqual(
                    0UL,
                    address % (ulong)alignment,
                    $"Block {index} is not aligned to {alignment} bytes."
                );
            }

            int distinct = blocks.Distinct().Count();
            Assert.AreEqual(
                AllocationCount,
                distinct,
                "Aligned allocations must not return the same address twice while the blocks are alive."
            );
        }
        finally
        {
            foreach (IntPtr block in blocks)
            {
                allocator.DeallocateAligned(block);
            }
        }
    }
}
