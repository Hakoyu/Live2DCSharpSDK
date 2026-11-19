using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 呼吸の更新処理（官方 CubismBreathUpdater）。
/// </summary>
public class CubismBreathUpdater : ICubismUpdater
{
    /// <summary>
    /// 呼吸
    /// </summary>
    private readonly CubismBreath _breath;

    /// <summary>
    /// コンストラクタ。実行順序は CubismUpdateOrder.Breath。
    /// </summary>
    /// <param name="breath">呼吸</param>
    public CubismBreathUpdater(CubismBreath breath)
        : base((int)CubismUpdateOrder.Breath)
    {
        _breath = breath;
    }

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="breath">呼吸</param>
    /// <param name="executionOrder">実行順序</param>
    public CubismBreathUpdater(CubismBreath breath, int executionOrder)
        : base(executionOrder)
    {
        _breath = breath;
    }

    /// <summary>
    /// 更新処理。
    /// </summary>
    /// <param name="model">更新対象のモデル</param>
    /// <param name="deltaTimeSeconds">デルタ時間[秒]</param>
    public override void OnLateUpdate(CubismModel model, float deltaTimeSeconds)
    {
        if (model == null)
        {
            return;
        }

        _breath.UpdateParameters(model, deltaTimeSeconds);
    }
}
