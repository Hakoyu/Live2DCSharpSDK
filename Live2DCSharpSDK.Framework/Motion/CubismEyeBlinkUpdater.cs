using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 自动眨眼的更新处理 (官方 CubismEyeBlinkUpdater).
/// </summary>
/// <remarks>
/// 官方持有 <c>const csmBool&amp; motionUpdated</c> 的引用,
/// C# 无法持有引用字段, 因此改用 <see cref="Func{TResult}"/> 每次获取
/// (调用方注入的委托).
/// </remarks>
public class CubismEyeBlinkUpdater : ICubismUpdater
{
    /// <summary>
    /// 返回动作是否更新了参数的函数
    /// </summary>
    private readonly Func<bool> _motionUpdated;

    /// <summary>
    /// 自动眨眼
    /// </summary>
    private readonly CubismEyeBlink _eyeBlink;

    /// <summary>
    /// 构造函数. 执行顺序为 CubismUpdateOrder.EyeBlink.
    /// </summary>
    /// <param name="motionUpdated">返回动作是否更新了参数的函数</param>
    /// <param name="eyeBlink">自动眨眼</param>
    public CubismEyeBlinkUpdater(Func<bool> motionUpdated, CubismEyeBlink eyeBlink)
        : base((int)CubismUpdateOrder.EyeBlink)
    {
        _motionUpdated = motionUpdated;
        _eyeBlink = eyeBlink;
    }

    /// <summary>
    /// 构造函数.
    /// </summary>
    /// <param name="motionUpdated">返回动作是否更新了参数的函数</param>
    /// <param name="eyeBlink">自动眨眼</param>
    /// <param name="executionOrder">执行顺序</param>
    public CubismEyeBlinkUpdater(
        Func<bool> motionUpdated,
        CubismEyeBlink eyeBlink,
        int executionOrder
    )
        : base(executionOrder)
    {
        _motionUpdated = motionUpdated;
        _eyeBlink = eyeBlink;
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

        if (!_motionUpdated())
        {
            // 主动作没有更新时进行眨眼
            _eyeBlink.UpdateParameters(model, deltaTimeSeconds);
        }
    }
}
