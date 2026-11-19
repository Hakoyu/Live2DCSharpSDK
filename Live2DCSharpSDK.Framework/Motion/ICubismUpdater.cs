using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 更新处理的执行顺序 (官方 ICubismUpdater.hpp:12-22 的 CubismUpdateOrder).
/// </summary>
public enum CubismUpdateOrder : int
{
    /// <summary>
    /// 自动眨眼
    /// </summary>
    EyeBlink = 200,
    /// <summary>
    /// 表情
    /// </summary>
    Expression = 300,
    /// <summary>
    /// 拖拽 (面部朝向)
    /// </summary>
    Look = 400,
    /// <summary>
    /// 呼吸
    /// </summary>
    Breath = 500,
    /// <summary>
    /// 物理运算
    /// </summary>
    Physics = 600,
    /// <summary>
    /// 唇形同步
    /// </summary>
    LipSync = 700,
    /// <summary>
    /// 姿势
    /// </summary>
    Pose = 800,
    /// <summary>
    /// 未指定 (最后执行)
    /// </summary>
    Max = int.MaxValue
}

/// <summary>
/// 由 CubismUpdateScheduler 管理的更新处理抽象基类 (官方 ICubismUpdater).
/// </summary>
public abstract class ICubismUpdater
{
    /// <summary>
    /// 执行顺序
    /// </summary>
    private readonly int _executionOrder;

    /// <summary>
    /// 不指定执行顺序进行创建. 执行顺序为 CubismUpdateOrder.Max.
    /// </summary>
    protected ICubismUpdater()
        : this((int)CubismUpdateOrder.Max)
    {
    }

    /// <summary>
    /// 指定执行顺序进行创建.
    /// </summary>
    /// <param name="executionOrder">执行顺序</param>
    protected ICubismUpdater(int executionOrder)
    {
        _executionOrder = executionOrder;
    }

    /// <summary>
    /// 执行顺序 (对应官方的 private _executionOrder. 用于排序).
    /// </summary>
    internal int ExecutionOrder => _executionOrder;

    /// <summary>
    /// 用于排序的比较函数 (官方 ICubismUpdater::SortFunction).
    /// </summary>
    /// <param name="left">要比较的更新处理 1</param>
    /// <param name="right">要比较的更新处理 2</param>
    /// <returns>若 left 先于 right 执行则为 true</returns>
    public static bool SortFunction(ICubismUpdater left, ICubismUpdater right)
    {
        return left._executionOrder < right._executionOrder;
    }

    /// <summary>
    /// 更新处理.
    /// </summary>
    /// <param name="model">更新目标的模型</param>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    public abstract void OnLateUpdate(CubismModel model, float deltaTimeSeconds);
}
