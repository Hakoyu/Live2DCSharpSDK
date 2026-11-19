namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// パラメータ値を提供するインターフェース（官方 IParameterProvider）。
/// 口型（リップシンク）用の音声入力を CubismLipSyncUpdater に注入するために使用する。
/// </summary>
public interface IParameterProvider
{
    /// <summary>
    /// 更新処理。
    /// </summary>
    /// <returns>更新に成功したら true</returns>
    bool Update();

    /// <summary>
    /// 更新処理。
    /// </summary>
    /// <param name="deltaTimeSeconds">デルタ時間[秒]</param>
    /// <returns>更新に成功したら true</returns>
    bool Update(float deltaTimeSeconds);

    /// <summary>
    /// 現在のパラメータ値を取得する。
    /// </summary>
    /// <returns>パラメータ値</returns>
    float GetParameter();
}

/// <summary>
/// 始终返回 0 的 IParameterProvider 最小实现.
/// 本輪は wav デコードを導入しないため、口型は常に 0 のままとする。
/// </summary>
public sealed class ConstantParameterProvider : IParameterProvider
{
    /// <summary>
    /// 更新処理（何もしない）。
    /// </summary>
    /// <returns>常に false</returns>
    public bool Update()
    {
        return false;
    }

    /// <summary>
    /// 更新処理（何もしない）。
    /// </summary>
    /// <param name="deltaTimeSeconds">デルタ時間[秒]</param>
    /// <returns>常に false</returns>
    public bool Update(float deltaTimeSeconds)
    {
        return false;
    }

    /// <summary>
    /// 現在のパラメータ値を取得する。
    /// </summary>
    /// <returns>常に 0</returns>
    public float GetParameter()
    {
        return 0.0f;
    }
}
