using System.Text.Json;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 表示 motion3.json 一致性校验失败的异常 (本地特有).
/// 官方 CubismMotion::Create 返回 NULL, 但 C# 构造函数无法返回 null,
/// 因此用该类型通知失败. CubismUserModel.LoadMotion 只捕获该类型并转换为 null
/// (其他异常不吞掉).
/// </summary>
public sealed class CubismMotionConsistencyException : Exception
{
    public CubismMotionConsistencyException() : base("Inconsistent motion3.json.")
    {
    }

    public CubismMotionConsistencyException(string message) : base(message)
    {
    }
}

/// <summary>
/// 动作的类.
/// </summary>
public class CubismMotion : ACubismMotion
{
    public const string EffectNameEyeBlink = "EyeBlink";
    public const string EffectNameLipSync = "LipSync";
    public const string TargetNameModel = "Model";
    public const string TargetNameParameter = "Parameter";
    public const string TargetNamePartOpacity = "PartOpacity";

    // Id
    public const string IdNameOpacity = "Opacity";

    /// <summary>
    /// 已加载文件的 FPS. 若无描述则默认为 15fps
    /// </summary>
    private readonly float _sourceFrameRate;
    /// <summary>
    /// mtn 文件中定义的一连串动作的长度
    /// </summary>
    private readonly float _loopDurationSeconds;
    /// <summary>
    /// Motion Behavior 的版本 (官方 5-r.5 的 CubismMotion::MotionBehavior)
    /// </summary>
    public enum MotionBehavior
    {
        /// <summary>
        /// 旧有的动作行为 (官方 MotionBehavior_V1)
        /// </summary>
        V1,
        /// <summary>
        /// 能正确再现 Animator 动作的行为 (官方 MotionBehavior_V2)
        /// </summary>
        V2
    }

    /// <summary>
    /// Motion Behavior 的版本. 默认值为 V2.
    /// </summary>
    private MotionBehavior _motionBehavior;
    /// <summary>
    /// 最后设置的权重
    /// </summary>
    private float _lastWeight;

    /// <summary>
    /// 所属的上下文实例, 用于解析动作中的参数 ID.
    /// </summary>
    private readonly CubismContext _context;

    /// <summary>
    /// 实际的动作数据本体
    /// </summary>
    private readonly CubismMotionData _motionData;

    /// <summary>
    /// 应用自动眨眼的参数 ID 句柄列表.  将模型 (模型设置) 与参数对应起来.
    /// </summary>
    private List<string> _eyeBlinkParameterIds = [];
    /// <summary>
    /// 应用唇形同步的参数 ID 句柄列表.  将模型 (模型设置) 与参数对应起来.
    /// </summary>
    private List<string> _lipSyncParameterIds = [];

    /// <summary>
    /// 模型持有的自动眨眼用参数 ID 句柄.  将模型与动作对应起来.
    /// </summary>
    private string _modelCurveIdEyeBlink;
    /// <summary>
    /// 模型持有的唇形同步用参数 ID 句柄.  将模型与动作对应起来.
    /// </summary>
    private string _modelCurveIdLipSync;
    /// <summary>
    /// 模型持有的不透明度用参数 ID 句柄.  将模型与动作对应起来.
    /// </summary>
    private string _modelCurveIdOpacity;

    /// <summary>
    /// 从动作取得的不透明度
    /// </summary>
    private float _modelOpacity;

    /**
    * 若要再现 Cubism SDK R2 以前的动作则为 true, 要正确再现 Animator 的动作则为 false.
    */
    private readonly bool UseOldBeziersCurveMotion = false;

    private static CubismMotionPoint LerpPoints(CubismMotionPoint a, CubismMotionPoint b, float t)
    {
        return new()
        {
            Time = a.Time + ((b.Time - a.Time) * t),
            Value = a.Value + ((b.Value - a.Value) * t)
        };
    }

    private static float LinearEvaluate(CubismMotionPoint[] points, int start, float time)
    {
        float t = (time - points[start].Time) / (points[start + 1].Time - points[start].Time);

        if (t < 0.0f)
        {
            t = 0.0f;
        }

        return points[start].Value + ((points[start + 1].Value - points[start].Value) * t);
    }

    private static float BezierEvaluate(CubismMotionPoint[] points, int start, float time)
    {
        float t = (time - points[start].Time) / (points[start + 3].Time - points[start].Time);

        if (t < 0.0f)
        {
            t = 0.0f;
        }

        var p01 = LerpPoints(points[start], points[start + 1], t);
        var p12 = LerpPoints(points[start + 1], points[start + 2], t);
        var p23 = LerpPoints(points[start + 2], points[start + 3], t);

        var p012 = LerpPoints(p01, p12, t);
        var p123 = LerpPoints(p12, p23, t);

        return LerpPoints(p012, p123, t).Value;
    }

