using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework;

namespace Live2DCSharpSDK.App;

/// <summary>
/// 内存分配与释放接口的实现.
/// 由框架调用.
/// </summary>
/// <remarks>
/// 带对齐的分配与释放委托给 .NET 6+ 的 <see cref="NativeMemory"/> 完成.
/// 旧实现使用 <see cref="IntPtr"/>(有符号 nint)进行地址运算与取余,
/// 因此在地址最高位为 1 的环境下 `alignedAddress % alignment` 会变成负数,
/// 把 preamble 写到分配区域之外, 从而破坏堆. 此外 `(int)(size + offset)` 的
/// 32 位截断, 以及分配失败(null)时仍写入 preamble 的问题也没有处理.
/// <see cref="NativeMemory.AlignedAlloc"/> / <see cref="NativeMemory.AlignedFree"/> 的
/// 分配与释放由同一套 API 成对完成, 因此保存原始地址的 preamble 本身不再需要.
/// </remarks>
public class LAppAllocator : ICubismAllocator
{
    /// <summary>
    /// 传入非法对齐值时使用的回退值.
    /// (Framework 实际只要求 Moc = 64 / Model = 16, 其余情况属于调用方缺陷.)
    /// </summary>
    private const int FallbackAlignment = 16;

    /// <summary>
    /// 分配内存区域.
    /// </summary>
    /// <param name="size">希望分配的字节数.</param>
    /// <returns>指定的内存区域. 失败时返回 <see cref="IntPtr.Zero"/>.</returns>
    public IntPtr Allocate(int size)
    {
        // ICubismAllocator 契约: 失败时返回 0(负数不是合法尺寸).
        if (size < 0)
        {
            LogErrorNoThrow($"Allocate() called with a negative size: {size}.");
            return IntPtr.Zero;
        }

        return Marshal.AllocHGlobal(size);
    }

    /// <summary>
    /// 释放内存区域.
    /// </summary>
    /// <param name="memory">要释放的内存.</param>
    public void Deallocate(IntPtr memory)
    {
        // 0 表示分配失败, 因此忽略释放请求(Marshal.FreeHGlobal(0) 本身也是 no-op).
        if (memory == IntPtr.Zero)
        {
            return;
        }

        Marshal.FreeHGlobal(memory);
    }

    /// <summary>
    /// 按对齐约束分配内存区域.
    /// </summary>
    /// <param name="size">希望分配的字节数.</param>
    /// <param name="alignment">内存块的对齐宽度(2 的幂).</param>
    /// <returns>满足指定对齐的内存区域. 失败时返回 <see cref="IntPtr.Zero"/>.</returns>
    public unsafe IntPtr AllocateAligned(int size, int alignment)
    {
        if (size < 0)
        {
            LogErrorNoThrow($"AllocateAligned() called with a negative size: {size}.");
            return IntPtr.Zero;
        }

        int effectiveAlignment = NormalizeAlignment(alignment);

        try
        {
            // NativeMemory.AlignedAlloc 只接受 2 的幂对齐(上面的规范化已保证).
            // 该 API 失败时会抛出 OutOfMemoryException, 因此按契约(返回 0)捕获它.
            void* memory = NativeMemory.AlignedAlloc((nuint)size, (nuint)effectiveAlignment);

            if (memory == null)
            {
                LogErrorNoThrow($"AllocateAligned() failed. size={size}, alignment={effectiveAlignment}.");
                return IntPtr.Zero;
            }

            return (IntPtr)memory;
        }
        catch (OutOfMemoryException)
        {
            LogErrorNoThrow($"AllocateAligned() failed. size={size}, alignment={effectiveAlignment}.");
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 释放带对齐约束的内存区域.
    /// </summary>
    /// <param name="alignedMemory">要释放的内存</param>
    /// <remarks>
    /// 参数必须是 <see cref="AllocateAligned"/> 返回的地址(分配与释放必须对称).
    /// 当前调用方只有 Framework 的 CubismMoc / CubismModel, 两者都直接传递 AllocateAligned 的
    /// 返回值(Core 的 in-place API 会返回同一地址, 已通过 probe 确认).
    /// 0 作为释放请求忽略(旧实现会在这里读取 preamble[-1] 并触发访问违例).
    /// </remarks>
    public unsafe void DeallocateAligned(IntPtr alignedMemory)
    {
        if (alignedMemory == IntPtr.Zero)
        {
            return;
        }

        NativeMemory.AlignedFree((void*)alignedMemory);
    }

    /// <summary>
    /// 校验对齐值, 非法时归入安全的默认值.
    /// </summary>
    private static int NormalizeAlignment(int alignment)
    {
        // 1 是 2 的 0 次幂, 有效; 0 及以下, 非 2 的幂均无效.
        if (alignment > 0 && (alignment & (alignment - 1)) == 0)
        {
            return alignment;
        }

        CubismLog.Warning($"LAppAllocator: invalid alignment {alignment}. Fallback to {FallbackAlignment}.");
        return FallbackAlignment;
    }

    /// <summary>
    /// 以 Error 级别输出日志, 但不抛出异常.
    /// </summary>
    /// <remarks>
    /// <see cref="CubismLog.Error"/> 输出日志后必定抛异常(本次不改动其本身),
    /// 与 ICubismAllocator "分配失败时返回 0" 的契约冲突.
    /// 因此保持与 CubismLog 相同的输出条件(日志级别), 只省略 throw.
    /// </remarks>
    private static void LogErrorNoThrow(string message)
    {
        // 输出条件与 CubismLog 相同(旧实现不等号方向写反, 默认 LoggingLevel 下
        // 完全不输出错误日志; LogLevel 的数值越大表示严重度越高).
        if (LogLevel.Error < CubismFramework.GetLoggingLevel())
        {
            return;
        }

        CubismFramework.CoreLogFunction($"[CSM] [E] {message}");
    }
}
