using Live2DCSharpSDK.Framework;

namespace Live2DCSharpSDK.App;

/// <summary>
/// 应用程序类.
/// 负责 Cubism SDK 的管理.
/// </summary>
public static class LAppDefine
{
    // 视图
    public const float ViewScale = 1.0f;
    public const float ViewMaxScale = 2.0f;
    public const float ViewMinScale = 0.8f;

    public const float ViewLogicalLeft = -1.0f;
    public const float ViewLogicalRight = 1.0f;
    public const float ViewLogicalBottom = -1.0f;
    public const float ViewLogicalTop = -1.0f;

    public const float ViewLogicalMaxLeft = -2.0f;
    public const float ViewLogicalMaxRight = 2.0f;
    public const float ViewLogicalMaxBottom = -2.0f;
    public const float ViewLogicalMaxTop = 2.0f;

    // 与外部定义文件(json)保持一致
    public const string MotionGroupIdle = "Idle"; // 待机
    public const string MotionGroupTapBody = "TapBody"; // 点击身体时

    // 与外部定义文件(json)保持一致
    public const string HitAreaNameHead = "Head";
    public const string HitAreaNameBody = "Body";

    // MOC3 一致性校验选项
    public const bool MocConsistencyValidationEnable = true;

    // motion3.json 一致性校验选项(与官方 5-r.5 的 LAppDefine.cpp 使用相同默认值)
    public const bool MotionConsistencyValidationEnable = true;

    // 调试日志输出选项(官方 LAppDefine.cpp:64-66)
    public const bool DebugLogEnable = true;
    public const bool DebugTouchLogEnable = false;

    // Framework 输出日志的级别设置
    public const LogLevel CubismLoggingLevel = LogLevel.Verbose;

    // 默认渲染目标尺寸(官方 LAppDefine.cpp:71-72)
    // 注: 本移植在示例层未启用 USE_RENDER_TARGET, 仅保持取值与官方一致.
    public const int RenderTargetWidth = 1900;
    public const int RenderTargetHeight = 1000;
}