    //private static float BezierEvaluateBinarySearch(List<CubismMotionPoint> points, int start, float time)
    //{
    //    float x_error = 0.01f;

    //    float x = time;
    //    float x1 = points[0].Time;
    //    float x2 = points[3].Time;
    //    float cx1 = points[1].Time;
    //    float cx2 = points[2].Time;

    //    float ta = 0.0f;
    //    float tb = 1.0f;
    //    float t = 0.0f;
    //    int i = 0;
    //    for (; i < 20; ++i)
    //    {
    //        if (x < x1 + x_error)
    //        {
    //            t = ta;
    //            break;
    //        }

    //        if (x2 - x_error < x)
    //        {
    //            t = tb;
    //            break;
    //        }

    //        float centerx = (cx1 + cx2) * 0.5f;
    //        cx1 = (x1 + cx1) * 0.5f;
    //        cx2 = (x2 + cx2) * 0.5f;
    //        float ctrlx12 = (cx1 + centerx) * 0.5f;
    //        float ctrlx21 = (cx2 + centerx) * 0.5f;
    //        centerx = (ctrlx12 + ctrlx21) * 0.5f;
    //        if (x < centerx)
    //        {
    //            tb = (ta + tb) * 0.5f;
    //            if (centerx - x_error < x)
    //            {
    //                t = tb;
    //                break;
    //            }

    //            x2 = centerx;
    //            cx2 = ctrlx12;
    //        }
    //        else
    //        {
    //            ta = (ta + tb) * 0.5f;
    //            if (x < centerx + x_error)
    //            {
    //                t = ta;
    //                break;
    //            }

    //            x1 = centerx;
    //            cx1 = ctrlx21;
    //        }
    //    }

    //    if (i == 20)
    //    {
    //        t = (ta + tb) * 0.5f;
    //    }

    //    if (t < 0.0f)
    //    {
    //        t = 0.0f;
    //    }
    //    if (t > 1.0f)
    //    {
    //        t = 1.0f;
    //    }

    //    CubismMotionPoint p01 = LerpPoints(points[start], points[start + 1], t);
    //    CubismMotionPoint p12 = LerpPoints(points[start + 1], points[start + 2], t);
    //    CubismMotionPoint p23 = LerpPoints(points[start + 2], points[start + 3], t);

    //    CubismMotionPoint p012 = LerpPoints(p01, p12, t);
    //    CubismMotionPoint p123 = LerpPoints(p12, p23, t);

    //    return LerpPoints(p012, p123, t).Value;
    //}

    private static float BezierEvaluateCardanoInterpretation(CubismMotionPoint[] points, int start, float time)
    {
        float x = time;
        float x1 = points[start].Time;
        float x2 = points[start + 3].Time;
        float cx1 = points[start + 1].Time;
        float cx2 = points[start + 2].Time;

        float a = x2 - 3.0f * cx2 + 3.0f * cx1 - x1;
        float b = 3.0f * cx2 - 6.0f * cx1 + 3.0f * x1;
        float c = 3.0f * cx1 - 3.0f * x1;
        float d = x1 - x;

        float t = CubismMath.CardanoAlgorithmForBezier(a, b, c, d);

        var p01 = LerpPoints(points[start], points[start + 1], t);
        var p12 = LerpPoints(points[start + 1], points[start + 2], t);
        var p23 = LerpPoints(points[start + 2], points[start + 3], t);

        var p012 = LerpPoints(p01, p12, t);
        var p123 = LerpPoints(p12, p23, t);

        return LerpPoints(p012, p123, t).Value;
    }

    private static float SteppedEvaluate(CubismMotionPoint[] points, int start, float time)
    {
        return points[start].Value;
    }

    private static float InverseSteppedEvaluate(CubismMotionPoint[] points, int start, float time)
    {
        return points[start + 1].Value;
    }

