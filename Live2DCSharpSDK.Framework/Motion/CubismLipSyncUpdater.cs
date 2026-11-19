using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// リップシンクの更新処理（官方 CubismLipSyncUpdater）。
/// </summary>
/// <remarks>
/// 本轮不引入 wav 解码, 只提供从外部注入 IParameterProvider 的形式,
/// 默认使用 ConstantParameterProvider(始终返回 0), App 层不注册.
/// </remarks>
public class CubismLipSyncUpdater : ICubismUpdater
{
    /// <summary>
    /// 口型の値を提供するパラメータプロバイダ
    /// </summary>
    private readonly IParameterProvider _wavFileHandler;

    /// <summary>
    /// リップシンクを適用するパラメータIDのリスト
    /// </summary>
    private readonly IReadOnlyList<string> _lipSyncIds;

    /// <summary>
    /// コンストラクタ。実行順序は CubismUpdateOrder.LipSync。
    /// </summary>
    /// <param name="lipSyncIds">リップシンクを適用するパラメータIDのリスト</param>
    /// <param name="wavFileHandler">口型の値を提供するパラメータプロバイダ</param>
    public CubismLipSyncUpdater(IReadOnlyList<string> lipSyncIds, IParameterProvider wavFileHandler)
        : base((int)CubismUpdateOrder.LipSync)
    {
        _lipSyncIds = lipSyncIds;
        _wavFileHandler = wavFileHandler;
    }

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="lipSyncIds">リップシンクを適用するパラメータIDのリスト</param>
    /// <param name="wavFileHandler">口型の値を提供するパラメータプロバイダ</param>
    /// <param name="executionOrder">実行順序</param>
    public CubismLipSyncUpdater(IReadOnlyList<string> lipSyncIds, IParameterProvider wavFileHandler, int executionOrder)
        : base(executionOrder)
    {
        _lipSyncIds = lipSyncIds;
        _wavFileHandler = wavFileHandler;
    }

    /// <summary>
    /// 更新処理。
    /// </summary>
    /// <param name="model">更新対象のモデル</param>
    /// <param name="deltaTimeSeconds">デルタ時間[秒]</param>
    public override void OnLateUpdate(CubismModel model, float deltaTimeSeconds)
    {
        if (model == null)
        {
            return;
        }

        // リアルタイムでリップシンクを行う場合、システムから音量を取得して0〜1の範囲で値を入力する。
        _wavFileHandler.Update(deltaTimeSeconds);
        float value = _wavFileHandler.GetParameter();

        for (int i = 0; i < _lipSyncIds.Count; ++i)
        {
            model.AddParameterValue(_lipSyncIds[i], value, 0.8f);
        }
    }
}
