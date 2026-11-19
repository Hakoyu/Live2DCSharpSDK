namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 提供参数值的接口 (官方 IParameterProvider).
/// 用于将口型 (唇形同步) 用的音频输入注入到 CubismLipSyncUpdater 中.
/// </summary>
public interface IParameterProvider
{
    /// <summary>
    /// 更新处理.
    /// </summary>
    /// <returns>更新成功则为 true</returns>
    bool Update();

    /// <summary>
    /// 更新处理.
    /// </summary>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    /// <returns>更新成功则为 true</returns>
    bool Update(float deltaTimeSeconds);

    /// <summary>
    /// 获取当前的参数值.
    /// </summary>
    /// <returns>参数值</returns>
    float GetParameter();
}

/// <summary>
/// 始终返回 0 的 IParameterProvider 最小实现.
/// 本轮不引入 wav 解码, 因此口型始终保持为 0.
/// </summary>
public sealed class ConstantParameterProvider : IParameterProvider
{
    /// <summary>
    /// 更新处理 (不做任何操作).
    /// </summary>
    /// <returns>始终为 false</returns>
    public bool Update()
    {
        return false;
    }

    /// <summary>
    /// 更新处理 (不做任何操作).
    /// </summary>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    /// <returns>始终为 false</returns>
    public bool Update(float deltaTimeSeconds)
    {
        return false;
    }

    /// <summary>
    /// 获取当前的参数值.
    /// </summary>
    /// <returns>始终为 0</returns>
    public float GetParameter()
    {
        return 0.0f;
    }
}
