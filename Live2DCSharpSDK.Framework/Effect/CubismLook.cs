using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Effect;

/// <summary>
/// ターゲット（ドラッグ入力）にパラメータを追従させる機能（官方 5-r.5 の CubismLook）。
/// </summary>
/// <remarks>
/// P3 で CubismLookUpdater のコンパイル前置として先行投入し、P4 で最終形態に収口した。
/// 生成は <see cref="Create"/> のみ（コンストラクタは private）とし、二重の入口を作らない。
/// </remarks>
public class CubismLook
{
    /// <summary>
    /// look のパラメータ情報（官方 CubismLook::LookParameterData）。
    /// </summary>
    public record LookParameterData
    {
        /// <summary>
        /// パラメータID
        /// </summary>
        public required string ParameterId { get; set; }
        /// <summary>
        /// X軸方向のドラッグ入力に対する係数
        /// </summary>
        public float FactorX { get; set; }
        /// <summary>
        /// Y軸方向のドラッグ入力に対する係数
        /// </summary>
        public float FactorY { get; set; }
        /// <summary>
        /// X-Y（クロス）ドラッグ入力に対する係数
        /// </summary>
        public float FactorXY { get; set; }
    }

    /// <summary>
    /// look に紐づけるパラメータ情報のリスト
    /// </summary>
    private List<LookParameterData> _lookParameters = [];

    /// <summary>
    /// コンストラクタ（官方と同じく private。生成は <see cref="Create"/> を使う）。
    /// </summary>
    private CubismLook()
    {
    }

    /// <summary>
    /// インスタンスを作成する。
    /// </summary>
    /// <returns>作成したインスタンス</returns>
    public static CubismLook Create()
    {
        return new CubismLook();
    }

    /// <summary>
    /// インスタンスを破棄する。
    /// </summary>
    /// <param name="instance">破棄するインスタンス</param>
    /// <remarks>アンマネージドリソースを持たないため、C# 側では参照を切るだけ。</remarks>
    public static void Delete(CubismLook? instance)
    {
        // C# は GC が回収するため何もしない（官方の CSM_DELETE_SELF に相当）。
    }

    /// <summary>
    /// look のパラメータを設定する。
    /// </summary>
    /// <param name="lookParameters">設定するパラメータ情報のリスト</param>
    /// <remarks>官方は csmVector を値コピーするため、ここでもコピーして保持する。</remarks>
    public void SetParameters(IReadOnlyList<LookParameterData> lookParameters)
    {
        _lookParameters = [.. lookParameters];
    }

    /// <summary>
    /// look に紐づくパラメータ情報を取得する。
    /// </summary>
    /// <returns>設定されているパラメータ情報のリスト</returns>
    public IReadOnlyList<LookParameterData> GetParameters()
    {
        return _lookParameters;
    }

    /// <summary>
    /// モデルのパラメータを更新する。
    /// </summary>
    /// <param name="model">更新対象のモデル</param>
    /// <param name="dragX">ターゲットのX座標</param>
    /// <param name="dragY">ターゲットのY座標</param>
    public void UpdateParameters(CubismModel model, float dragX, float dragY)
    {
        float dragXY = dragX * dragY;

        for (int i = 0; i < _lookParameters.Count; ++i)
        {
            var data = _lookParameters[i];

            model.AddParameterValue(data.ParameterId,
                (data.FactorX * dragX) + (data.FactorY * dragY) + (data.FactorXY * dragXY));
        }
    }
}
