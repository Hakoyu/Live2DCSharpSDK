using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 动作的抽象基类. 通过 MotionQueueManager 管理动作的播放.
/// </summary>
public abstract class ACubismMotion
{
    /// <summary>
    /// 淡入所需的时间[秒]
    /// </summary>
    public float FadeInSeconds { get; set; }
    /// <summary>
    /// 淡出所需的时间[秒]
    /// </summary>
    public float FadeOutSeconds { get; set; }
    /// <summary>
    /// 动作的权重
    /// </summary>
    public float Weight { get; set; }
    /// <summary>
    /// 动作播放的开始时刻[秒]
    /// </summary>
    public float OffsetSeconds { get; set; }

    protected readonly List<string> FiredEventValues = [];

    // 动作播放结束回调函数
    public FinishedMotionCallback? OnFinishedMotion { get; set; }

    // 动作播放开始回调函数 (官方 5-r.5 中新增)
    public BeganMotionCallback? OnBeganMotion { get; set; }

    /// <summary>
    /// 是否循环 (官方 5-r.5 中从 CubismMotion 移入. 默认值 false)
    /// </summary>
    protected bool _isLoop;
    /// <summary>
    /// 循环时是否启用淡入的标志 (默认值 true)
    /// </summary>
    protected bool _isLoopFadeIn;
    /// <summary>
    /// 上一次的循环状态 (官方 5-r.5 的 _previousLoopState)
    /// </summary>
    protected bool _previousLoopState;

    /// <summary>
    /// 构造函数.
    /// </summary>
    public ACubismMotion()
    {
        FadeInSeconds = -1.0f;
        FadeOutSeconds = -1.0f;
        Weight = 1.0f;
        _isLoop = false;
        _isLoopFadeIn = true;
        _previousLoopState = _isLoop;
    }

    /// <summary>
    /// 更新模型参数.
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="motionQueueEntry">CubismMotionQueueManager 中管理的动作</param>
    /// <param name="userTimeSeconds">增量时间的累计值[秒]</param>
    public void UpdateParameters(CubismModel model, CubismMotionQueueEntry motionQueueEntry, float userTimeSeconds)
    {
        if (!motionQueueEntry.Available || motionQueueEntry.Finished)
        {
            return;
        }

        SetupMotionQueueEntry(motionQueueEntry, userTimeSeconds);

        var fadeWeight = UpdateFadeWeight(motionQueueEntry, userTimeSeconds);

        //---- 遍历所有的参数 ID ----
        DoUpdateParameters(model, userTimeSeconds, fadeWeight, motionQueueEntry);

        //后处理
        //超过结束时刻后置结束标志 (CubismMotionQueueManager)
        if ((motionQueueEntry.EndTime > 0) && (motionQueueEntry.EndTime < userTimeSeconds))
        {
            motionQueueEntry.Finished = true;      //结束
        }
    }

    /// <summary>
    /// 进行用于开始动作播放的准备工作.
    /// </summary>
    /// <param name="motionQueueEntry">由 CubismMotionQueueManager 管理的动作</param>
    /// <param name="userTimeSeconds">总播放时间 (秒)</param>
    public void SetupMotionQueueEntry(CubismMotionQueueEntry motionQueueEntry, float userTimeSeconds)
    {
        if (!motionQueueEntry.Available || motionQueueEntry.Finished)
        {
            return;
        }

        if (motionQueueEntry.Started)
        {
            return;
        }

        motionQueueEntry.Started = true;
        motionQueueEntry.StartTime = userTimeSeconds - OffsetSeconds; //记录动作的开始时刻
        motionQueueEntry.FadeInStartTime = userTimeSeconds; //淡入的开始时刻

        if (motionQueueEntry.EndTime < 0)
        {
            //有时会在开始前就设置结束.
            AdjustEndTime(motionQueueEntry);
        }

        // 动作播放开始回调 (官方 ACubismMotion.cpp:55-59)
        OnBeganMotion?.Invoke(this);
    }

    /// <summary>
    /// 设置动作的结束时刻 (官方 5-r.5 中新增的 AdjustEndTime).
    /// </summary>
    /// <param name="motionQueueEntry">由 CubismMotionQueueManager 管理的动作</param>
    protected void AdjustEndTime(CubismMotionQueueEntry motionQueueEntry)
    {
        var duration = GetDuration();

        // duration == -1 时循环
        motionQueueEntry.EndTime = (duration <= 0)
            ? -1
            : motionQueueEntry.StartTime + duration;
    }

    /// <summary>
    /// 设置动作是否循环.
    /// </summary>
    /// <param name="loop">若为 true 则循环</param>
    public void SetLoop(bool loop)
    {
        _isLoop = loop;
    }

