using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 拖拽 (面部朝向) 的更新处理 (官方 CubismLookUpdater).
/// </summary>
public class CubismLookUpdater : ICubismUpdater
{
    /// <summary>
    /// 与 look 绑定的参数信息
    /// </summary>
    private readonly CubismLook _look;

    /// <summary>
    /// 拖拽输入的管理
    /// </summary>
    private readonly CubismTargetPoint _dragManager;

    /// <summary>
    /// 构造函数. 执行顺序为 CubismUpdateOrder.Look.
    /// </summary>
    /// <param name="look">与 look 绑定的参数信息</param>
    /// <param name="dragManager">拖拽输入的管理</param>
    public CubismLookUpdater(CubismLook look, CubismTargetPoint dragManager)
        : base((int)CubismUpdateOrder.Look)
    {
        _look = look;
        _dragManager = dragManager;
    }

    /// <summary>
    /// 构造函数.
    /// </summary>
    /// <param name="look">与 look 绑定的参数信息</param>
    /// <param name="dragManager">拖拽输入的管理</param>
    /// <param name="executionOrder">执行顺序</param>
    public CubismLookUpdater(CubismLook look, CubismTargetPoint dragManager, int executionOrder)
        : base(executionOrder)
    {
        _look = look;
        _dragManager = dragManager;
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

        _dragManager.Update(deltaTimeSeconds);

        // 本地的 CubismTargetPoint 提供 FaceX/FaceY 属性而非官方的 GetX()/GetY()(不改动 Math 层).
        float dragX = _dragManager.FaceX;
        float dragY = _dragManager.FaceY;

        _look.UpdateParameters(model, dragX, dragY);
    }
}
