namespace Live2DCSharpSDK.App;

/// <summary>
/// 抽象化平台相关功能的 Cubism Platform Abstraction Layer.
/// 汇总文件读取与时间获取等依赖平台的函数.
/// </summary>
public static class LAppPal
{
    /// <summary>
    /// 获取增量时间(与上一帧的差值).
    /// </summary>
    /// <returns>增量时间[ms]</returns>
    public static float DeltaTime { get; set; }
}
