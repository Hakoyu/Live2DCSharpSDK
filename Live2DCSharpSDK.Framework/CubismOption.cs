using Live2DCSharpSDK.Framework.Core;

namespace Live2DCSharpSDK.Framework;

/// <summary>
/// 日志输出级别
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// 详细日志
    /// </summary>
    Verbose = 0,
    /// <summary>
    /// 调试日志
    /// </summary>
    Debug,
    /// <summary>
    /// Info 日志
    /// </summary>
    Info,
    /// <summary>
    /// 警告日志
    /// </summary>
    Warning,
    /// <summary>
    /// 错误日志
    /// </summary>
    Error,
    /// <summary>
    /// 关闭日志输出
    /// </summary>
    Off
};

/// <summary>
/// 定义 CubismFramework 配置选项的类
/// </summary>
public class CubismOption
{
    /// <summary>
    /// 日志输出函数指针
    /// </summary>
    public required LogFunction LogFunction;
    /// <summary>
    /// 日志输出级别设置
    /// </summary>
    public LogLevel LoggingLevel;
}
