using Live2DCSharpSDK.Framework;

namespace Live2DCSharpSDK.App;

/// <summary>
/// アプリケーションクラス。
/// Cubism SDK の管理を行う。
/// </summary>
public static class LAppDefine
{
    // 画面
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

    // 外部定義ファイル(json)と合わせる
    public const string MotionGroupIdle = "Idle"; // アイドリング
    public const string MotionGroupTapBody = "TapBody"; // 体をタップしたとき

    // 外部定義ファイル(json)と合わせる
    public const string HitAreaNameHead = "Head";
    public const string HitAreaNameBody = "Body";

    // MOC3の整合性検証オプション
    public const bool MocConsistencyValidationEnable = true;

    // motion3.jsonの整合性検証オプション（官方 5-r.5 の LAppDefine.cpp と同じ既定値）
    public const bool MotionConsistencyValidationEnable = true;

    // デバッグ用ログの表示オプション（官方 LAppDefine.cpp:64-66）
    public const bool DebugLogEnable = true;
    public const bool DebugTouchLogEnable = false;

    // Frameworkから出力するログのレベル設定
    public const LogLevel CubismLoggingLevel = LogLevel.Verbose;

    // デフォルトのレンダーターゲットサイズ（官方 LAppDefine.cpp:71-72）
    // 注: 本移植はサンプル層で USE_RENDER_TARGET を有効にしていない（PLAN D-05）。値だけ公式と揃えておく。
    public const int RenderTargetWidth = 1900;
    public const int RenderTargetHeight = 1000;
}
