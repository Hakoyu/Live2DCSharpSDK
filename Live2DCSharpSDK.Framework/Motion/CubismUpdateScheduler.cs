using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 登録された更新処理を実行順に呼び出すスケジューラ（官方 CubismUpdateScheduler）。
/// </summary>
/// <remarks>
/// 官方はデストラクタで登録された ICubismUpdater を全て delete する（所有権あり）。
/// C# 側は GC が回収するため、Dispose ではリストを空にするだけにしている
/// （updater は効果クラスへの参照だけを保持しており、所有はしていない）。
/// </remarks>
public class CubismUpdateScheduler : IDisposable
{
    /// <summary>
    /// 登録された更新処理のリスト
    /// </summary>
    private readonly List<ICubismUpdater> _cubismUpdatableList = [];

    /// <summary>
    /// ソートが必要かどうか
    /// </summary>
    private bool _needsSort = true;

    /// <summary>
    /// 更新処理を登録する。
    /// </summary>
    /// <param name="updatable">登録する更新処理。null は無視する。</param>
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
    /// 更新処理のリストを実行順に並べ替える。
    /// </summary>
    /// <remarks>
    /// PLAN D-04: 安定ソートが必須。LINQ の OrderBy は安定ソートなので、
    /// 同じ実行順序の更新処理は登録順が保たれる。
    /// List&lt;T&gt;.Sort は不安定ソートなので使用禁止。
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
    /// 登録された更新処理を実行順に呼び出す。
    /// </summary>
    /// <param name="model">更新対象のモデル</param>
    /// <param name="deltaTimeSeconds">デルタ時間[秒]</param>
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
    /// 登録されている更新処理を破棄する。
    /// </summary>
    public void Dispose()
    {
        _cubismUpdatableList.Clear();
        _needsSort = false;
        GC.SuppressFinalize(this);
    }
}
