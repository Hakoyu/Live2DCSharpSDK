using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// ドラッグ（顔の向き）の更新処理（官方 CubismLookUpdater）。
/// </summary>
public class CubismLookUpdater : ICubismUpdater
{
    /// <summary>
    /// look に紐づくパラメータ情報
    /// </summary>
    private readonly CubismLook _look;

    /// <summary>
    /// ドラッグ入力の管理
    /// </summary>
    private readonly CubismTargetPoint _dragManager;

    /// <summary>
    /// コンストラクタ。実行順序は CubismUpdateOrder.Look。
    /// </summary>
    /// <param name="look">look に紐づくパラメータ情報</param>
    /// <param name="dragManager">ドラッグ入力の管理</param>
    public CubismLookUpdater(CubismLook look, CubismTargetPoint dragManager)
        : base((int)CubismUpdateOrder.Look)
    {
        _look = look;
        _dragManager = dragManager;
    }

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="look">look に紐づくパラメータ情報</param>
    /// <param name="dragManager">ドラッグ入力の管理</param>
    /// <param name="executionOrder">実行順序</param>
    public CubismLookUpdater(CubismLook look, CubismTargetPoint dragManager, int executionOrder)
        : base(executionOrder)
    {
        _look = look;
        _dragManager = dragManager;
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

        _dragManager.Update(deltaTimeSeconds);

        // 本地的 CubismTargetPoint 提供 FaceX/FaceY 属性而非官方的 GetX()/GetY()(不改动 Math 层).
        float dragX = _dragManager.FaceX;
        float dragY = _dragManager.FaceY;

        _look.UpdateParameters(model, dragX, dragY);
    }
}
