using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 用于动作播放管理的类. 用于播放 CubismMotion 动作等 ACubismMotion 的子类.
/// 
/// 播放过程中若通过 StartMotion() 启动了另一个动作, 则平滑切换到新动作并中断旧动作.
/// 例如将表情用动作, 身体用动作等分开做成动作时,
/// 需要同时播放多个动作时, 请使用多个 CubismMotionQueueManager 实例.
/// </summary>
public class CubismMotionQueueManager
{
    /// <summary>
    /// 动作
    /// </summary>
    protected readonly List<CubismMotionQueueEntry> Motions = [];

    private readonly List<CubismMotionQueueEntry> _remove = [];

    /// <summary>
    /// 回调函数指针
    /// </summary>
    private CubismMotionEventFunction? _eventCallback;
    /// <summary>
    /// 返回给回调的数据
    /// </summary>
    private CubismUserModel? _eventCustomData;

    /// <summary>
    /// 增量时间的累计值[秒]
    /// </summary>
    protected float UserTimeSeconds;

    /// <summary>
    /// 开始指定的动作. 若已存在同类型的动作, 则给已有动作置结束标志并开始淡出.
    /// </summary>
    /// <param name="motion">要开始的动作</param>
    /// <returns>返回已开始动作的识别编号. 用于 IsFinished() 的参数, 判定各个动作是否结束. 无法开始时为 "-1"</returns>
    public CubismMotionQueueEntry StartMotion(ACubismMotion motion)
    {
        CubismMotionQueueEntry motionQueueEntry;

        // 若已有动作则置结束标志
        for (int i = 0; i < Motions.Count; ++i)
        {
            motionQueueEntry = Motions[0];
            if (motionQueueEntry == null)
            {
                continue;
            }

            motionQueueEntry.SetFadeout(motionQueueEntry.Motion.FadeOutSeconds);
        }

        motionQueueEntry = new CubismMotionQueueEntry()
        {
            Motion = motion
        };

        Motions.Add(motionQueueEntry);

        return motionQueueEntry;
    }

    /// <summary>
    /// 所有动作是否都已结束.
    /// </summary>
    /// <returns>true    全部已结束
    /// false   尚未结束</returns>
    public bool IsFinished()
    {
        // ------- 执行处理 --------
        // 若已有动作则置结束标志
        foreach (var item in Motions)
        {
            // ----- 若存在已结束的处理则删除 ------
            if (!item.Finished)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 指定的动作是否已结束.
    /// </summary>
    /// <param name="motionQueueEntryNumber">动作的识别编号</param>
    /// <returns>true    指定的动作已结束
    /// false   尚未结束</returns>
    public bool IsFinished(object motionQueueEntryNumber)
    {
        // 若已有动作则置结束标志

        foreach (var item in Motions)
        {
            if (item == null)
            {
                continue;
            }

            if (item == motionQueueEntryNumber && !item.Finished)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 停止所有动作.
    /// </summary>
    public void StopAllMotions()
    {
        // ------- 执行处理 --------
        // 若已有动作则置结束标志

        Motions.Clear();
    }

    /// <summary>
    /// 获取指定的 CubismMotionQueueEntry.
    /// </summary>
    /// <param name="motionQueueEntryNumber">动作的识别编号</param>
    /// <returns>指向指定 CubismMotionQueueEntry 的指针
    /// NULL   未找到</returns>
    public CubismMotionQueueEntry? GetCubismMotionQueueEntry(object motionQueueEntryNumber)
    {
        //------- 执行处理 --------
        //若已有动作则置结束标志

        foreach (var item in Motions)
        {
            if (item == motionQueueEntryNumber)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// 注册接收事件的 Callback.
    /// </summary>
    /// <param name="callback">回调函数</param>
    /// <param name="customData">返回给回调的数据</param>
    public void SetEventCallback(CubismMotionEventFunction callback, CubismUserModel customData)
    {
        _eventCallback = callback;
        _eventCustomData = customData;
    }

    /// <summary>
    /// 更新动作, 并把参数值反映到模型.
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="userTimeSeconds">增量时间的累计值[秒]</param>
    /// <returns>true    已把参数值反映到模型
    /// false   未把参数值反映到模型 (动作无变化)</returns>
    public virtual bool DoUpdateMotion(CubismModel model, float userTimeSeconds)
    {
        bool updated = false;

        // ------- 执行处理 --------
        // 若已有动作则置结束标志

        _remove.Clear();

        foreach (var item in Motions)
        {
            var motion = item.Motion;

            // ------ 反映值 ------
            motion.UpdateParameters(model, item, userTimeSeconds);
            updated = true;

            // ------ 检查用户触发事件 ----
            var firedList = motion.GetFiredEvent(
                item.LastEventCheckSeconds - item.StartTime,
                userTimeSeconds - item.StartTime);

            for (int i = 0; i < firedList.Count; ++i)
            {
                _eventCallback?.Invoke(_eventCustomData, firedList[i]);
            }

            item.LastEventCheckSeconds = userTimeSeconds;

            // ----- 若存在已结束的处理则删除 ------
            if (item.Finished)
            {
                _remove.Add(item);          // 删除
            }
            else
            {
                if (item.IsTriggeredFadeOut)
                {
                    item.StartFadeout(item.FadeOutSeconds, userTimeSeconds);
                }
            }
        }

        foreach (var item in _remove)
        {
            Motions.Remove(item);
        }

        return updated;
    }
}
