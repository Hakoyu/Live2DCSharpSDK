namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// motion3.json の整合性検証（官方 CubismMotionJson::HasConsistency）。
/// </summary>
/// <remarks>
/// 官方は生 JSON を走査するが、本地は <see cref="CubismMotionObj"/> の DTO を走査して
/// 核对 Curves / Segments / Points 的实际数值与 Meta 的声明值.
/// </remarks>
public static class CubismMotionJson
{
    /// <summary>
    /// motion3.json の各カーブ・セグメント・ポイント数が Meta の宣言値と一致するか検証する。
    /// </summary>
    /// <param name="motionObj">読み込んだ motion3.json の DTO</param>
    /// <returns>整合性が取れていれば true</returns>
    public static bool HasConsistency(CubismMotionObj motionObj)
    {
        if (motionObj?.Meta == null || motionObj.Curves == null)
        {
            CubismLog.Warning("[Live2D SDK]The motion3.json is invalid.");
            return false;
        }

        bool result = true;

        int actualCurveListSize = motionObj.Curves.Count;
        int actualTotalSegmentCount = 0;
        int actualTotalPointCount = 0;

        // カウント処理
        for (int curvePosition = 0; curvePosition < actualCurveListSize; ++curvePosition)
        {
            var segments = motionObj.Curves[curvePosition]?.Segments;

            if (segments == null)
            {
                CubismLog.Warning("[Live2D SDK]The curve does not have segments.");
                return false;
            }

            for (int segmentPosition = 0; segmentPosition < segments.Count;)
            {
                if (segmentPosition == 0)
                {
                    actualTotalPointCount += 1;
                    segmentPosition += 2;
                }

                // セグメントの種類（CubismMotionSegmentType）
                int segment = (int)segments[segmentPosition];

                switch (segment)
                {
                    case (int)CubismMotionSegmentType.Linear:
                        actualTotalPointCount += 1;
                        segmentPosition += 3;
                        break;
                    case (int)CubismMotionSegmentType.Bezier:
                        actualTotalPointCount += 3;
                        segmentPosition += 7;
                        break;
                    case (int)CubismMotionSegmentType.Stepped:
                        actualTotalPointCount += 1;
                        segmentPosition += 3;
                        break;
                    case (int)CubismMotionSegmentType.InverseStepped:
                        actualTotalPointCount += 1;
                        segmentPosition += 3;
                        break;
                    default:
                        // 官方は CSM_ASSERT(0)。C# 側は不正データとして扱う。
                        CubismLog.Warning("[Live2D SDK]Unknown segment type in motion3.json.");
                        return false;
                }

                ++actualTotalSegmentCount;
            }
        }

        // 個数チェック
        if (actualCurveListSize != motionObj.Meta.CurveCount)
        {
            CubismLog.Warning("[Live2D SDK]The number of curves does not match the metadata.");
            result = false;
        }

        if (actualTotalSegmentCount != motionObj.Meta.TotalSegmentCount)
        {
            CubismLog.Warning("[Live2D SDK]The number of segment does not match the metadata.");
            result = false;
        }

        if (actualTotalPointCount != motionObj.Meta.TotalPointCount)
        {
            CubismLog.Warning("[Live2D SDK]The number of point does not match the metadata.");
            result = false;
        }

        return result;
    }
}
