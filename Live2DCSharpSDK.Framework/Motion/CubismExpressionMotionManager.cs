using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 保存应用到参数上的表情值的结构体
/// </summary>
public record ExpressionParameterValue
{
    /// <summary>
    /// 参数 ID
    /// </summary>
    public string ParameterId;
    /// <summary>
    /// 加算值
    /// </summary>
    public float AdditiveValue;
    /// <summary>
    /// 乘算值
    /// </summary>
    public float MultiplyValue;
    /// <summary>
    /// 覆盖值
    /// </summary>
    public float OverwriteValue;
};

public class CubismExpressionMotionManager : CubismMotionQueueManager
{
    // 应用到模型的各参数的值
    private readonly List<ExpressionParameterValue> _expressionParameterValues = [];
    // 播放中的表情的权重
    private readonly List<float> _fadeWeights = [];

    /// <summary>
    /// 更新表情动作, 并把参数值反映到模型.
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    /// <returns>true    已更新
    /// false   未更新</returns>
    public bool UpdateMotion(CubismModel model, float deltaTimeSeconds)
    {
        UserTimeSeconds += deltaTimeSeconds;
        bool updated = false;
        List<CubismMotionQueueEntry> motions = Motions;

        float expressionWeight = 0.0f;
        int expressionIndex = 0;

        // 按播放中的表情数量确保渐变权重的空间 (官方 5-r.5)
        while (_fadeWeights.Count < motions.Count)
        {
            _fadeWeights.Add(0.0f);
        }

        // ------- 执行处理 --------
        // 若已有动作则置结束标志
        var list = new List<CubismMotionQueueEntry>();
        foreach (var item in motions)
        {
            if (item.Motion is not CubismExpressionMotion expressionMotion)
            {
                list.Add(item);
                continue;
            }

            List<ExpressionParameter> expressionParameters = expressionMotion.Parameters;
            if (item.Available)
            {
                // 列出播放中的 Expression 所引用的全部参数
                for (int i = 0; i < expressionParameters.Count; ++i)
                {
                    if (expressionParameters[i].ParameterId == null)
                    {
                        continue;
                    }

                    int index = -1;
                    // 检索列表中是否存在该参数 ID
                    for (int j = 0; j < _expressionParameterValues.Count; ++j)
                    {
                        if (_expressionParameterValues[j].ParameterId != expressionParameters[i].ParameterId)
                        {
                            continue;
                        }

                        index = j;
                        break;
                    }

                    if (index >= 0)
                    {
                        continue;
                    }

                    // 若列表中不存在该参数则新增
                    ExpressionParameterValue item1 = new()
                    {
                        ParameterId = expressionParameters[i].ParameterId,
                        AdditiveValue = CubismExpressionMotion.DefaultAdditiveValue,
                        MultiplyValue = CubismExpressionMotion.DefaultMultiplyValue
                    };
                    item1.OverwriteValue = model.GetParameterValue(item1.ParameterId);
                    _expressionParameterValues.Add(item1);
                }
            }

            // ------ 计算值 ------
            expressionMotion.SetupMotionQueueEntry(item, UserTimeSeconds);
            SetFadeWeight(expressionIndex, expressionMotion.UpdateFadeWeight(item, UserTimeSeconds));
            expressionMotion.CalculateExpressionParameters(model, UserTimeSeconds,
                item, _expressionParameterValues, expressionIndex, GetFadeWeight(expressionIndex));

            expressionWeight += expressionMotion.FadeInSeconds == 0.0f
                ? 1.0f
                : CubismMath.GetEasingSine((UserTimeSeconds - item.FadeInStartTime) / expressionMotion.FadeInSeconds);

            updated = true;

            if (item.IsTriggeredFadeOut)
            {
                // 淡出开始
                item.StartFadeout(item.FadeOutSeconds, UserTimeSeconds);
            }

            ++expressionIndex;
        }

        // ----- 若最新 Expression 的渐变已完成, 则删除它之前的项 ------
        if (motions.Count > 1)
        {
            float latestFadeWeight = GetFadeWeight(_fadeWeights.Count - 1);
            if (latestFadeWeight >= 1.0f)
            {
                // 不删除数组的最后一个元素
                for (int i = motions.Count - 2; i >= 0; i--)
                {
                    motions.RemoveAt(i);
                    _fadeWeights.RemoveAt(i);
                }
            }
        }

        if (expressionWeight > 1.0f)
        {
            expressionWeight = 1.0f;
        }

        // 把各值应用到模型
        for (int i = 0; i < _expressionParameterValues.Count; ++i)
        {
            model.SetParameterValue(_expressionParameterValues[i].ParameterId,
                (_expressionParameterValues[i].OverwriteValue + _expressionParameterValues[i].AdditiveValue) * _expressionParameterValues[i].MultiplyValue,
                expressionWeight);

            _expressionParameterValues[i].AdditiveValue = CubismExpressionMotion.DefaultAdditiveValue;
            _expressionParameterValues[i].MultiplyValue = CubismExpressionMotion.DefaultMultiplyValue;
        }

        return updated;
    }

    /// <summary>
    /// 获取当前表情的渐变权重值.
    /// </summary>
    /// <param name="index">要获取的表情动作的索引</param>
    /// <returns>表情的渐变权重值</returns>
    public float GetFadeWeight(int index)
    {
        if (index < 0 || _fadeWeights.Count < 1 || _fadeWeights.Count <= index)
        {
            CubismLog.Warning("[Live2D SDK]Failed to get the fade weight value. The element at that index does not exist.");
            return -1;
        }

        return _fadeWeights[index];
    }

    /// <summary>
    /// 设置表情的渐变权重值.
    /// </summary>
    /// <param name="index">要设置的表情动作的索引</param>
    /// <param name="expressionFadeWeight">表情的渐变权重值</param>
    private void SetFadeWeight(int index, float expressionFadeWeight)
    {
        if (index < 0 || _fadeWeights.Count < 1 || _fadeWeights.Count <= index)
        {
            CubismLog.Warning("[Live2D SDK]Failed to set the fade weight value. The element at that index does not exist.");
            return;
        }

        _fadeWeights[index] = expressionFadeWeight;
    }
}
