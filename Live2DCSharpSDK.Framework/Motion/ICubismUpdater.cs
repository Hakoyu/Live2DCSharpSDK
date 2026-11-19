using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 更新処理の実行順序（官方 ICubismUpdater.hpp:12-22 の CubismUpdateOrder）。
/// </summary>
public enum CubismUpdateOrder : int
{
    /// <summary>自動まばたき</summary>
    EyeBlink = 200,
    /// <summary>表情</summary>
    Expression = 300,
    /// <summary>ドラッグ（顔の向き）</summary>
    Look = 400,
    /// <summary>呼吸</summary>
    Breath = 500,
    /// <summary>物理演算</summary>
    Physics = 600,
    /// <summary>リップシンク</summary>
    LipSync = 700,
    /// <summary>ポーズ</summary>
    Pose = 800,
    /// <summary>未指定（最後に実行）</summary>
    Max = int.MaxValue
}

/// <summary>
/// CubismUpdateScheduler で管理される更新処理の抽象基底クラス（官方 ICubismUpdater）。
/// </summary>
public abstract class ICubismUpdater
{
    /// <summary>
    /// 実行順序
    /// </summary>
    private readonly int _executionOrder;

    /// <summary>
    /// 実行順序を指定せずに生成する。実行順序は CubismUpdateOrder.Max になる。
    /// </summary>
    protected ICubismUpdater()
        : this((int)CubismUpdateOrder.Max)
    {
    }

    /// <summary>
    /// 実行順序を指定して生成する。
    /// </summary>
    /// <param name="executionOrder">実行順序</param>
    protected ICubismUpdater(int executionOrder)
    {
        _executionOrder = executionOrder;
    }

    /// <summary>
    /// 実行順序（官方の private _executionOrder に対応。ソート用）。
    /// </summary>
    internal int ExecutionOrder => _executionOrder;

    /// <summary>
    /// ソート用の比較関数（官方 ICubismUpdater::SortFunction）。
    /// </summary>
    /// <param name="left">比較する更新処理1</param>
    /// <param name="right">比較する更新処理2</param>
    /// <returns>left を right より先に実行するなら true</returns>
    public static bool SortFunction(ICubismUpdater left, ICubismUpdater right)
    {
        return left._executionOrder < right._executionOrder;
    }

    /// <summary>
    /// 更新処理。
    /// </summary>
    /// <param name="model">更新対象のモデル</param>
    /// <param name="deltaTimeSeconds">デルタ時間[秒]</param>
    public abstract void OnLateUpdate(CubismModel model, float deltaTimeSeconds);
}