    /// <summary>
    /// 获取动作是否循环.
    /// </summary>
    /// <returns>若循环则为 true</returns>
    public bool GetLoop()
    {
        return _isLoop;
    }

    /// <summary>
    /// 设置循环时是否进行淡入.
    /// </summary>
    /// <param name="loopFadeIn">若为 true 则在循环时淡入</param>
    public void SetLoopFadeIn(bool loopFadeIn)
    {
        _isLoopFadeIn = loopFadeIn;
    }

    /// <summary>
    /// 获取循环时是否进行淡入.
    /// </summary>
    /// <returns>若循环时淡入则为 true</returns>
    public bool GetLoopFadeIn()
    {
        return _isLoopFadeIn;
    }

    /// <summary>
    /// 更新动作的权重.
    /// </summary>
    /// <param name="motionQueueEntry">CubismMotionQueueManager 中管理的动作</param>
    /// <param name="userTimeSeconds">增量时间的累计值[秒]</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public float UpdateFadeWeight(CubismMotionQueueEntry? motionQueueEntry, float userTimeSeconds)
    {
        if (motionQueueEntry == null)
        {
            // 官方会调用 CubismLogError 并返回 -1. 本地的 CubismLog.Error 会抛出异常,
            // 为与官方一致, 采取 "返回 -1 并由调用方处理" 的行为, 因此使用 Warning.
            CubismLog.Warning("[Live2D SDK]motionQueueEntry is null.");
            return -1;
        }

        float fadeWeight = Weight; //与当前值相乘的比例

        //---- 淡入淡出的处理 ----
        //使用简单的正弦函数进行缓动
        float fadeIn = FadeInSeconds == 0.0f ? 1.0f
                           : CubismMath.GetEasingSine((userTimeSeconds - motionQueueEntry.FadeInStartTime) / FadeInSeconds);

        float fadeOut = (FadeOutSeconds == 0.0f || motionQueueEntry.EndTime < 0.0f) ? 1.0f
                            : CubismMath.GetEasingSine((motionQueueEntry.EndTime - userTimeSeconds) / FadeOutSeconds);

        fadeWeight = fadeWeight * fadeIn * fadeOut;

        motionQueueEntry.SetState(userTimeSeconds, fadeWeight);

        if (0.0f > fadeWeight || fadeWeight > 1.0f)
        {
            throw new Exception("fadeWeight out of range");
        }

        return fadeWeight;
    }

    /// <summary>
    /// 获取动作的长度.
    /// 
    /// 循环时为 "-1".
    /// 非循环时请重写此方法.
    /// 为正值时在取得的时刻结束.
    /// 为 "-1" 时, 只要外部没有停止指令就不会结束.
    /// </summary>
    /// <returns>动作的长度[秒]</returns>
    public virtual float GetDuration()
    {
        return -1.0f;
    }

    /// <summary>
    /// 获取动作循环一次的时长.
    /// 
    /// 不循环时返回与 GetDuration() 相同的值.
    /// 无法定义循环一次时长时 (例如程序上会持续运动的子类) 返回 "-1".
    /// </summary>
    /// <returns>动作循环一次的时长[秒]</returns>
    public virtual float GetLoopDuration()
    {
        return -1.0f;
    }

    /// <summary>
    /// 事件触发的检查.
    /// 输入的时间以被调用的动作时机为 0 的秒数计.
    /// </summary>
    /// <param name="beforeCheckTimeSeconds">上一次的事件检查时间[秒]</param>
    /// <param name="motionTimeSeconds">本次的播放时间[秒]</param>
    /// <returns></returns>
    public virtual List<string> GetFiredEvent(float beforeCheckTimeSeconds, float motionTimeSeconds)
    {
        return FiredEventValues;
    }

    /// <summary>
    /// 确认是否存在不透明度的曲线
    /// </summary>
    /// <returns>true  . 键存在
    /// false . 键不存在</returns>
    public virtual bool IsExistModelOpacity()
    {
        return false;
    }

    /// <summary>
    /// 返回不透明度曲线的索引
    /// </summary>
    /// <returns>success: 不透明度曲线的索引</returns>
    public virtual int GetModelOpacityIndex()
    {
        return -1;
    }

    /// <summary>
    /// 返回不透明度的 Id
    /// </summary>
    /// <returns>不透明度的 Id</returns>
    public virtual string? GetModelOpacityId(int index)
    {
        return "";
    }

    public virtual float GetModelOpacityValue()
    {
        return 1.0f;
    }

    public abstract void DoUpdateParameters(CubismModel model, float userTimeSeconds, float weight, CubismMotionQueueEntry motionQueueEntry);
}
