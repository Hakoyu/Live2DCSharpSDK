using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Tests.Context;

namespace Live2DCSharpSDK.Tests.App;

/// <summary>
/// 在作用域内捕获框架日志, 离开作用域时恢复日志级别与输出函数.
/// 进入作用域时把 CubismContext.Current 置空, 让日志落到 CubismContext.Default,
/// 避免被其它用例残留的当前上下文接收.
/// </summary>
internal sealed class LogCaptureScope : IDisposable
{
    /// <summary>
    /// 进入作用域前的当前上下文.
    /// </summary>
    private readonly CubismContext? _previousCurrent;

    /// <summary>
    /// 进入作用域前的日志级别.
    /// </summary>
    private readonly LogLevel _previousLevel;

    /// <summary>
    /// 进入作用域前的日志输出函数.
    /// </summary>
    private readonly LogFunction? _previousOutput;

    /// <summary>
    /// 开始捕获日志.
    /// </summary>
    /// <param name="level">捕获级别下限, 低于该级别的日志被过滤</param>
    public LogCaptureScope(LogLevel level = LogLevel.Warning)
    {
        _previousCurrent = CubismContextCurrentHook.GetCurrent();
        CubismContextCurrentHook.SetCurrent(null);
        _previousLevel = CubismContext.Default.LogLevel;
        _previousOutput = CubismContext.Default.LogOutput;
        CubismContext.Default.LogLevel = level;
        CubismContext.Default.LogOutput = Messages.Add;
    }

    /// <summary>
    /// 捕获到的日志消息, 格式与 CubismLog 输出一致.
    /// </summary>
    public List<string> Messages { get; } = [];

    /// <summary>
    /// 恢复日志级别, 输出函数与当前上下文.
    /// </summary>
    public void Dispose()
    {
        CubismContext.Default.LogOutput = _previousOutput;
        CubismContext.Default.LogLevel = _previousLevel;
        CubismContextCurrentHook.SetCurrent(_previousCurrent);
    }
}