    /// <summary>
    /// 从终点到起点的修正处理 (官方 5-r.5 的 CorrectEndPoint).
    /// </summary>
    /// <param name="motionData">动作数据</param>
    /// <param name="segmentIndex">段的索引</param>
    /// <param name="beginIndex">第一个点的索引</param>
    /// <param name="endIndex">最后一个点的索引</param>
    /// <param name="time">求值的时间[秒]</param>
    /// <param name="endTime">动作的结束时间[秒]</param>
    /// <returns>修正后的值</returns>
    private static float CorrectEndPoint(CubismMotionData motionData, int segmentIndex, int beginIndex, int endIndex,
        float time, float endTime)
    {
        var motionPoint = new CubismMotionPoint[2];
        motionPoint[0] = motionData.Points[endIndex];
        motionPoint[1] = motionData.Points[beginIndex];
        motionPoint[1].Time = endTime;

        switch (motionData.Segments[segmentIndex].SegmentType)
        {
            case CubismMotionSegmentType.Stepped:
                return SteppedEvaluate(motionPoint, 0, time);
            case CubismMotionSegmentType.InverseStepped:
                return InverseSteppedEvaluate(motionPoint, 0, time);
            case CubismMotionSegmentType.Linear:
            case CubismMotionSegmentType.Bezier:
            default:
                return LinearEvaluate(motionPoint, 0, time);
        }
    }

    private static float EvaluateCurve(CubismMotionData motionData, int index, float time, bool isCorrection,
        float endTime)
    {
        // 查找要求值的段.
        var curve = motionData.Curves[index];

        int target = -1;
        int totalSegmentCount = curve.BaseSegmentIndex + curve.SegmentCount;
        int pointPosition = 0;
        for (int i = curve.BaseSegmentIndex; i < totalSegmentCount; ++i)
        {
            // 获取下一段的第一个点.
            pointPosition = motionData.Segments[i].BasePointIndex
                + (motionData.Segments[i].SegmentType ==
                    CubismMotionSegmentType.Bezier ? 3 : 1);

            // 若时间落在当前段内则中断.
            if (motionData.Points[pointPosition].Time > time)
            {
                target = i;
                break;
            }
        }

        if (target == -1)
        {
            if (isCorrection && time < endTime)
            {
                // 从终点到起点的修正处理
                return CorrectEndPoint(motionData, totalSegmentCount - 1,
                    motionData.Segments[curve.BaseSegmentIndex].BasePointIndex, pointPosition, time, endTime);
            }

            return motionData.Points[pointPosition].Value;
        }

        var segment = motionData.Segments[target];

        return segment.Evaluate(motionData.Points, segment.BasePointIndex, time);
    }

