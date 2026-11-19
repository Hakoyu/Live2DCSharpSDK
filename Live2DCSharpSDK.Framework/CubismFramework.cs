using Live2DCSharpSDK.Framework.Id;

namespace Live2DCSharpSDK.Framework;

/// <summary>
/// 过渡期兼容门面(M-1 / S1).
/// 所有静态成员都转调 <see cref="CubismContext.Default"/>, 静态类内部不再持有进程级状态;
/// 该门面计划在调用点迁移完成后(S4)删除, 新代码请直接使用 <see cref="CubismContext"/> 实例.
/// </summary>
public static class CubismFramework
{
    /// <summary>
    /// 网格顶点的偏移值
    /// </summary>
    public const int VertexOffset = 0;
    /// <summary>
    /// 网格顶点的步进值
    /// </summary>
    public const int VertexStep = 2;

    /// <summary>
    /// 默认上下文的 ID 管理器.
    /// </summary>
    public static CubismIdManager CubismIdManager => CubismContext.Default.IdManager;

    /// <summary>
    /// 默认上下文是否已启动.
    /// </summary>
    public static bool IsStarted => CubismContext.Default.IsStarted;

    /// <summary>
    /// 使 Cubism Framework 的 API 可用.
    /// 执行 API 之前必须先调用该函数.
    /// 必须传入内存分配器参数.
    /// 准备完成后再次调用会跳过内部处理.
    /// </summary>
    /// <param name="allocator">ICubismAllocator 类的实例</param>
    /// <param name="option">CubismOption 类的实例</param>
    /// <returns>准备处理完成时返回 true.</returns>
    public static bool StartUp(ICubismAllocator allocator, CubismOption option)
        => CubismContext.Default.StartUp(allocator, option);

    /// <summary>
    /// 清除由 StartUp() 初始化的状态.
    /// 在复用已 CleanUp() 的默认上下文时使用.
    /// </summary>
    public static void CleanUp()
        => CubismContext.Default.CleanUp();

    /// <summary>
    /// 执行绑定到 Core API 的日志函数.
    /// </summary>
    /// <param name="data">日志消息</param>
    public static void CoreLogFunction(string data)
        => CubismContext.InvokeCoreLogFunction(data);

    /// <summary>
    /// 返回当前的日志输出级别设置值.
    /// </summary>
    /// <returns>当前的日志输出级别设置值</returns>
    public static LogLevel GetLoggingLevel()
        => CubismContext.Default.LogLevel;

    public static IntPtr Allocate(int size)
        => CubismContext.Default.Allocator.Allocate(size);
    public static IntPtr AllocateAligned(int size, int alignment)
        => CubismContext.Default.Allocator.AllocateAligned(size, alignment);
    public static void Deallocate(IntPtr address)
        => CubismContext.Default.Allocator.Deallocate(address);
    public static void DeallocateAligned(IntPtr address)
        => CubismContext.Default.Allocator.DeallocateAligned(address);
}
