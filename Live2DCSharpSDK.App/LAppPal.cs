namespace Live2DCSharpSDK.App;

/// <summary>
/// 抽象化平台相关功能的 Cubism Platform Abstraction Layer.
/// 汇总文件读取与时间获取等依赖平台的函数.
/// </summary>
public static class LAppPal
{
    /// <summary>
    /// 获取增量时间(与上一帧的差值).
    /// 仅用于兼容旧调用点: 转发到最近一次完成初始化(<see cref="LAppDelegate.InitApp"/>)的 delegate 实例.
    /// 多个 delegate 并存时转发目标为最后创建的那个; 该实例释放后不再转发, 读取返回 0, 写入为空操作.
    /// 新代码请直接读取所属 delegate 的 <see cref="LAppDelegate.DeltaTime"/>.
    /// </summary>
    [Obsolete(
        "LAppPal.DeltaTime is deprecated. Use LAppDelegate.DeltaTime on the owning delegate instance instead."
    )]
    public static float DeltaTime
    {
        get => LAppDelegate.Latest?.DeltaTime ?? 0.0f;
        set => LAppDelegate.Latest?.SetDeltaTime(value);
    }
}
