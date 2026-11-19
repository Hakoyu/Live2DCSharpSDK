using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Physics;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 物理演算の更新処理（官方 CubismPhysicsUpdater）。
/// </summary>
public class CubismPhysicsUpdater : ICubismUpdater
{
    /// <summary>
    /// 物理演算
    /// </summary>
    private readonly CubismPhysics _physics;

    /// <summary>
    /// コンストラクタ。実行順序は CubismUpdateOrder.Physics。
    /// </summary>
    /// <param name="physics">物理演算</param>
    public CubismPhysicsUpdater(CubismPhysics physics)
        : base((int)CubismUpdateOrder.Physics)
    {
        _physics = physics;
    }

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="physics">物理演算</param>
    /// <param name="executionOrder">実行順序</param>
    public CubismPhysicsUpdater(CubismPhysics physics, int executionOrder)
        : base(executionOrder)
    {
        _physics = physics;
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

        _physics.Evaluate(model, deltaTimeSeconds);
    }
}
