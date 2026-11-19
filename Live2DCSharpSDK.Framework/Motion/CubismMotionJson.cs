namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// motion3.json 的一致性校验 (官方 CubismMotionJson::HasConsistency).
/// </summary>
/// <remarks>
/// 官方遍历原始 JSON, 本地则遍历 <see cref="CubismMotionObj"/> 的 DTO,
/// 核对 Curves / Segments / Points 的实际数值与 Meta 的声明值.
/// </remarks>
public static class CubismMotionJson
{
    /// <summary>
    /// 校验 motion3.json 中各曲线, 段, 点的数量是否与 Meta 的声明值一致.
    /// </summary>
    /// <param name="motionObj">已读取的 motion3.json 的 DTO</param>
    /// <returns>若一致则为 true</returns>
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

        // 计数处理
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

                // 段的类型 (CubismMotionSegmentType)
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
                        // 官方为 CSM_ASSERT(0). C# 侧作为非法数据处理.
                        CubismLog.Warning("[Live2D SDK]Unknown segment type in motion3.json.");
                        return false;
                }

                ++actualTotalSegmentCount;
            }
        }

        // 数量检查
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
