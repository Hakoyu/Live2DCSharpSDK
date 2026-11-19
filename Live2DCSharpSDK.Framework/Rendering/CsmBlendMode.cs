using Live2DCSharpSDK.Framework.Core;

namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// 定义混合模式的类 (官方 Framework/src/Rendering/csmBlendMode.hpp:20-87 / csmBlendMode.cpp:13-111).
/// 与既有的 CubismBlendMode 枚举并存, 不做替换.
/// 官方返回的 csmString 改为 static readonly string[] 的索引引用.
/// </summary>
public struct CsmBlendMode
{
    /// <summary>
    /// 颜色混合种类的字符串表 (index = CsmEnum.CsmColorBlendType_*)
    /// </summary>
    private static readonly string[] ColorBlendTypeStrings =
    [
        "Normal",             // CsmColorBlendType_Normal (0)
        "AddCompatible",      // CsmColorBlendType_AddCompatible (1)
        "MultiplyCompatible", // CsmColorBlendType_MultiplyCompatible (2)
        "Add",                // CsmColorBlendType_Add (3)
        "AddGlow",            // CsmColorBlendType_AddGlow (4)
        "Darken",             // CsmColorBlendType_Darken (5)
        "Multiply",           // CsmColorBlendType_Multiply (6)
        "ColorBurn",          // CsmColorBlendType_ColorBurn (7)
        "LinearBurn",         // CsmColorBlendType_LinearBurn (8)
        "Lighten",            // CsmColorBlendType_Lighten (9)
        "Screen",             // CsmColorBlendType_Screen (10)
        "ColorDodge",         // CsmColorBlendType_ColorDodge (11)
        "Overlay",            // CsmColorBlendType_Overlay (12)
        "SoftLight",          // CsmColorBlendType_SoftLight (13)
        "HardLight",          // CsmColorBlendType_HardLight (14)
        "LinearLight",        // CsmColorBlendType_LinearLight (15)
        "Hue",                // CsmColorBlendType_Hue (16)
        "Color",              // CsmColorBlendType_Color (17)
    ];

    /// <summary>
    /// Alpha 混合种类的字符串表 (index = CsmEnum.CsmAlphaBlendType_*)
    /// </summary>
    private static readonly string[] AlphaBlendTypeStrings =
    [
        "Over",         // CsmAlphaBlendType_Over (0)
        "Atop",         // CsmAlphaBlendType_Atop (1)
        "Out",          // CsmAlphaBlendType_Out (2)
        "ConjointOver", // CsmAlphaBlendType_ConjointOver (3)
        "DisjointOver", // CsmAlphaBlendType_DisjointOver (4)
    ];

    /// <summary>
    /// 颜色混合种类
    /// </summary>
    private int _colorBlendType;
    /// <summary>
    /// Alpha 混合种类
    /// </summary>
    private int _alphaBlendType;

    /// <summary>
    /// 构造函数 (用 Normal / Over 初始化)
    /// </summary>
    public CsmBlendMode()
    {
        _colorBlendType = CsmEnum.CsmColorBlendType_Normal;
        _alphaBlendType = CsmEnum.CsmAlphaBlendType_Over;
    }

    /// <summary>
    /// 从 blendMode 值生成 (低 8 bit = 颜色, 接下来的 8 bit = Alpha)
    /// </summary>
    /// <param name="blendMode">blendMode 値</param>
    public CsmBlendMode(int blendMode) : this()
    {
        SetBlendMode(blendMode);
    }

    /// <summary>
    /// 把颜色混合种类转换为字符串 (官方 csmBlendMode::ColorBlendModeToString)
    /// </summary>
    /// <param name="type">颜色混合种类</param>
    /// <returns>混合名. 未知的值返回空字符串</returns>
    public static string ColorBlendModeToString(int type)
    {
        return (uint)type < (uint)ColorBlendTypeStrings.Length ? ColorBlendTypeStrings[type] : string.Empty;
    }

    /// <summary>
    /// 把 Alpha 混合种类转换为字符串 (官方 csmBlendMode::AlphaBlendModeToString)
    /// </summary>
    /// <param name="type">Alpha 混合种类</param>
    /// <returns>混合名. 未知的值返回空字符串</returns>
    public static string AlphaBlendModeToString(int type)
    {
        return (uint)type < (uint)AlphaBlendTypeStrings.Length ? AlphaBlendTypeStrings[type] : string.Empty;
    }

    /// <summary>
    /// 设置 blendMode 值 (官方 csmBlendMode::SetBlendMode)
    /// </summary>
    /// <param name="blendMode">低 8 bit 为颜色, 接下来的 8 bit 为 Alpha</param>
    public void SetBlendMode(int blendMode)
    {
        _colorBlendType = blendMode & 0xFF;
        _alphaBlendType = (blendMode >> 8) & 0xFF;
    }

    /// <summary>
    /// 获取颜色混合种类
    /// </summary>
    public readonly int GetColorBlendType()
    {
        return _colorBlendType;
    }

    /// <summary>
    /// 获取 Alpha 混合种类
    /// </summary>
    public readonly int GetAlphaBlendType()
    {
        return _alphaBlendType;
    }

    /// <summary>
    /// 获取颜色混合种类的字符串
    /// </summary>
    public readonly string GetColorBlendTypeString()
    {
        return ColorBlendModeToString(_colorBlendType);
    }

    /// <summary>
    /// 获取 Alpha 混合种类的字符串
    /// </summary>
    public readonly string GetAlphaBlendTypeString()
    {
        return AlphaBlendModeToString(_alphaBlendType);
    }
}
