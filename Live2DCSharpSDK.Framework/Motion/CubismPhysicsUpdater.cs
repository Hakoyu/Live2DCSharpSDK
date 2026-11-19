using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Physics;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 物理运算的更新处理 (官方 CubismPhysicsUpdater).
/// </summary>
public class CubismPhysicsUpdater : ICubismUpdater
{
    /// <summary>
    /// 物理运算
    /// </summary>
    private readonly CubismPhysics _physics;

    /// <summary>
    /// 构造函数. 执行顺序为 CubismUpdateOrder.Physics.
    /// </summary>
    /// <param name="physics">物理运算</param>
    public CubismPhysicsUpdater(CubismPhysics physics)
        : base((int)CubismUpdateOrder.Physics)
    {
        _physics = physics;
    }

    /// <summary>
    /// 构造函数.
    /// </summary>
    /// <param name="physics">物理运算</param>
    /// <param name="executionOrder">执行顺序</param>
    public CubismPhysicsUpdater(CubismPhysics physics, int executionOrder)
        : base(executionOrder)
    {
        _physics = physics;
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

        _physics.Evaluate(model, deltaTimeSeconds);
    }
}
