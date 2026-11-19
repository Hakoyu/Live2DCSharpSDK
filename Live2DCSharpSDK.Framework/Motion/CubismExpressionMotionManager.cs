using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// パラメータに適用する表情の値を持たせる構造体
/// </summary>
public record ExpressionParameterValue
{
    /// <summary>
    /// パラメータID
    /// </summary>
    public string ParameterId;
    /// <summary>
    /// 加算値
    /// </summary>
    public float AdditiveValue;
    /// <summary>
    /// 乗算値
    /// </summary>
    public float MultiplyValue;
    /// <summary>
    /// 上書き値
    /// </summary>
    public float OverwriteValue;
};

public class CubismExpressionMotionManager : CubismMotionQueueManager
{
    // モデルに適用する各パラメータの値
    private readonly List<ExpressionParameterValue> _expressionParameterValues = [];
    // 再生中の表情のウェイト
    private readonly List<float> _fadeWeights = [];

    /// <summary>
    /// 表情モーションを更新して、モデルにパラメータ値を反映する。
    /// </summary>
    /// <param name="model">対象のモデル</param>
    /// <param name="deltaTimeSeconds"> デルタ時間[秒]</param>
    /// <returns>true    更新されている
    /// false   更新されていない</returns>
    public bool UpdateMotion(CubismModel model, float deltaTimeSeconds)
    {
        UserTimeSeconds += deltaTimeSeconds;
        bool updated = false;
        List<CubismMotionQueueEntry> motions = Motions;

        float expressionWeight = 0.0f;
        int expressionIndex = 0;

        // 再生中の表情数に合わせてフェードウェイトの領域を確保する（官方 5-r.5）
        while (_fadeWeights.Count < motions.Count)
        {
            _fadeWeights.Add(0.0f);
        }

        // ------- 処理を行う --------
        // 既にモーションがあれば終了フラグを立てる
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
                // 再生中のExpressionが参照しているパラメータをすべてリストアップ
                for (int i = 0; i < expressionParameters.Count; ++i)
                {
                    if (expressionParameters[i].ParameterId == null)
                    {
                        continue;
                    }

                    int index = -1;
                    // リストにパラメータIDが存在するか検索
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

                    // パラメータがリストに存在しないなら新規追加
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

            // ------ 値を計算する ------
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
                // フェードアウト開始
                item.StartFadeout(item.FadeOutSeconds, UserTimeSeconds);
            }

            ++expressionIndex;
        }

        // ----- 最新のExpressionのフェードが完了していればそれ以前を削除する ------
        if (motions.Count > 1)
        {
            float latestFadeWeight = GetFadeWeight(_fadeWeights.Count - 1);
            if (latestFadeWeight >= 1.0f)
            {
                // 配列の最後の要素は削除しない
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

        // モデルに各値を適用
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
    /// 現在の表情のフェードのウェイト値を取得する。
    /// </summary>
    /// <param name="index">取得する表情モーションのインデックス</param>
    /// <returns>表情のフェードのウェイト値</returns>
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
    /// 表情のフェードのウェイト値を設定する。
    /// </summary>
    /// <param name="index">設定する表情モーションのインデックス</param>
    /// <param name="expressionFadeWeight">表情のフェードのウェイト値</param>
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
