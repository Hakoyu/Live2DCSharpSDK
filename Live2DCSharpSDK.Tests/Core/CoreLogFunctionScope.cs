using Live2DCSharpSDK.Framework.Core;

namespace Live2DCSharpSDK.Tests.Core;

/// <summary>
/// 保存并恢复 Core 的进程级日志回调 (csmGetLogFunction / csmSetLogFunction).
///
/// <see cref="CubismContext.StartUp"/> 会把上下文配置里的日志函数注册到 Core, 这是进程级副作用;
/// 用例结束前恢复原回调, 避免影响同一进程里随后运行的其它测试.
/// 原本没有注册回调时回退到静默函数, 让注册表里始终是一个安全的托管委托.
/// </summary>
internal sealed class CoreLogFunctionScope : IDisposable
{
    /// <summary>
    /// 作用域结束后要恢复的日志函数.
    /// </summary>
    private readonly LogFunction _restoreFunction;

    /// <summary>
    /// 记录当前的 Core 日志回调.
    /// </summary>
    public CoreLogFunctionScope()
    {
        _restoreFunction = CubismCore.GetLogFunction() ?? CoreTestEnvironment.SilentLog;
    }

    /// <summary>
    /// 恢复进入作用域前的 Core 日志回调.
    /// </summary>
    public void Dispose()
    {
        CubismCore.SetLogFunction(_restoreFunction);
    }
}