    /// <summary>
    /// 创建实例.
    /// </summary>
    /// <param name="buffer">已读取 motion3.json 的缓冲区</param>
    /// <param name="context">上下文实例</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数. 为 NULL 时不调用.</param>
    /// <param name="onBeganMotionHandler">动作播放开始时调用的回调函数. 为 NULL 时不调用.</param>
    /// <param name="shouldCheckMotionConsistency">是否校验 motion3.json 的一致性</param>
    /// <remarks>
    /// 官方的 Create() 在一致性不成立时返回 NULL, 但 C# 的构造函数无法返回 null, 因此抛出异常.
    /// </remarks>
    /// <exception cref="ArgumentNullException">context 为 null 时抛出</exception>
    public CubismMotion(string buffer, CubismContext context, FinishedMotionCallback? onFinishedMotionHandler = null,
        BeganMotionCallback? onBeganMotionHandler = null, bool shouldCheckMotionConsistency = false)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context), "CubismMotion requires a context instance.");
        }

        _context = context;
        _sourceFrameRate = 30.0f;
        _loopDurationSeconds = -1.0f;
        _motionBehavior = MotionBehavior.V2;   // 官方 5-r.5 的默认值
        _modelOpacity = 1.0f;

        using var stream = File.Open(buffer, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var obj = JsonSerializer.Deserialize(stream, CubismMotionObjContext.Default.CubismMotionObj)
            ?? throw new Exception("Load Motion error");

        // 若无法确认一致性则不进行处理 (官方 CubismMotion::Parse)
        if (shouldCheckMotionConsistency && !CubismMotionJson.HasConsistency(obj))
        {
            // 官方 CubismMotion::Parse 不填充 _motionData 就返回, 由 Create 返回 NULL.
            // C# 构造函数无法返回 null, 因此用专用异常通知(其他异常不吞掉).
            throw new CubismMotionConsistencyException();
        }

        _motionData = new()
        {
            Duration = obj.Meta.Duration,
            Loop = obj.Meta.Loop,
            CurveCount = obj.Meta.CurveCount,
            Fps = obj.Meta.Fps,
            EventCount = obj.Meta.UserDataCount,
            Curves = new CubismMotionCurve[obj.Meta.CurveCount],
            Segments = new CubismMotionSegment[obj.Meta.TotalSegmentCount],
            Points = new CubismMotionPoint[obj.Meta.TotalPointCount],
            Events = new CubismMotionEvent[obj.Meta.UserDataCount]
        };

        bool areBeziersRestructed = obj.Meta.AreBeziersRestricted;

        if (obj.Meta.FadeInTime != null)
        {
            FadeInSeconds = obj.Meta.FadeInTime < 0.0f ? 1.0f : (float)obj.Meta.FadeInTime;
        }
        else
        {
            FadeInSeconds = 1.0f;
        }

        if (obj.Meta.FadeOutTime != null)
        {
            FadeOutSeconds = obj.Meta.FadeOutTime < 0.0f ? 1.0f : (float)obj.Meta.FadeOutTime;
        }
        else
        {
            FadeOutSeconds = 1.0f;
        }

        int totalPointCount = 0;
        int totalSegmentCount = 0;

        // Curves
        for (int curveCount = 0; curveCount < _motionData.CurveCount; ++curveCount)
        {
            var item = obj.Curves[curveCount];
            string key = item.Target;
            _motionData.Curves[curveCount] = new()
            {
                Id = _context.IdManager.GetId(item.Id),
                BaseSegmentIndex = totalSegmentCount,
                FadeInTime = item.FadeInTime != null ? (float)item.FadeInTime : -1.0f,
                FadeOutTime = item.FadeOutTime != null ? (float)item.FadeOutTime : -1.0f,
                Type = key switch
                {
                    TargetNameModel => CubismMotionCurveTarget.Model,
                    TargetNameParameter => CubismMotionCurveTarget.Parameter,
                    TargetNamePartOpacity => CubismMotionCurveTarget.PartOpacity,
                    _ => throw new Exception("Error: Unable to get segment type from Curve! The number of \"CurveCount\" may be incorrect!")
                }
            };

            // Segments
            for (int segmentPosition = 0; segmentPosition < item.Segments.Count;)
            {
                if (segmentPosition == 0)
                {
                    _motionData.Segments[totalSegmentCount] = new()
                    {
                        BasePointIndex = totalPointCount
                    };
                    _motionData.Points[totalPointCount] = new()
                    {
                        Time = item.Segments[segmentPosition],
                        Value = item.Segments[segmentPosition + 1]
                    };

                    totalPointCount += 1;
                    segmentPosition += 2;
                }
                else
                {
                    _motionData.Segments[totalSegmentCount] = new()
                    {
                        BasePointIndex = totalPointCount - 1
                    };
                }

                switch ((CubismMotionSegmentType)item.Segments[segmentPosition])
                {
                    case CubismMotionSegmentType.Linear:
                        {
                            _motionData.Segments[totalSegmentCount].SegmentType = CubismMotionSegmentType.Linear;
                            _motionData.Segments[totalSegmentCount].Evaluate = LinearEvaluate;

                            _motionData.Points[totalPointCount] = new()
                            {
                                Time = item.Segments[segmentPosition + 1],
                                Value = item.Segments[segmentPosition + 2]
                            };

                            totalPointCount += 1;
                            segmentPosition += 3;

                            break;
                        }
                    case CubismMotionSegmentType.Bezier:
                        {
                            _motionData.Segments[totalSegmentCount].SegmentType = CubismMotionSegmentType.Bezier;
                            if (areBeziersRestructed || UseOldBeziersCurveMotion)
                            {
                                _motionData.Segments[totalSegmentCount].Evaluate = BezierEvaluate;
                            }
                            else
                            {
                                _motionData.Segments[totalSegmentCount].Evaluate = BezierEvaluateCardanoInterpretation;
                            }

                            _motionData.Points[totalPointCount] = new()
                            {
                                Time = item.Segments[segmentPosition + 1],
                                Value = item.Segments[segmentPosition + 2]
                            };

                            _motionData.Points[totalPointCount + 1] = new()
                            {
                                Time = item.Segments[segmentPosition + 3],
                                Value = item.Segments[segmentPosition + 4]
                            };

                            _motionData.Points[totalPointCount + 2] = new()
                            {
                                Time = item.Segments[segmentPosition + 5],
                                Value = item.Segments[segmentPosition + 6]
                            };

                            totalPointCount += 3;
                            segmentPosition += 7;

                            break;
                        }
                    case CubismMotionSegmentType.Stepped:
                        {
                            _motionData.Segments[totalSegmentCount].SegmentType = CubismMotionSegmentType.Stepped;
                            _motionData.Segments[totalSegmentCount].Evaluate = SteppedEvaluate;

                            _motionData.Points[totalPointCount] = new()
                            {
                                Time = item.Segments[segmentPosition + 1],
                                Value = item.Segments[segmentPosition + 2]
                            };

                            totalPointCount += 1;
                            segmentPosition += 3;

                            break;
                        }
                    case CubismMotionSegmentType.InverseStepped:
                        {
                            _motionData.Segments[totalSegmentCount].SegmentType = CubismMotionSegmentType.InverseStepped;
                            _motionData.Segments[totalSegmentCount].Evaluate = InverseSteppedEvaluate;

                            _motionData.Points[totalPointCount] = new()
                            {
                                Time = item.Segments[segmentPosition + 1],
                                Value = item.Segments[segmentPosition + 2]
                            };

                            totalPointCount += 1;
                            segmentPosition += 3;

                            break;
                        }
                    default:
                        {
                            throw new Exception("CubismMotionSegmentType error");
                        }
                }

                ++_motionData.Curves[curveCount].SegmentCount;
                ++totalSegmentCount;
            }
        }

        for (int userdatacount = 0; userdatacount < obj.Meta.UserDataCount; ++userdatacount)
        {
            _motionData.Events[userdatacount] = new()
            {
                FireTime = obj.UserData[userdatacount].Time,
                Value = obj.UserData[userdatacount].Value
            };
        }

        _sourceFrameRate = _motionData.Fps;
        _loopDurationSeconds = _motionData.Duration;
        OnFinishedMotion = onFinishedMotionHandler;
        OnBeganMotion = onBeganMotionHandler;
    }

    /// <summary>
    /// 创建实例 (兼容旧签名, 使用默认上下文).
    /// </summary>
    /// <param name="buffer">已读取 motion3.json 的缓冲区</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数. 为 NULL 时不调用.</param>
    /// <param name="onBeganMotionHandler">动作播放开始时调用的回调函数. 为 NULL 时不调用.</param>
    /// <param name="shouldCheckMotionConsistency">是否校验 motion3.json 的一致性</param>
    [Obsolete("请改用带 CubismContext 的构造")]
    public CubismMotion(string buffer, FinishedMotionCallback? onFinishedMotionHandler = null,
        BeganMotionCallback? onBeganMotionHandler = null, bool shouldCheckMotionConsistency = false)
        : this(buffer, CubismContext.Default, onFinishedMotionHandler, onBeganMotionHandler, shouldCheckMotionConsistency)
    {
    }

    /// <summary>
    /// 执行模型的参数更新.
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="userTimeSeconds">当前时刻[秒]</param>
    /// <param name="fadeWeight">动作的权重</param>
    /// <param name="motionQueueEntry">CubismMotionQueueManager 中管理的动作</param>
    public override void DoUpdateParameters(CubismModel model, float userTimeSeconds, float fadeWeight, CubismMotionQueueEntry motionQueueEntry)
    {
        _modelCurveIdEyeBlink ??= _context.IdManager.GetId(EffectNameEyeBlink);

        _modelCurveIdLipSync ??= _context.IdManager.GetId(EffectNameLipSync);

        _modelCurveIdOpacity ??= _context.IdManager.GetId(IdNameOpacity);

        if (_motionBehavior == MotionBehavior.V2)
        {
            if (_previousLoopState != _isLoop)
            {
                // 重新计算结束时间
                AdjustEndTime(motionQueueEntry);
                _previousLoopState = _isLoop;
            }
        }

        float timeOffsetSeconds = userTimeSeconds - motionQueueEntry.StartTime;

        if (timeOffsetSeconds < 0.0f)
        {
            timeOffsetSeconds = 0.0f; // 避免出错
        }

        float lipSyncValue = float.MaxValue;
        float eyeBlinkValue = float.MaxValue;

        //用于检测眨眼, 唇形同步中是否已应用动作的位标志 (最多 maxFlagCount 个
        int MaxTargetSize = 64;
        ulong lipSyncFlags = 0;
        ulong eyeBlinkFlags = 0;

        //眨眼, 唇形同步的目标数量超过上限时
        if (_eyeBlinkParameterIds.Count > MaxTargetSize)
        {
            CubismLog.Warning($"[Live2D SDK]too many eye blink targets : {_eyeBlinkParameterIds.Count}");
        }
        if (_lipSyncParameterIds.Count > MaxTargetSize)
        {
            CubismLog.Warning($"[Live2D SDK]too many lip sync targets : {_lipSyncParameterIds.Count}");
        }

        float tmpFadeIn = (FadeInSeconds <= 0.0f) ? 1.0f :
           CubismMath.GetEasingSine((userTimeSeconds - motionQueueEntry.FadeInStartTime) / FadeInSeconds);

        float tmpFadeOut = (FadeOutSeconds <= 0.0f || motionQueueEntry.EndTime < 0.0f) ? 1.0f :
           CubismMath.GetEasingSine((motionQueueEntry.EndTime - userTimeSeconds) / FadeOutSeconds);

        float value;
        int c, parameterIndex;

        // 按需要重复时间.
        float time = timeOffsetSeconds;
        float duration = _motionData.Duration;
        bool isCorrection = _motionBehavior == MotionBehavior.V2 && _isLoop;

        if (_isLoop)
        {
            if (_motionBehavior == MotionBehavior.V2)
            {
                duration += 1.0f / _motionData.Fps;
            }

            while (time > duration)
            {
                time -= duration;
            }
        }

        var curves = _motionData.Curves;

        // 求模型曲线的值.
        for (c = 0; c < _motionData.CurveCount && curves[c].Type == CubismMotionCurveTarget.Model; ++c)
        {
            // 求曲线的值并调用处理.
            value = EvaluateCurve(_motionData, c, time, isCorrection, duration);

            if (curves[c].Id == _modelCurveIdEyeBlink)
            {
                eyeBlinkValue = value;
            }
            else if (curves[c].Id == _modelCurveIdLipSync)
            {
                lipSyncValue = value;
            }
            else if (curves[c].Id == _modelCurveIdOpacity)
            {
                _modelOpacity = value;

                // ------ 若不透明度值存在则反映 ------
                model.SetModelOpacity(GetModelOpacityValue());
            }
        }

        int parameterMotionCurveCount = 0;

        for (; c < _motionData.CurveCount && curves[c].Type == CubismMotionCurveTarget.Parameter; ++c)
        {
            parameterMotionCurveCount++;

            // 查找参数索引.
            parameterIndex = model.GetParameterIndex(curves[c].Id);

            // 若 sink 中没有值则跳过曲线求值.
            if (parameterIndex == -1)
            {
                continue;
            }

            float sourceValue = model.GetParameterValue(parameterIndex);

            // 求曲线的值并应用.
            value = EvaluateCurve(_motionData, c, time, isCorrection, duration);

            if (eyeBlinkValue != float.MaxValue)
            {
                for (int i = 0; i < _eyeBlinkParameterIds.Count && i < MaxTargetSize; ++i)
                {
                    if (_eyeBlinkParameterIds[i] == curves[c].Id)
                    {
                        value *= eyeBlinkValue;
                        eyeBlinkFlags |= 1UL << i;
                        break;
                    }
                }
            }

            if (lipSyncValue != float.MaxValue)
            {
                for (int i = 0; i < _lipSyncParameterIds.Count && i < MaxTargetSize; ++i)
                {
                    if (_lipSyncParameterIds[i] == curves[c].Id)
                    {
                        value += lipSyncValue;
                        lipSyncFlags |= 1UL << i;
                        break;
                    }
                }
            }

            // 为兼容性只处理重复 (官方 5-r.5)
            if (model.IsRepeat(parameterIndex))
            {
                value = model.GetParameterRepeatValue(parameterIndex, value);
            }

            float v;
            // 按参数进行渐变
            if (curves[c].FadeInTime < 0.0f && curves[c].FadeOutTime < 0.0f)
            {
                //应用动作的渐变
                v = sourceValue + (value - sourceValue) * fadeWeight;
            }
            else
            {
                // 若针对该参数设置了淡入或淡出, 则应用该设置
                float fin;
                float fout;

                if (curves[c].FadeInTime < 0.0f)
                {
                    fin = tmpFadeIn;
                }
                else
                {
                    fin = curves[c].FadeInTime == 0.0f ? 1.0f
                            : CubismMath.GetEasingSine((userTimeSeconds - motionQueueEntry.FadeInStartTime) / curves[c].FadeInTime);
                }

                if (curves[c].FadeOutTime < 0.0f)
                {
                    fout = tmpFadeOut;
                }
                else
                {
                    fout = (curves[c].FadeOutTime == 0.0f || motionQueueEntry.EndTime < 0.0f)
                                ? 1.0f
                            : CubismMath.GetEasingSine((motionQueueEntry.EndTime - userTimeSeconds) / curves[c].FadeOutTime);
                }

                float paramWeight = Weight * fin * fout;

                // 应用按参数的渐变
                v = sourceValue + (value - sourceValue) * paramWeight;
            }

            model.SetParameterValue(parameterIndex, v);
        }

        if (eyeBlinkValue != float.MaxValue)
        {
            for (int i = 0; i < _eyeBlinkParameterIds.Count && i < MaxTargetSize; ++i)
            {
                float sourceValue = model.GetParameterValue(_eyeBlinkParameterIds[i]);
                //动作中已覆盖该参数时不应用眨眼
                if (((eyeBlinkFlags >> i) & 0x01) != 0UL)
                {
                    continue;
                }

                float v = sourceValue + (eyeBlinkValue - sourceValue) * fadeWeight;

                model.SetParameterValue(_eyeBlinkParameterIds[i], v);
            }
        }

        if (lipSyncValue != float.MaxValue)
        {
            for (int i = 0; i < _lipSyncParameterIds.Count && i < MaxTargetSize; ++i)
            {
                float sourceValue = model.GetParameterValue(_lipSyncParameterIds[i]);
                //动作中已覆盖该参数时不应用唇形同步
                if (((lipSyncFlags >> i) & 0x01) != 0UL)
                {
                    continue;
                }

                float v = sourceValue + (lipSyncValue - sourceValue) * fadeWeight;

                model.SetParameterValue(_lipSyncParameterIds[i], v);
            }
        }

        for (; c < _motionData.CurveCount && curves[c].Type == CubismMotionCurveTarget.PartOpacity; ++c)
        {
            // 查找参数索引.
            parameterIndex = model.GetParameterIndex(curves[c].Id);

            // 若 sink 中没有值则跳过曲线求值.
            if (parameterIndex == -1)
            {
                continue;
            }

            // 求曲线的值并应用.
            value = EvaluateCurve(_motionData, c, time, isCorrection, duration);

            model.SetParameterValue(parameterIndex, value);
        }

        if (timeOffsetSeconds >= duration)
        {
            if (_isLoop)
            {
                UpdateForNextLoop(model, motionQueueEntry, userTimeSeconds, time);
            }
            else
            {
                OnFinishedMotion?.Invoke(model, this);

                motionQueueEntry.Finished = true;
            }
        }

        _lastWeight = fadeWeight;
    }

    /// <summary>
    /// 循环时面向下次播放的状态更新 (官方 5-r.5 的 UpdateForNextLoop).
    /// </summary>
    /// <param name="model">更新目标的模型</param>
    /// <param name="motionQueueEntry">CubismMotionQueueManager 中管理的动作</param>
    /// <param name="userTimeSeconds">增量时间的累计值[秒]</param>
    /// <param name="time">循环内的当前时间[秒]</param>
    /// <remarks>
    /// 官方的 UpdateForNextLoop 不接收 model, 但本地的 FinishedMotionCallback 是
    /// (CubismModel, ACubismMotion) 的 2 个参数, 因此追加了 model (private 方法, 对外无影响).
    /// </remarks>
    private void UpdateForNextLoop(CubismModel model, CubismMotionQueueEntry motionQueueEntry, float userTimeSeconds,
        float time)
    {
        switch (_motionBehavior)
        {
            case MotionBehavior.V2:
            default:
                motionQueueEntry.StartTime = userTimeSeconds - time; //回到初始状态
                if (_isLoopFadeIn)
                {
                    //循环中且循环用淡入有效时, 重新设置淡入
                    motionQueueEntry.FadeInStartTime = userTimeSeconds - time;
                }

                OnFinishedMotion?.Invoke(model, this);
                break;

            case MotionBehavior.V1:
                // 旧版循环处理
                motionQueueEntry.StartTime = userTimeSeconds; //回到初始状态
                if (_isLoopFadeIn)
                {
                    //循环中且循环用淡入有效时, 重新设置淡入
                    motionQueueEntry.FadeInStartTime = userTimeSeconds;
                }

                break;
        }
    }

    /// <summary>
    /// 获取动作的长度.
    /// </summary>
    /// <returns>动作的长度[秒]</returns>
    public override float GetDuration()
    {
        return _isLoop ? -1.0f : _loopDurationSeconds;
    }

    /// <summary>
    /// 设置 Motion Behavior 的版本 (官方 5-r.5 中新增).
    /// </summary>
    /// <param name="motionBehavior">Motion Behavior 的版本</param>
    public void SetMotionBehavior(MotionBehavior motionBehavior)
    {
        _motionBehavior = motionBehavior;
    }

    /// <summary>
    /// 获取 Motion Behavior 的版本 (官方 5-r.5 中新增).
    /// </summary>
    /// <returns>Motion Behavior 的版本</returns>
    public MotionBehavior GetMotionBehavior()
    {
        return _motionBehavior;
    }

    /// <summary>
    /// 获取动作循环时的长度.
    /// </summary>
    /// <returns>动作循环时的长度[秒]</returns>
    public override float GetLoopDuration()
    {
        return _loopDurationSeconds;
    }

    /// <summary>
    /// 设置参数的淡入时间.
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <param name="value">淡入所需的时间[秒]</param>
    public void SetParameterFadeInTime(string parameterId, float value)
    {
        var curves = _motionData.Curves;

        for (int i = 0; i < _motionData.CurveCount; ++i)
        {
            if (parameterId == curves[i].Id)
            {
                curves[i].FadeInTime = value;
                return;
            }
        }
    }

    /// <summary>
    /// 设置参数的淡出时间.
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <param name="value">淡出所需的时间[秒]</param>
    public void SetParameterFadeOutTime(string parameterId, float value)
    {
        var curves = _motionData.Curves;

        for (int i = 0; i < _motionData.CurveCount; ++i)
        {
            if (parameterId == curves[i].Id)
            {
                curves[i].FadeOutTime = value;
                return;
            }
        }
    }

    /// <summary>
    /// 获取参数的淡入时间.
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <returns>淡入所需的时间[秒]</returns>
    public float GetParameterFadeInTime(string parameterId)
    {
        var curves = _motionData.Curves;

        for (int i = 0; i < _motionData.CurveCount; ++i)
        {
            if (parameterId == curves[i].Id)
            {
                return curves[i].FadeInTime;
            }
        }

        return -1;
    }

    /// <summary>
    /// 获取参数的淡出时间.
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <returns>淡出所需的时间[秒]</returns>
    public float GetParameterFadeOutTime(string parameterId)
    {
        var curves = _motionData.Curves;

        for (int i = 0; i < _motionData.CurveCount; ++i)
        {
            if (parameterId == curves[i].Id)
            {
                return curves[i].FadeOutTime;
            }
        }

        return -1;
    }

    /// <summary>
    /// 设置正在应用自动效果的参数 ID 列表.
    /// </summary>
    /// <param name="eyeBlinkParameterIds">正在应用自动眨眼的参数 ID 列表</param>
    /// <param name="lipSyncParameterIds">正在应用唇形同步的参数 ID 列表</param>
    public void SetEffectIds(List<string> eyeBlinkParameterIds, List<string> lipSyncParameterIds)
    {
        _eyeBlinkParameterIds = eyeBlinkParameterIds;
        _lipSyncParameterIds = lipSyncParameterIds;
    }

    /// <summary>
    /// 事件触发的检查.
    /// 输入的时间以被调用的动作时机为 0 的秒数计.
    /// </summary>
    /// <param name="beforeCheckTimeSeconds">上一次的事件检查时间[秒]</param>
    /// <param name="motionTimeSeconds">本次的播放时间[秒]</param>
    /// <returns></returns>
    public override List<string> GetFiredEvent(float beforeCheckTimeSeconds, float motionTimeSeconds)
    {
        FiredEventValues.Clear();
        /// 事件的触发检查
        for (int u = 0; u < _motionData.EventCount; ++u)
        {
            if ((_motionData.Events[u].FireTime > beforeCheckTimeSeconds) &&
                (_motionData.Events[u].FireTime <= motionTimeSeconds))
            {
                FiredEventValues.Add(_motionData.Events[u].Value);
            }
        }

        return FiredEventValues;

    }

    /// <summary>
    /// 确认是否存在不透明度的曲线
    /// </summary>
    /// <returns>true  . 键存在
    /// false . 键不存在</returns>
    public override bool IsExistModelOpacity()
    {
        for (int i = 0; i < _motionData.CurveCount; i++)
        {
            CubismMotionCurve curve = _motionData.Curves[i];

            if (curve.Type != CubismMotionCurveTarget.Model)
            {
                continue;
            }

            if (curve.Id == IdNameOpacity)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 返回不透明度曲线的索引
    /// </summary>
    /// <returns>不透明度曲线的索引</returns>
    public override int GetModelOpacityIndex()
    {
        if (IsExistModelOpacity())
        {
            for (int i = 0; i < _motionData.CurveCount; i++)
            {
                CubismMotionCurve curve = _motionData.Curves[i];

                if (curve.Type != CubismMotionCurveTarget.Model)
                {
                    continue;
                }

                if (curve.Id == IdNameOpacity)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// 返回不透明度的 Id
    /// </summary>
    /// <returns>不透明度的 Id</returns>
    public override string? GetModelOpacityId(int index)
    {
        if (index != -1)
        {
            CubismMotionCurve curve = _motionData.Curves[index];

            if (curve.Type == CubismMotionCurveTarget.Model)
            {
                if (curve.Id == IdNameOpacity)
                {
                    return _context.IdManager.GetId(curve.Id);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 返回不透明度的值
    /// 要获取更新后的值, 请在 UpdateParameters() 之后调用.
    /// </summary>
    /// <returns>动作当前时间的不透明度值</returns>
    public override float GetModelOpacityValue()
    {
        return _modelOpacity;
    }
}
