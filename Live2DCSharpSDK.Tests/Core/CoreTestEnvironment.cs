using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Core;

namespace Live2DCSharpSDK.Tests.Core;

/// <summary>
/// B 档用例共用的上下文工厂与日志设置.
/// 默认关闭日志输出, 避免 Core 日志回调在测试输出里产生噪声.
/// </summary>
internal static class CoreTestEnvironment
{
    /// <summary>
    /// 静默日志函数. 用静态字段持有, 保证委托在 Core 进程级注册期间不会被回收.
    /// </summary>
    public static readonly LogFunction SilentLog = static _ => { };

    /// <summary>
    /// 用指定分配器创建一个尚未 StartUp 的上下文.
    /// </summary>
    /// <param name="allocator">真实的非托管分配器</param>
    /// <returns>尚未启动的上下文实例</returns>
    public static CubismContext CreateContext(ICubismAllocator allocator)
    {
        ArgumentNullException.ThrowIfNull(allocator);

        CubismOption option = new()
        {
            LogFunction = SilentLog,
            LoggingLevel = LogLevel.Off
        };

        return CubismContext.Create(allocator, option);
    }
}
