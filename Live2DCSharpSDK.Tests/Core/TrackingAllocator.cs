using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework;

namespace Live2DCSharpSDK.Tests.Core;

/// <summary>
/// B 档用的真实内存分配器, 在分配的同时跟踪存活块.
///
/// moc3 与 model 必须放在真实的非托管内存里, 因此不能用 A 档的空分配器;
/// 这里用 <see cref="Marshal.AllocHGlobal(int)"/> 满足 Core 的 in-place API.
/// 释放时先查存活表: 同一个地址被释放第二次时只记入 <see cref="DoubleFreeCount"/> 而不真正 free,
/// 这样即使产品代码回归到 "Dispose 不幂等", 用例也能给出干净的断言失败, 而不是让测试宿主堆损坏.
/// </summary>
internal sealed class TrackingAllocator : ICubismAllocator
{
    /// <summary>
    /// 普通分配的存活块.
    /// </summary>
    private readonly HashSet<IntPtr> _blocks = [];

    /// <summary>
    /// 对齐分配: 键为对齐后的地址, 值为 <see cref="Marshal.AllocHGlobal(int)"/> 返回的原始地址.
    /// </summary>
    private readonly Dictionary<IntPtr, IntPtr> _alignedBlocks = [];

    /// <summary>
    /// 重复释放 (或释放了未知地址) 的次数.
    /// </summary>
    public int DoubleFreeCount { get; private set; }

    /// <summary>
    /// 当前仍然存活的块数量 (普通分配 + 对齐分配).
    /// </summary>
    public int LiveBlockCount => _blocks.Count + _alignedBlocks.Count;

    /// <summary>
    /// 分配一块普通内存并登记.
    /// </summary>
    /// <param name="size">要分配的字节数</param>
    /// <returns>分配到的地址, 失败时为 <see cref="IntPtr.Zero"/></returns>
    public IntPtr Allocate(int size)
    {
        if (size < 0)
        {
            return IntPtr.Zero;
        }

        IntPtr block = Marshal.AllocHGlobal(System.Math.Max(size, 1));
        _blocks.Add(block);

        return block;
    }

    /// <summary>
    /// 释放普通分配, 重复释放只计数.
    /// </summary>
    /// <param name="memory">要释放的地址</param>
    public void Deallocate(IntPtr memory)
    {
        if (memory == IntPtr.Zero)
        {
            return;
        }

        if (_blocks.Remove(memory) is false)
        {
            DoubleFreeCount++;
            return;
        }

        Marshal.FreeHGlobal(memory);
    }

    /// <summary>
    /// 按对齐要求分配内存并登记对齐后的地址.
    /// </summary>
    /// <param name="size">要分配的字节数</param>
    /// <param name="alignment">对齐宽度, 非法时回退到 16</param>
    /// <returns>满足对齐的地址, 失败时为 <see cref="IntPtr.Zero"/></returns>
    public IntPtr AllocateAligned(int size, int alignment)
    {
        if (size < 0)
        {
            return IntPtr.Zero;
        }

        int effectiveAlignment = NormalizeAlignment(alignment);
        long padding = effectiveAlignment - 1L;
        long total = size + padding;

        // 多分配 alignment - 1 字节, 再向前找到满足对齐的地址; 原始地址记在表里用于释放.
        IntPtr original = Marshal.AllocHGlobal((IntPtr)total);
        long alignedAddress = (original.ToInt64() + padding) & ~padding;
        IntPtr aligned = (IntPtr)alignedAddress;

        _alignedBlocks.Add(aligned, original);

        return aligned;
    }

    /// <summary>
    /// 释放对齐分配, 重复释放只计数.
    /// </summary>
    /// <param name="alignedMemory">AllocateAligned 返回的地址</param>
    public void DeallocateAligned(IntPtr alignedMemory)
    {
        if (alignedMemory == IntPtr.Zero)
        {
            return;
        }

        if (_alignedBlocks.Remove(alignedMemory, out IntPtr original) is false)
        {
            DoubleFreeCount++;
            return;
        }

        Marshal.FreeHGlobal(original);
    }

    /// <summary>
    /// 校验对齐值, 非法时归入 16 (与产品分配器的约定一致).
    /// </summary>
    /// <param name="alignment">对齐宽度</param>
    /// <returns>2 的幂对齐值</returns>
    private static int NormalizeAlignment(int alignment)
    {
        if (alignment > 0 && (alignment & (alignment - 1)) == 0)
        {
            return alignment;
        }

        return 16;
    }
}
