using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 自動まばたきの更新処理（官方 CubismEyeBlinkUpdater）。
/// </summary>
/// <remarks>
/// 官方持有 <c>const csmBool&amp; motionUpdated</c> 的引用,
/// C# 无法持有引用字段, 因此改用 <see cref="Func{TResult}"/> 每次获取
/// (调用方注入的委托).
/// </remarks>
public class CubismEyeBlinkUpdater : ICubismUpdater
{
    /// <summary>
    /// モーションによるパラメータ更新の有無を返す関数
    /// </summary>
    private readonly Func<bool> _motionUpdated;

    /// <summary>
    /// 自動まばたき
    /// </summary>
    private readonly CubismEyeBlink _eyeBlink;

    /// <summary>
    /// コンストラクタ。実行順序は CubismUpdateOrder.EyeBlink。
    /// </summary>
    /// <param name="motionUpdated">モーションによるパラメータ更新の有無を返す関数</param>
    /// <param name="eyeBlink">自動まばたき</param>
    public CubismEyeBlinkUpdater(Func<bool> motionUpdated, CubismEyeBlink eyeBlink)
        : base((int)CubismUpdateOrder.EyeBlink)
    {
        _motionUpdated = motionUpdated;
        _eyeBlink = eyeBlink;
    }

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="motionUpdated">モーションによるパラメータ更新の有無を返す関数</param>
    /// <param name="eyeBlink">自動まばたき</param>
    /// <param name="executionOrder">実行順序</param>
    public CubismEyeBlinkUpdater(Func<bool> motionUpdated, CubismEyeBlink eyeBlink, int executionOrder)
        : base(executionOrder)
    {
        _motionUpdated = motionUpdated;
        _eyeBlink = eyeBlink;
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

        if (!_motionUpdated())
        {
            // メインモーションの更新がないときは目パチ
            _eyeBlink.UpdateParameters(model, deltaTimeSeconds);
        }
    }
}
