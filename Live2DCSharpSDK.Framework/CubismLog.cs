using Live2DCSharpSDK.Framework.Core;

namespace Live2DCSharpSDK.Framework;

/// <summary>
/// Cubism Framework 的日志输出.
///
/// 静态方法签名保持不变. 日志级别与输出函数按当前执行上下文解析:
/// 有 <see cref="CubismContext.Current"/> 时使用该上下文的设置, 否则使用全局默认
/// (<see cref="CubismContext.Default"/>); 因此两个上下文各自设置级别时互不影响.
/// </summary>
public static class CubismLog
{
    /// <summary>
    /// 当前生效的日志输出级别.
    /// 取值时优先当前上下文, 没有当前上下文时取全局默认;
    /// 赋值时同样写入当前上下文, 没有当前上下文时写入全局默认.
    /// </summary>
    public static LogLevel LogLevel
    {
        get => CurrentLevel;
        set
        {
            if (CubismContext.Current is null)
            {
                CubismContext.Default.LogLevel = value;
                return;
            }

            CubismContext.Current.LogLevel = value;
        }
    }

    /// <summary>
    /// 设置当前生效的日志输出函数.
    /// 有当前上下文时写入该上下文, 否则写入全局默认; 传 null 表示撤销显式设置.
    /// </summary>
    /// <param name="logFunction">日志输出函数</param>
    public static void SetLogOutput(LogFunction? logFunction)
    {
        if (CubismContext.Current is null)
        {
            CubismContext.Default.LogOutput = logFunction;
            return;
        }

        CubismContext.Current.LogOutput = logFunction;
    }

    /// <summary>
    /// 当前生效的日志输出级别: 优先当前上下文, 否则全局默认.
    /// </summary>
    private static LogLevel CurrentLevel =>
        CubismContext.Current is null
            ? CubismContext.Default.LogLevel
            : CubismContext.Current.LogLevel;

    /// <summary>
    /// 当前生效的日志输出函数: 上下文设置优先, 未设置时回退到 Core 当前注册的回调.
    /// </summary>
    /// <returns>日志输出函数, 没有可用输出时返回 null</returns>
    private static LogFunction? ResolveLogOutput()
    {
        CubismContext? current = CubismContext.Current;
        CubismContext context = current is null ? CubismContext.Default : current;
        return context.LogOutput ?? CubismCore.GetLogFunction();
    }

    private static void CubismLogPrintln(
        LogLevel level,
        string head,
        string fmt,
        params object?[] args
    )
    {
        string data = $"[CSM] {head} {string.Format(fmt, args)}";
        if (level < CurrentLevel)
            return;

        ResolveLogOutput()?.Invoke(data);
    }

    public static void Verbose(string fmt, params object?[] args)
    {
        CubismLogPrintln(LogLevel.Verbose, "[V]", fmt, args);
    }

    public static void Debug(string fmt, params object?[] args)
    {
        CubismLogPrintln(LogLevel.Debug, "[D]", fmt, args);
    }

    public static void Info(string fmt, params object?[] args)
    {
        CubismLogPrintln(LogLevel.Info, "[I]", fmt, args);
    }

    public static void Warning(string fmt, params object?[] args)
    {
        CubismLogPrintln(LogLevel.Warning, "[W]", fmt, args);
    }

    public static void Error(string fmt, params object?[] args)
    {
        CubismLogPrintln(LogLevel.Error, "[E]", fmt, args);
        throw new Exception(string.Format(fmt, args));
    }
}
