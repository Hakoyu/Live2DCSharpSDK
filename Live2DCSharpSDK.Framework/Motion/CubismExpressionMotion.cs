using System.Text.Json.Nodes;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 表情的动作类.
/// </summary>
public class CubismExpressionMotion : ACubismMotion
{
    /// <summary>
    /// 加算应用的初始值
    /// </summary>
    public const float DefaultAdditiveValue = 0.0f;

    /// <summary>
    /// 乘算应用的初始值
    /// </summary>
    public const float DefaultMultiplyValue = 1.0f;

    public const string ExpressionKeyFadeIn = "FadeInTime";
    public const string ExpressionKeyFadeOut = "FadeOutTime";
    public const string ExpressionKeyParameters = "Parameters";
    public const string ExpressionKeyId = "Id";
    public const string ExpressionKeyValue = "Value";
    public const string ExpressionKeyBlend = "Blend";
    public const string BlendValueAdd = "Add";
    public const string BlendValueMultiply = "Multiply";
    public const string BlendValueOverwrite = "Overwrite";
    public const float DefaultFadeTime = 1.0f;

    /// <summary>
    /// 表情的参数信息列表
    /// </summary>
    public List<ExpressionParameter> Parameters { get; init; } = [];

    /// <summary>
    /// 所属的上下文实例, 用于解析表情中的参数 ID.
    /// </summary>
    private readonly CubismContext _context;

    /// <summary>
    /// 创建实例.
    /// </summary>
    /// <param name="buffer">已读取 exp 文件的缓冲区</param>
    /// <param name="context">上下文实例</param>
    /// <exception cref="ArgumentNullException">context 为 null 时抛出</exception>
    public CubismExpressionMotion(string buf, CubismContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(
                nameof(context),
                "CubismExpressionMotion requires a context instance."
            );
        }

        _context = context;

        using var stream = File.Open(buf, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var obj = JsonNode.Parse(stream) ?? throw new Exception("Load ExpressionMotion error");
        var json = obj.AsObject();

        FadeInSeconds = json.ContainsKey(ExpressionKeyFadeIn)
            ? (float)json[ExpressionKeyFadeIn]!
            : DefaultFadeTime; // 淡入
        FadeOutSeconds = json.ContainsKey(ExpressionKeyFadeOut)
            ? (float)json[ExpressionKeyFadeOut]!
            : DefaultFadeTime; // 淡出

        if (FadeInSeconds < 0.0f)
        {
            FadeInSeconds = DefaultFadeTime;
        }

        if (FadeOutSeconds < 0.0f)
        {
            FadeOutSeconds = DefaultFadeTime;
        }

        // 针对各参数
        var list = json[ExpressionKeyParameters]!;
        int parameterCount = list.AsArray().Count;

        for (int i = 0; i < parameterCount; ++i)
        {
            var param = list[i]!;
            var parameterId = _context.IdManager.GetId(param[ExpressionKeyId]!.ToString()); // 参数 ID
            var value = (float)param[ExpressionKeyValue]!; // 值

            // 计算方法的设置
            ExpressionBlendType blendType;
            var type = param[ExpressionKeyBlend]?.ToString();
            if (type == null || type == BlendValueAdd)
            {
                blendType = ExpressionBlendType.Add;
            }
            else if (type == BlendValueMultiply)
            {
                blendType = ExpressionBlendType.Multiply;
            }
            else if (type == BlendValueOverwrite)
            {
                blendType = ExpressionBlendType.Overwrite;
            }
            else
            {
                // 其他情况: 设置了规范外的值时改为加算模式以恢复
                blendType = ExpressionBlendType.Add;
            }

            // 创建设置对象并添加到列表
            Parameters.Add(
                new()
                {
                    ParameterId = parameterId,
                    BlendType = blendType,
                    Value = value,
                }
            );
        }
    }

    public override void DoUpdateParameters(
        CubismModel model,
        float userTimeSeconds,
        float weight,
        CubismMotionQueueEntry motionQueueEntry
    )
    {
        foreach (var item in Parameters)
        {
            switch (item.BlendType)
            {
                case ExpressionBlendType.Add:
                {
                    model.AddParameterValue(item.ParameterId, item.Value, weight); // 相对变化 加算
                    break;
                }
                case ExpressionBlendType.Multiply:
                {
                    model.MultiplyParameterValue(item.ParameterId, item.Value, weight); // 相对变化 乘算
                    break;
                }
                case ExpressionBlendType.Overwrite:
                {
                    model.SetParameterValue(item.ParameterId, item.Value, weight); // 绝对变化 覆盖
                    break;
                }
                default:
                    // 设置了规范外的值时已经处于加算模式
                    break;
            }
        }
    }

