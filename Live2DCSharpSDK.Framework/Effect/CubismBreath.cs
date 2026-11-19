using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Effect;

/// <summary>
/// 提供呼吸功能.
/// </summary>
public class CubismBreath
{
    /// <summary>
    /// 呼吸效果的参数列表.
    /// </summary>
    public required List<BreathParameterData> Parameters { get; init; }

    /// <summary>
    /// 累计时间[秒]
    /// </summary>
    private float _currentTime;

    /// <summary>
    /// 更新模型参数.
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    public void UpdateParameters(CubismModel model, float deltaTimeSeconds)
    {
        _currentTime += deltaTimeSeconds;

        // 官方 5-r.2 中由 3.14159f 改为 CubismMath.Pi
        float t = _currentTime * 2.0f * CubismMath.Pi;

        foreach (var item in Parameters)
        {
            model.AddParameterValue(
                item.ParameterId,
                item.Offset + (item.Peak * MathF.Sin(t / item.Cycle)),
                item.Weight
            );
        }
    }
}
