using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 呼吸的更新处理 (官方 CubismBreathUpdater).
/// </summary>
public class CubismBreathUpdater : ICubismUpdater
{
    /// <summary>
    /// 呼吸
    /// </summary>
    private readonly CubismBreath _breath;

    /// <summary>
    /// 构造函数. 执行顺序为 CubismUpdateOrder.Breath.
    /// </summary>
    /// <param name="breath">呼吸</param>
    public CubismBreathUpdater(CubismBreath breath)
        : base((int)CubismUpdateOrder.Breath)
    {
        _breath = breath;
    }

    /// <summary>
    /// 构造函数.
    /// </summary>
    /// <param name="breath">呼吸</param>
    /// <param name="executionOrder">执行顺序</param>
    public CubismBreathUpdater(CubismBreath breath, int executionOrder)
        : base(executionOrder)
    {
        _breath = breath;
    }

    /// <summary>
    /// 更新处理.
    /// </summary>
    /// <param name="model">更新目标的模型</param>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    public override void OnLateUpdate(CubismModel model, float deltaTimeSeconds)
    {
        if (model == null)
        {
            return;
        }

        _breath.UpdateParameters(model, deltaTimeSeconds);
    }
}
