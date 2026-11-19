using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Motion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Motion;

/// <summary>
/// A7: CubismUpdateScheduler 的排序与注册语义.
///
/// 更新处理用不读模型的 mock 记录调用顺序; OnLateUpdate 需要 CubismModel 参数,
/// 这里传入以 IntPtr.Zero 构造的惰性模型 (CubismModel 的 Core 初始化在句柄为零时直接返回),
/// 因此不会加载 Core, 也不会解引用空指针, 不需要用 null 硬塞参数.
/// </summary>
[TestClass]
public sealed class CubismUpdateSchedulerTests
{
    /// <summary>
    /// 按执行顺序排序, 相同执行顺序保持注册顺序 (稳定排序).
    /// </summary>
    [TestMethod]
    public void SortsByExecutionOrderAndKeepsRegistrationOrder()
    {
        var calls = new List<string>();
        using var scheduler = new CubismUpdateScheduler();
        scheduler.AddUpdatableList(new RecordingUpdater(calls, "breath-a", 500));
        scheduler.AddUpdatableList(new RecordingUpdater(calls, "blink", 200));
        scheduler.AddUpdatableList(new RecordingUpdater(calls, "breath-b", 500));
        scheduler.AddUpdatableList(new RecordingUpdater(calls, "expression", 300));

        UpdateOnce(scheduler);

        CollectionAssert.AreEqual(new[] { "blink", "expression", "breath-a", "breath-b" }, calls);
    }

    /// <summary>
    /// 同一执行顺序的多个更新处理在任何规模下都保持注册顺序.
    /// </summary>
    [TestMethod]
    public void ManyUpdatersWithSameOrderKeepRegistrationOrder()
    {
        var calls = new List<string>();
        using var scheduler = new CubismUpdateScheduler();

        for (int i = 0; i < 10; i++)
        {
            scheduler.AddUpdatableList(new RecordingUpdater(calls, $"same-{i}", 400));
        }

        UpdateOnce(scheduler);

        CollectionAssert.AreEqual(
            Enumerable.Range(0, 10).Select(i => $"same-{i}").ToArray(),
            calls
        );
    }

    /// <summary>
    /// 重建 40 次结果不摇摆: 每次调用都得到同一顺序.
    /// </summary>
    [TestMethod]
    public void RebuildFortyTimesKeepsOrderStable()
    {
        string[] expected = ["blink", "expression", "breath-a", "breath-b"];

        for (int round = 0; round < 40; round++)
        {
            var calls = new List<string>();
            using var scheduler = new CubismUpdateScheduler();
            scheduler.AddUpdatableList(new RecordingUpdater(calls, "breath-a", 500));
            scheduler.AddUpdatableList(new RecordingUpdater(calls, "blink", 200));
            scheduler.AddUpdatableList(new RecordingUpdater(calls, "breath-b", 500));
            scheduler.AddUpdatableList(new RecordingUpdater(calls, "expression", 300));

            UpdateOnce(scheduler);

            CollectionAssert.AreEqual(expected, calls, $"round={round}");
        }
    }

    /// <summary>
    /// null 更新处理被忽略, 不影响已有注册与顺序.
    /// </summary>
    [TestMethod]
    public void NullUpdatableIsIgnored()
    {
        var calls = new List<string>();
        using var scheduler = new CubismUpdateScheduler();
        scheduler.AddUpdatableList(new RecordingUpdater(calls, "breath", 500));
        scheduler.AddUpdatableList(null);
        scheduler.AddUpdatableList(new RecordingUpdater(calls, "blink", 200));

        UpdateOnce(scheduler);

        CollectionAssert.AreEqual(new[] { "blink", "breath" }, calls);
    }

    /// <summary>
    /// 排序后新增更新处理会重新排序 (懒排序标记正确复位).
    /// </summary>
    [TestMethod]
    public void AddAfterUpdateTriggersResort()
    {
        var calls = new List<string>();
        using var scheduler = new CubismUpdateScheduler();
        scheduler.AddUpdatableList(new RecordingUpdater(calls, "breath", 500));

        UpdateOnce(scheduler);
        scheduler.AddUpdatableList(new RecordingUpdater(calls, "blink", 200));
        UpdateOnce(scheduler);

        CollectionAssert.AreEqual(new[] { "breath", "blink", "breath" }, calls);
    }

