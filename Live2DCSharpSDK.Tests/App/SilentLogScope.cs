using Live2DCSharpSDK.Framework;

namespace Live2DCSharpSDK.Tests.App;

/// <summary>
/// 在作用域内把框架日志级别固定为 <see cref="LogLevel.Off"/>, 离开作用域时恢复.
/// </summary>
/// <remarks>
/// A 档用例不得加载 Core: 框架输出日志时会回退到 Core 注册的日志回调 (csmGetLogFunction),
/// 因此先关掉日志, 避免因为别处改过日志级别而意外触发 P/Invoke.
/// </remarks>
internal sealed class SilentLogScope : IDisposable
{
    /// <summary>
    /// 进入作用域前的日志级别.
    /// </summary>
    private readonly LogLevel _previousLevel;

    /// <summary>
    /// 关闭日志并记录原级别.
    /// </summary>
    public SilentLogScope()
    {
        _previousLevel = CubismLog.LogLevel;
        CubismLog.LogLevel = LogLevel.Off;
    }

    /// <summary>
    /// 恢复进入作用域前的日志级别.
    /// </summary>
    public void Dispose()
    {
        CubismLog.LogLevel = _previousLevel;
    }
}
