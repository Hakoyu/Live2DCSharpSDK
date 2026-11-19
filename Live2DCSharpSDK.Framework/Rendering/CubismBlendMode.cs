namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// カラーブレンディングのモード
/// </summary>
public enum CubismBlendMode
{
    /// <summary>
    /// 通常
    /// </summary>
    Normal = 0,
    /// <summary>
    /// 加算
    /// </summary>
    Additive = 1,
    /// <summary>
    /// 乗算
    /// </summary>
    Multiplicative = 2,
    /// <summary>
    /// マスク
    /// </summary>
    Mask = 3,
    /// <summary>
    /// コピー（R5 で追加。offscreen 合成用。CubismRenderer.hpp:48）
    /// </summary>
    Copy = 4,
};
