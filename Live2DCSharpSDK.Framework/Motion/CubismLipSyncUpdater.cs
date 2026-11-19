using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 唇形同步的更新处理 (官方 CubismLipSyncUpdater).
/// </summary>
/// <remarks>
/// 本轮不引入 wav 解码, 只提供从外部注入 IParameterProvider 的形式,
/// 默认使用 ConstantParameterProvider(始终返回 0), App 层不注册.
/// </remarks>
public class CubismLipSyncUpdater : ICubismUpdater
{
    /// <summary>
    /// 提供口型值的参数提供者
    /// </summary>
    private readonly IParameterProvider _wavFileHandler;

    /// <summary>
    /// 应用唇形同步的参数 ID 列表
    /// </summary>
    private readonly IReadOnlyList<string> _lipSyncIds;

    /// <summary>
    /// 构造函数. 执行顺序为 CubismUpdateOrder.LipSync.
    /// </summary>
    /// <param name="lipSyncIds">应用唇形同步的参数 ID 列表</param>
    /// <param name="wavFileHandler">提供口型值的参数提供者</param>
    public CubismLipSyncUpdater(IReadOnlyList<string> lipSyncIds, IParameterProvider wavFileHandler)
        : base((int)CubismUpdateOrder.LipSync)
    {
        _lipSyncIds = lipSyncIds;
        _wavFileHandler = wavFileHandler;
    }

    /// <summary>
    /// 构造函数.
    /// </summary>
    /// <param name="lipSyncIds">应用唇形同步的参数 ID 列表</param>
    /// <param name="wavFileHandler">提供口型值的参数提供者</param>
    /// <param name="executionOrder">执行顺序</param>
    public CubismLipSyncUpdater(
        IReadOnlyList<string> lipSyncIds,
        IParameterProvider wavFileHandler,
        int executionOrder
    )
        : base(executionOrder)
    {
        _lipSyncIds = lipSyncIds;
        _wavFileHandler = wavFileHandler;
    }

    /// <summary>
    /// 更新处理.
    /// </summary>
    /// <param name="model">更新目标的模型</param>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    public override void OnLateUpdate(CubismModel model, float deltaTimeSeconds)
    {
        if (model == null)
        {
            return;
        }

        // 实时进行唇形同步时, 从系统获取音量并输入 0 到 1 范围内的值.
        _wavFileHandler.Update(deltaTimeSeconds);
        float value = _wavFileHandler.GetParameter();

        for (int i = 0; i < _lipSyncIds.Count; ++i)
        {
            model.AddParameterValue(_lipSyncIds[i], value, 0.8f);
        }
    }
}
