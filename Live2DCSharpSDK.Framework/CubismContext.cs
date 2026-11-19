using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Id;

namespace Live2DCSharpSDK.Framework;

/// <summary>
/// Cubism Framework 的上下文实例.
///
/// 每个实例持有自己的内存分配器, 配置, ID 管理器与日志级别, 因此同一进程内可以有多个
/// 互相独立的上下文. 创建后必须调用 <see cref="StartUp"/> 才能使用, 使用完毕调用
/// <see cref="Dispose"/> 释放; StartUp 与 Dispose 都是幂等的.
/// </summary>
public sealed class CubismContext : IDisposable
{
    /// <summary>
    /// 兼容门面 <see cref="CubismFramework"/> 使用的默认上下文.
    /// 过渡期(S1) 由静态门面转调, 静态门面删除(S4) 后不再使用.
    /// </summary>
    public static CubismContext Default { get; } = new();

    /// <summary>
    /// 当前执行上下文(ExecutionContext)关联的 CubismContext.
    /// 由 <see cref="StartUp"/> 设置为自身, 由 <see cref="Dispose"/> 恢复为启动前的值;
    /// 日志层用它实现上下文感知的日志级别与输出.
    /// </summary>
    public static CubismContext? Current => s_current.Value;

    private static readonly AsyncLocal<CubismContext?> s_current = new();

    /// <summary>
    /// ID 管理器.
    /// 在构造时创建(与旧静态属性立即初始化的语义一致), 每个上下文各持有一个实例.
    /// </summary>
    public CubismIdManager IdManager { get; } = new();

    /// <summary>
    /// 内存分配器, 由 <see cref="Create"/> 绑定.
    /// </summary>
    private ICubismAllocator? _allocator;

    /// <summary>
    /// 上下文配置, 由 <see cref="Create"/> 绑定.
    /// </summary>
    private CubismOption? _option;

    /// <summary>
    /// 当前上下文的日志输出级别.
    /// </summary>
    private LogLevel _logLevel = LogLevel.Off;

    /// <summary>
    /// 是否已启动(StartUp 成功后为 true, Dispose 后为 false).
    /// </summary>
    private bool _isStarted;

    /// <summary>
    /// 是否已 Dispose(防止二次释放的幂等保护).
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 显式设置的日志输出函数, 为 null 时回退到配置里的 <see cref="CubismOption.LogFunction"/>.
    /// </summary>
    private LogFunction? _logOutput;

    /// <summary>
    /// 本上下文成为当前上下文之前的上下文(用于 Dispose 时恢复).
    /// </summary>
    private CubismContext? _previousCurrent;

    /// <summary>
    /// <see cref="_previousCurrent"/> 是否有效.
    /// </summary>
    private bool _hasPreviousCurrent;

    /// <summary>
    /// 构造实例, 不做任何 Core 注册.
    /// </summary>
    private CubismContext()
    {
    }

    /// <summary>
    /// 使用指定的内存分配器与配置创建上下文.
    /// 创建过程不做任何 Core 注册(没有副作用), 日志回调与 Core 版本信息都在 <see cref="StartUp"/> 时才处理.
    /// </summary>
    /// <param name="allocator">ICubismAllocator 类的实例</param>
    /// <param name="option">CubismOption 类的实例</param>
    /// <returns>尚未启动的上下文实例</returns>
    /// <exception cref="ArgumentNullException">allocator 或 option 为 null 时抛出</exception>
    public static CubismContext Create(ICubismAllocator allocator, CubismOption option)
    {
        if (allocator is null)
        {
            throw new ArgumentNullException(nameof(allocator), "CubismContext requires an allocator instance.");
        }

        if (option is null)
        {
            throw new ArgumentNullException(nameof(option), "CubismContext requires an option instance.");
        }

        return new CubismContext
        {
            _allocator = allocator,
            _option = option,
            _logLevel = option.LoggingLevel
        };
    }

    /// <summary>
    /// 当前上下文的内存分配器.
    /// </summary>
    /// <exception cref="InvalidOperationException">上下文没有绑定分配器(未用 Create 创建)或已 Dispose 时抛出</exception>
    public ICubismAllocator Allocator
        => _allocator ?? throw new InvalidOperationException(
            "CubismContext has no allocator. Create the context with CubismContext.Create(allocator, option).");

    /// <summary>
    /// 当前上下文的配置.
    /// </summary>
    /// <exception cref="InvalidOperationException">上下文没有绑定配置(未用 Create 创建)或已 Dispose 时抛出</exception>
    public CubismOption Option
        => _option ?? throw new InvalidOperationException(
            "CubismContext has no option. Create the context with CubismContext.Create(allocator, option).");

    /// <summary>
    /// 当前上下文的日志输出级别.
    /// 初值取自 <see cref="CubismOption.LoggingLevel"/>, 之后可以直接改写.
    /// </summary>
    public LogLevel LogLevel
    {
        get => _logLevel;
        set => _logLevel = value;
    }

    /// <summary>
    /// 上下文是否已启动.
    /// </summary>
    public bool IsStarted => _isStarted;

