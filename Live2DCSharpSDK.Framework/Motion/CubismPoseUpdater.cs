using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 姿势的更新处理 (官方 CubismPoseUpdater).
/// </summary>
public class CubismPoseUpdater : ICubismUpdater
{
    /// <summary>
    /// 姿势管理
    /// </summary>
    private readonly CubismPose _pose;

    /// <summary>
    /// 构造函数. 执行顺序为 CubismUpdateOrder.Pose.
    /// </summary>
    /// <param name="pose">姿势管理</param>
    public CubismPoseUpdater(CubismPose pose)
        : base((int)CubismUpdateOrder.Pose)
    {
        _pose = pose;
    }

    /// <summary>
    /// 构造函数.
    /// </summary>
    /// <param name="pose">姿势管理</param>
    /// <param name="executionOrder">执行顺序</param>
    public CubismPoseUpdater(CubismPose pose, int executionOrder)
        : base(executionOrder)
    {
        _pose = pose;
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

        _pose.UpdateParameters(model, deltaTimeSeconds);
    }
}
