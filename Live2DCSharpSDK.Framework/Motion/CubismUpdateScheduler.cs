using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 按执行顺序调用已注册更新处理的调度器 (官方 CubismUpdateScheduler).
/// </summary>
/// <remarks>
/// 官方的析构函数会 delete 所有已注册的 ICubismUpdater (持有所有权).
/// C# 侧由 GC 回收, 因此 Dispose 中只清空列表
/// (updater 只持有对效果类的引用, 并不拥有所有权).
/// </remarks>
public class CubismUpdateScheduler : IDisposable
{
    /// <summary>
    /// 已注册的更新处理列表
    /// </summary>
    private readonly List<ICubismUpdater> _cubismUpdatableList = [];

    /// <summary>
    /// 是否需要排序
    /// </summary>
    private bool _needsSort = true;

    /// <summary>
    /// 注册更新处理.
    /// </summary>
    /// <param name="updatable">要注册的更新处理. null 时忽略.</param>
    public void AddUpdatableList(ICubismUpdater? updatable)
    {
        if (updatable == null)
        {
            return;
        }

        _cubismUpdatableList.Add(updatable);
        _needsSort = true;
    }

    /// <summary>
    /// 按执行顺序对更新处理列表排序.
    /// </summary>
    /// <remarks>
    /// 必须使用稳定排序: LINQ 的 OrderBy 是稳定排序,
    /// 相同执行顺序的更新处理会保持注册顺序.
    /// List&lt;T&gt;.Sort 为不稳定排序, 禁止使用.
    /// </remarks>
    public void SortUpdatableList()
    {
        if (!_needsSort)
        {
            return;
        }

        var sorted = _cubismUpdatableList.OrderBy(static updater => updater.ExecutionOrder).ToList();

        _cubismUpdatableList.Clear();
        _cubismUpdatableList.AddRange(sorted);
        _needsSort = false;
    }

    /// <summary>
    /// 按执行顺序调用已注册的更新处理.
    /// </summary>
    /// <param name="model">更新目标的模型</param>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    public void OnLateUpdate(CubismModel model, float deltaTimeSeconds)
    {
        if (_needsSort)
        {
            SortUpdatableList();
        }

        for (int i = 0; i < _cubismUpdatableList.Count; ++i)
        {
            _cubismUpdatableList[i].OnLateUpdate(model, deltaTimeSeconds);
        }
    }

    /// <summary>
    /// 销毁已注册的更新处理.
    /// </summary>
    public void Dispose()
    {
        _cubismUpdatableList.Clear();
        _needsSort = false;
        GC.SuppressFinalize(this);
    }
}
