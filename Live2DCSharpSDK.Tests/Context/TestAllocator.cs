using Live2DCSharpSDK.Framework;

namespace Live2DCSharpSDK.Tests.Context;

/// <summary>
/// 测试用的空内存分配器.
/// A6 / A8 / A9 的用例只构造上下文, 不会真的分配内存;
/// 这里用计数器暴露意外分配, 避免悄悄走到 Core.
/// </summary>
internal sealed class TestAllocator : ICubismAllocator
{
    /// <summary>
    /// Allocate 的调用次数.
    /// </summary>
    public int AllocateCallCount { get; private set; }

    /// <summary>
    /// AllocateAligned 的调用次数.
    /// </summary>
    public int AllocateAlignedCallCount { get; private set; }

    /// <summary>
    /// 空实现, 只记录调用次数.
    /// </summary>
    /// <param name="size">要分配的字节数</param>
    /// <returns>总是 0</returns>
    public IntPtr Allocate(int size)
    {
        AllocateCallCount++;
        return IntPtr.Zero;
    }

    /// <summary>
    /// 空实现.
    /// </summary>
    /// <param name="memory">要释放的内存地址</param>
    public void Deallocate(IntPtr memory) { }

    /// <summary>
    /// 空实现, 只记录调用次数.
    /// </summary>
    /// <param name="size">要分配的字节数</param>
    /// <param name="alignment">对齐宽度</param>
    /// <returns>总是 0</returns>
    public IntPtr AllocateAligned(int size, int alignment)
    {
        AllocateAlignedCallCount++;
        return IntPtr.Zero;
    }

    /// <summary>
    /// 空实现.
    /// </summary>
    /// <param name="alignedMemory">要释放的内存地址</param>
    public void DeallocateAligned(IntPtr alignedMemory) { }
}
