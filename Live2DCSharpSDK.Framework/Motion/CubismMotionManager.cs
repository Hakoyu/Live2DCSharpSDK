using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 进行动作管理的类.
/// </summary>
public class CubismMotionManager : CubismMotionQueueManager
{
    /// <summary>
    /// 当前播放中的动作的优先级
    /// </summary>
    public MotionPriority CurrentPriority { get; private set; }

    /// <summary>
    /// 预定播放的动作的优先级. 播放期间为 0. 用于在其他线程中读取动作文件.
    /// </summary>
    public MotionPriority ReservePriority { get; set; }

    /// <summary>
    /// 设置优先级并开始动作.
    /// </summary>
    /// <param name="motion">动作</param>
    /// <param name="autoDelete">若在播放结束后删除动作实例则为 true</param>
    /// <param name="priority">优先级</param>
    /// <returns>返回已开始动作的识别编号. 用于 IsFinished() 的参数, 判定各个动作是否结束. 无法开始时为 "-1"</returns>
    public CubismMotionQueueEntry StartMotionPriority(ACubismMotion motion, MotionPriority priority)
    {
        if (priority == ReservePriority)
        {
            ReservePriority = 0; // 解除预约
        }

        CurrentPriority = priority; // 设置播放中动作的优先级

        return StartMotion(motion);
    }

    /// <summary>
    /// 更新动作, 并把参数值反映到模型.
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    /// <returns>true    已更新
    /// false   未更新</returns>
    public bool UpdateMotion(CubismModel model, float deltaTimeSeconds)
    {
        UserTimeSeconds += deltaTimeSeconds;

        bool updated = DoUpdateMotion(model, UserTimeSeconds);

        if (IsFinished())
        {
            CurrentPriority = 0; // 解除播放中动作的优先级
        }

        return updated;
    }

    /// <summary>
    /// 预约动作.
    /// </summary>
    /// <param name="priority">优先级</param>
    /// <returns>true    预约成功
    /// false   预约失败</returns>
    public bool ReserveMotion(MotionPriority priority)
    {
        if ((priority <= ReservePriority) || (priority <= CurrentPriority))
        {
            return false;
        }

        ReservePriority = priority;

        return true;
    }
}
