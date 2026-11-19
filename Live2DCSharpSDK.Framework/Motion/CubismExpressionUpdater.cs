using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 表情的更新处理 (官方 CubismExpressionUpdater).
/// </summary>
public class CubismExpressionUpdater : ICubismUpdater
{
    /// <summary>
    /// 表情管理
    /// </summary>
    private readonly CubismExpressionMotionManager _expressionManager;

    /// <summary>
    /// 构造函数. 执行顺序为 CubismUpdateOrder.Expression.
    /// </summary>
    /// <param name="expressionManager">表情管理</param>
    public CubismExpressionUpdater(CubismExpressionMotionManager expressionManager)
        : base((int)CubismUpdateOrder.Expression)
    {
        _expressionManager = expressionManager;
    }

    /// <summary>
    /// 构造函数.
    /// </summary>
    /// <param name="expressionManager">表情管理</param>
    /// <param name="executionOrder">执行顺序</param>
    public CubismExpressionUpdater(CubismExpressionMotionManager expressionManager, int executionOrder)
        : base(executionOrder)
    {
        _expressionManager = expressionManager;
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

        // 按表情更新参数 (相对变化)
        _expressionManager.UpdateMotion(model, deltaTimeSeconds);
    }
}
