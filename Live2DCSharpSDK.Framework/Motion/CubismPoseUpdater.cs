using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// ポーズの更新処理（官方 CubismPoseUpdater）。
/// </summary>
public class CubismPoseUpdater : ICubismUpdater
{
    /// <summary>
    /// ポーズ管理
    /// </summary>
    private readonly CubismPose _pose;

    /// <summary>
    /// コンストラクタ。実行順序は CubismUpdateOrder.Pose。
    /// </summary>
    /// <param name="pose">ポーズ管理</param>
    public CubismPoseUpdater(CubismPose pose)
        : base((int)CubismUpdateOrder.Pose)
    {
        _pose = pose;
    }

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="pose">ポーズ管理</param>
    /// <param name="executionOrder">実行順序</param>
    public CubismPoseUpdater(CubismPose pose, int executionOrder)
        : base(executionOrder)
    {
        _pose = pose;
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

        _pose.UpdateParameters(model, deltaTimeSeconds);
    }
}