    /// <summary>
    /// 重复调用 SortUpdatableList 不改变顺序.
    /// </summary>
    [TestMethod]
    public void SortUpdatableListIsIdempotent()
    {
        var calls = new List<string>();
        using var scheduler = new CubismUpdateScheduler();
        scheduler.AddUpdatableList(new RecordingUpdater(calls, "breath", 500));
        scheduler.AddUpdatableList(new RecordingUpdater(calls, "blink", 200));

        scheduler.SortUpdatableList();
        scheduler.SortUpdatableList();
        UpdateOnce(scheduler);

        CollectionAssert.AreEqual(new[] { "blink", "breath" }, calls);
    }

    /// <summary>
    /// 空调度器调用 OnLateUpdate 不抛异常.
    /// </summary>
    [TestMethod]
    public void EmptySchedulerDoesNothing()
    {
        using var scheduler = new CubismUpdateScheduler();

        UpdateOnce(scheduler);
    }

    /// <summary>
    /// Dispose 清空列表: 之后的调用不再触发处理, 且可以继续注册复用.
    /// </summary>
    [TestMethod]
    public void DisposeClearsAndAllowsReuse()
    {
        var calls = new List<string>();
        var scheduler = new CubismUpdateScheduler();
        scheduler.AddUpdatableList(new RecordingUpdater(calls, "first", 300));

        UpdateOnce(scheduler);
        scheduler.Dispose();

        // Dispose 之后列表已清空, 再调用不应触发任何处理.
        UpdateOnce(scheduler);
        CollectionAssert.AreEqual(new[] { "first" }, calls);

        // 清空后的调度器可以继续注册并正常排序.
        scheduler.AddUpdatableList(new RecordingUpdater(calls, "second", 100));
        UpdateOnce(scheduler);
        CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
    }

    /// <summary>
    /// 模型实例与增量时间原样传给每个更新处理 (模型非 null).
    /// </summary>
    [TestMethod]
    public void PassesModelAndDeltaTimeToEveryUpdater()
    {
        var calls = new List<string>();
        var first = new RecordingUpdater(calls, "first", 100);
        var second = new RecordingUpdater(calls, "second", 200);
        using var scheduler = new CubismUpdateScheduler();
        scheduler.AddUpdatableList(first);
        scheduler.AddUpdatableList(second);

        using CubismModel model = CreateInertModel();
        scheduler.OnLateUpdate(model, 0.25f);

        Assert.IsTrue(ReferenceEquals(model, first.LastModel));
        Assert.IsTrue(ReferenceEquals(model, second.LastModel));
        Assert.AreEqual(0.25f, first.LastDeltaTimeSeconds, 1e-6f);
        Assert.AreEqual(0.25f, second.LastDeltaTimeSeconds, 1e-6f);
    }

    /// <summary>
    /// 构造一个不持有任何 Core 资源的惰性模型: 句柄为 IntPtr.Zero 时 CubismModel 的
    /// Core 初始化会直接返回, 因此可以安全地当作参数传递.
    /// </summary>
    /// <returns>惰性模型实例</returns>
    private static CubismModel CreateInertModel()
    {
        return new CubismModel(CubismContext.Default, IntPtr.Zero);
    }

    /// <summary>
    /// 以惰性模型驱动一次更新.
    /// </summary>
    /// <param name="scheduler">调度器</param>
    private static void UpdateOnce(CubismUpdateScheduler scheduler)
    {
        using CubismModel model = CreateInertModel();
        scheduler.OnLateUpdate(model, 1.0f / 30.0f);
    }

    /// <summary>
    /// 记录调用顺序与参数的更新处理 mock, 不访问模型内容.
    /// </summary>
    private sealed class RecordingUpdater : ICubismUpdater
    {
        private readonly List<string> _calls;
        private readonly string _name;

        /// <summary>
        /// 以指定执行顺序创建.
        /// </summary>
        /// <param name="calls">共享的调用记录</param>
        /// <param name="name">调用记录里使用的名字</param>
        /// <param name="executionOrder">执行顺序</param>
        public RecordingUpdater(List<string> calls, string name, int executionOrder)
            : base(executionOrder)
        {
            _calls = calls;
            _name = name;
        }

        /// <summary>
        /// 最近一次收到的模型.
        /// </summary>
        public CubismModel? LastModel { get; private set; }

        /// <summary>
        /// 最近一次收到的增量时间[秒].
        /// </summary>
        public float LastDeltaTimeSeconds { get; private set; }

        /// <summary>
        /// 记录调用, 不读取模型.
        /// </summary>
        /// <param name="model">更新目标的模型</param>
        /// <param name="deltaTimeSeconds">增量时间[秒]</param>
        public override void OnLateUpdate(CubismModel model, float deltaTimeSeconds)
        {
            _calls.Add(_name);
            LastModel = model;
            LastDeltaTimeSeconds = deltaTimeSeconds;
        }
    }
}