    /// <summary>
    /// 当前上下文的日志输出函数.
    /// 未显式设置时回退到 <see cref="CubismOption.LogFunction"/>; 设置为 null 表示撤销显式设置.
    /// </summary>
    public LogFunction? LogOutput
    {
        get => _logOutput ?? _option?.LogFunction;
        set => _logOutput = value;
    }

    /// <summary>
    /// 启动上下文.
    /// 注册 Core 的日志回调并输出 Core 版本信息; ID 管理器已在构造时创建, 这里不再重复创建.
    /// 准备完成后再次调用会跳过内部处理(幂等).
    /// </summary>
    /// <exception cref="ObjectDisposedException">上下文已 Dispose 时抛出</exception>
    public void StartUp()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CubismContext));
        }

        // 先成为当前上下文, 启动过程中的日志才能按本上下文的级别与输出函数处理.
        AcquireCurrent();

        if (_isStarted)
        {
            CubismLog.Info("[Live2D SDK]CubismContext.StartUp() is already done.");
            return;
        }

        // Core 的日志函数是进程级注册, 每次 StartUp 都会覆盖成当前上下文的回调.
        if (_option is not null)
        {
            CubismCore.SetLogFunction(_option.LogFunction);
        }

        if (_allocator is null)
        {
            CubismLog.Warning("[Live2D SDK]CubismContext.StartUp() failed, need allocator instance.");
            return;
        }

        _isStarted = true;

        // 显示 Live2D Cubism Core 的版本信息
        var version = CubismCore.GetVersion();

        uint major = (version & 0xFF000000) >> 24;
        uint minor = (version & 0x00FF0000) >> 16;
        uint patch = version & 0x0000FFFF;
        uint versionNumber = version;

        CubismLog.Info($"[Live2D SDK]Cubism Core version: {major:#0}.{minor:0}.{patch:0000} ({versionNumber})");
        CubismLog.Info("[Live2D SDK]CubismContext.StartUp() is complete.");
    }

    /// <summary>
    /// 释放上下文.
    /// 幂等, 第二次及以后调用不做任何事.
    /// 当前上下文若仍是本实例, 恢复为启动前记录的上下文; 分配器由调用方持有, 这里只解除引用;
    /// Core 的日志函数是进程级注册, 不在这里撤销, 避免影响其它仍在运行的上下文.
    /// ID 管理器是纯托管数据, 随实例一起回收, 不需要显式清理.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _isStarted = false;
        RestoreCurrent();
        _allocator = null;
        _option = null;
        _logOutput = null;
        _logLevel = LogLevel.Off;
    }

    /// <summary>
    /// 绑定分配器与配置后启动上下文.
    /// 仅用于过渡期(S1) <see cref="CubismFramework"/> 静态门面的转调, 因此与旧门面一致地接受 null 参数.
    /// </summary>
    /// <param name="allocator">内存分配器, 允许为 null (启动失败, IsStarted 保持 false)</param>
    /// <param name="option">上下文配置, 允许为 null (不注册 Core 日志回调)</param>
    /// <returns>调用结束后 <see cref="IsStarted"/> 的值</returns>
    internal bool StartUp(ICubismAllocator? allocator, CubismOption? option)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CubismContext));
        }

        AcquireCurrent();

        if (_isStarted)
        {
            CubismLog.Info("[Live2D SDK]CubismFramework.StartUp() is already done.");
            return true;
        }

        _allocator = allocator;
        _option = option;
        _logLevel = option is null ? LogLevel.Off : option.LoggingLevel;

        StartUp();

        return _isStarted;
    }

    /// <summary>
    /// 清除启动状态, 保留分配器与配置, 之后可以再次 StartUp().
    /// 仅用于过渡期(S1) <see cref="CubismFramework.CleanUp"/> 的转调.
    /// </summary>
    internal void CleanUp()
    {
        if (_disposed)
        {
            return;
        }

        _isStarted = false;
    }

    /// <summary>
    /// 把本上下文设为当前上下文, 并记录(仅记录一次)恢复目标.
    /// AsyncLocal 的值属于当前执行上下文, 其它线程不受影响.
    /// </summary>
    private void AcquireCurrent()
    {
        CubismContext? previous = s_current.Value;
        if (ReferenceEquals(previous, this))
        {
            return;
        }

        if (_hasPreviousCurrent is false)
        {
            _previousCurrent = previous;
            _hasPreviousCurrent = true;
        }

        s_current.Value = this;
    }

    /// <summary>
    /// 若当前上下文仍是本实例, 恢复为启动前记录的上下文.
    /// Dispose 与 StartUp 可能在不同执行上下文里发生, 此处只做尽力恢复.
    /// </summary>
    private void RestoreCurrent()
    {
        if (ReferenceEquals(s_current.Value, this))
        {
            s_current.Value = _hasPreviousCurrent ? _previousCurrent : null;
        }

        _previousCurrent = null;
        _hasPreviousCurrent = false;
    }

    /// <summary>
    /// 通过 Core 当前注册的日志函数输出一条消息.
    /// Core 的日志函数注册是进程级的, 因此该方法与具体上下文的启动状态无关.
    /// </summary>
    /// <param name="data">日志消息</param>
    public static void InvokeCoreLogFunction(string data)
    {
        CubismCore.GetLogFunction()?.Invoke(data);
    }
}
