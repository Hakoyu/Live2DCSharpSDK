using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 动作播放结束回调函数定义
/// </summary>
public delegate void FinishedMotionCallback(CubismModel model, ACubismMotion self);

/// <summary>
/// 动作播放开始回调函数定义 (官方 5-r.5 的 BeganMotionCallback)
/// </summary>
/// <param name="self">已开始播放的动作</param>
public delegate void BeganMotionCallback(ACubismMotion self);

/// <summary>
/// 可注册到事件回调的函数的类型信息
/// </summary>
/// <param name="eventValue">已触发事件的字符串数据</param>
/// <param name="customData">注册时指定并返回给回调的数据</param>
public delegate void CubismMotionEventFunction(CubismUserModel? customData, string eventValue);

/// <summary>
/// 动作曲线段的求值函数.
/// </summary>
/// <param name="points">动作曲线的控制点列表</param>
/// <param name="time">求值的时间[秒]</param>
public delegate float csmMotionSegmentEvaluationFunction(
    CubismMotionPoint[] points,
    int start,
    float time
);

/// <summary>
/// 动作的优先级常量
/// </summary>
public enum MotionPriority : int
{
    PriorityNone = 0,
    PriorityIdle = 1,
    PriorityNormal = 2,
    PriorityForce = 3,
}

/// <summary>
/// 表情参数值的计算方式
/// </summary>
public enum ExpressionBlendType
{
    /// <summary>
    /// 加算
    /// </summary>
    Add = 0,

    /// <summary>
    /// 乘算
    /// </summary>
    Multiply = 1,

    /// <summary>
    /// 覆盖
    /// </summary>
    Overwrite = 2,
};

/// <summary>
/// 表情的参数信息的结构体.
/// </summary>
public record ExpressionParameter
{
    /// <summary>
    /// 参数 ID
    /// </summary>
    public required string ParameterId { get; set; }

    /// <summary>
    /// 参数的运算种类
    /// </summary>
    public ExpressionBlendType BlendType { get; set; }

    /// <summary>
    /// 值
    /// </summary>
    public float Value { get; set; }
}

/// <summary>
/// 动作曲线的种类.
/// </summary>
public enum CubismMotionCurveTarget
{
    /// <summary>
    /// 针对模型
    /// </summary>
    Model,

    /// <summary>
    /// 针对参数
    /// </summary>
    Parameter,

    /// <summary>
    /// 针对部件的不透明度
    /// </summary>
    PartOpacity,
};

/// <summary>
/// 动作曲线段的种类.
/// </summary>
public enum CubismMotionSegmentType : int
{
    /// <summary>
    /// 线性
    /// </summary>
    Linear = 0,

    /// <summary>
    /// 贝塞尔曲线
    /// </summary>
    Bezier = 1,

    /// <summary>
    /// 阶梯
    /// </summary>
    Stepped = 2,

    /// <summary>
    /// 反向阶梯
    /// </summary>
    InverseStepped = 3,
};

/// <summary>
/// 动作曲线的控制点.
/// </summary>
public struct CubismMotionPoint
{
    /// <summary>
    /// 时间[秒]
    /// </summary>
    public float Time;

    /// <summary>
    /// 值
    /// </summary>
    public float Value;
}

/// <summary>
/// 动作曲线的段.
/// </summary>
public record CubismMotionSegment
{
    /// <summary>
    /// 使用的求值函数
    /// </summary>
    public csmMotionSegmentEvaluationFunction Evaluate;

    /// <summary>
    /// 指向第一个段的索引
    /// </summary>
    public int BasePointIndex;

    /// <summary>
    /// 段的种类
    /// </summary>
    public CubismMotionSegmentType SegmentType;
}

/// <summary>
/// 动作曲线.
/// </summary>
public record CubismMotionCurve
{
    /// <summary>
    /// 曲线的种类
    /// </summary>
    public CubismMotionCurveTarget Type;

    /// <summary>
    /// 曲线的 ID
    /// </summary>
    public string Id;

    /// <summary>
    /// 段的个数
    /// </summary>
    public int SegmentCount;

    /// <summary>
    /// 第一个段的索引
    /// </summary>
    public int BaseSegmentIndex;

    /// <summary>
    /// 淡入所需的时间[秒]
    /// </summary>
    public float FadeInTime;

    /// <summary>
    /// 淡出所需的时间[秒]
    /// </summary>
    public float FadeOutTime;
}

/// <summary>
/// 事件.
/// </summary>
public record CubismMotionEvent
{
    public float FireTime;
    public string Value;
}

/// <summary>
/// 动作数据.
/// </summary>
public record CubismMotionData
{
    /// <summary>
    /// 动作的长度[秒]
    /// </summary>
    public float Duration;

    /// <summary>
    /// 是否循环
    /// </summary>
    public bool Loop;

    /// <summary>
    /// 曲线的个数
    /// </summary>
    public int CurveCount;

    /// <summary>
    /// UserData 的个数
    /// </summary>
    public int EventCount;

    /// <summary>
    /// 帧率
    /// </summary>
    public float Fps;

    /// <summary>
    /// 曲线的列表
    /// </summary>
    public CubismMotionCurve[] Curves;

    /// <summary>
    /// 段的列表
    /// </summary>
    public CubismMotionSegment[] Segments;

    /// <summary>
    /// 点的列表
    /// </summary>
    public CubismMotionPoint[] Points;

    /// <summary>
    /// 事件的列表
    /// </summary>
    public CubismMotionEvent[] Events;
}
