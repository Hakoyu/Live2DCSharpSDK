namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// 颜色混合的模式
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
    /// 遮罩
    /// </summary>
    Mask = 3,
    /// <summary>
    /// 复制 (R5 中追加. 用于 offscreen 合成. CubismRenderer.hpp:48)
    /// </summary>
    Copy = 4,
};
