namespace Live2DCSharpSDK.Framework.Effect;

/// <summary>
/// 呼吸的参数信息.
/// </summary>
public record BreathParameterData
{
    /// <summary>
    /// 与呼吸绑定的参数 ID
    /// </summary>
    public required string ParameterId { get; set; }

    /// <summary>
    /// 将呼吸视为正弦波时, 波的偏移量
    /// </summary>
    public float Offset { get; set; }

    /// <summary>
    /// 将呼吸视为正弦波时, 波的振幅
    /// </summary>
    public float Peak { get; set; }

    /// <summary>
    /// 将呼吸视为正弦波时, 波的周期
    /// </summary>
    public float Cycle { get; set; }

    /// <summary>
    /// 参数权重
    /// </summary>
    public float Weight { get; set; }
}
