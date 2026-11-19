using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 表情の更新処理（官方 CubismExpressionUpdater）。
/// </summary>
public class CubismExpressionUpdater : ICubismUpdater
{
    /// <summary>
    /// 表情管理
    /// </summary>
    private readonly CubismExpressionMotionManager _expressionManager;

    /// <summary>
    /// コンストラクタ。実行順序は CubismUpdateOrder.Expression。
    /// </summary>
    /// <param name="expressionManager">表情管理</param>
    public CubismExpressionUpdater(CubismExpressionMotionManager expressionManager)
        : base((int)CubismUpdateOrder.Expression)
    {
        _expressionManager = expressionManager;
    }

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="expressionManager">表情管理</param>
    /// <param name="executionOrder">実行順序</param>
    public CubismExpressionUpdater(CubismExpressionMotionManager expressionManager, int executionOrder)
        : base(executionOrder)
    {
        _expressionManager = expressionManager;
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

        // 表情でパラメータ更新（相対変化）
        _expressionManager.UpdateMotion(model, deltaTimeSeconds);
    }
}