    /// <summary>
    /// 计算模型的表情相关参数.
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="userTimeSeconds">目标模型</param>
    /// <param name="motionQueueEntry">CubismMotionQueueManager 中管理的动作</param>
    /// <param name="expressionParameterValues">应用到模型的各参数的值</param>
    /// <param name="expressionIndex">表情的索引</param>
    public void CalculateExpressionParameters(
        CubismModel model,
        float userTimeSeconds,
        CubismMotionQueueEntry? motionQueueEntry,
        List<ExpressionParameterValue>? expressionParameterValues,
        int expressionIndex,
        float fadeWeight
    )
    {
        if (motionQueueEntry == null || expressionParameterValues == null)
        {
            return;
        }

        if (!motionQueueEntry.Available)
        {
            return;
        }

        // 为更新状态计算渐变权重 (值使用参数 fadeWeight).
        UpdateFadeWeight(motionQueueEntry, userTimeSeconds);

        // 计算应用到模型的值
        for (int i = 0; i < expressionParameterValues.Count; ++i)
        {
            ExpressionParameterValue expressionParameterValue = expressionParameterValues[i];

            if (expressionParameterValue.ParameterId == null)
            {
                continue;
            }

            float currentParameterValue = expressionParameterValue.OverwriteValue =
                model.GetParameterValue(expressionParameterValue.ParameterId);

            var expressionParameters = Parameters;
            int parameterIndex = -1;
            for (int j = 0; j < expressionParameters.Count; ++j)
            {
                if (expressionParameterValue.ParameterId != expressionParameters[j].ParameterId)
                {
                    continue;
                }

                parameterIndex = j;

                break;
            }

            // 播放中的 Expression 未引用的参数应用初始值
            if (parameterIndex < 0)
            {
                if (expressionIndex == 0)
                {
                    expressionParameterValues[i].AdditiveValue = DefaultAdditiveValue;

                    expressionParameterValues[i].MultiplyValue = DefaultMultiplyValue;

                    expressionParameterValues[i].OverwriteValue = currentParameterValue;
                }
                else
                {
                    expressionParameterValues[i].AdditiveValue = CalculateValue(
                        expressionParameterValue.AdditiveValue,
                        DefaultAdditiveValue,
                        fadeWeight
                    );

                    expressionParameterValues[i].MultiplyValue = CalculateValue(
                        expressionParameterValue.MultiplyValue,
                        DefaultMultiplyValue,
                        fadeWeight
                    );

                    expressionParameterValues[i].OverwriteValue = CalculateValue(
                        expressionParameterValue.OverwriteValue,
                        currentParameterValue,
                        fadeWeight
                    );
                }
                continue;
            }

            // 计算值
            float value = expressionParameters[parameterIndex].Value;
            float newAdditiveValue,
                newMultiplyValue,
                newSetValue;
            switch (expressionParameters[parameterIndex].BlendType)
            {
                case ExpressionBlendType.Add:
                    newAdditiveValue = value;
                    newMultiplyValue = DefaultMultiplyValue;
                    newSetValue = currentParameterValue;
                    break;
                case ExpressionBlendType.Multiply:
                    newAdditiveValue = DefaultAdditiveValue;
                    newMultiplyValue = value;
                    newSetValue = currentParameterValue;
                    break;
                case ExpressionBlendType.Overwrite:
                    newAdditiveValue = DefaultAdditiveValue;
                    newMultiplyValue = DefaultMultiplyValue;
                    newSetValue = value;
                    break;
                default:
                    return;
            }

            if (expressionIndex == 0)
            {
                expressionParameterValues[i].AdditiveValue = newAdditiveValue;
                expressionParameterValues[i].MultiplyValue = newMultiplyValue;
                expressionParameterValues[i].OverwriteValue = newSetValue;
            }
            else
            {
                expressionParameterValues[i].AdditiveValue =
                    (expressionParameterValue.AdditiveValue * (1.0f - fadeWeight))
                    + newAdditiveValue * fadeWeight;
                expressionParameterValues[i].MultiplyValue =
                    (expressionParameterValue.MultiplyValue * (1.0f - fadeWeight))
                    + newMultiplyValue * fadeWeight;
                expressionParameterValues[i].OverwriteValue =
                    (expressionParameterValue.OverwriteValue * (1.0f - fadeWeight))
                    + newSetValue * fadeWeight;
            }
        }
    }

    private float CalculateValue(float source, float destination, float fadeWeight)
    {
        return (source * (1.0f - fadeWeight)) + (destination * fadeWeight);
    }
}
