using Live2DCSharpSDK.Framework.Core;

namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// ブレンドモードを定義するクラス（官方 Framework/src/Rendering/csmBlendMode.hpp:20-87 / csmBlendMode.cpp:13-111）。
/// 与既有的 CubismBlendMode 枚举并存, 不做替换.
/// 官方返回的 csmString 改为 static readonly string[] 的索引引用.
/// </summary>
public struct CsmBlendMode
{
    /// <summary>
    /// カラーブレンド種別の文字列テーブル（index = CsmEnum.CsmColorBlendType_*）
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
    /// アルファブレンド種別の文字列テーブル（index = CsmEnum.CsmAlphaBlendType_*）
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
    /// カラーブレンド種別
    /// </summary>
    private int _colorBlendType;
    /// <summary>
    /// アルファブレンド種別
    /// </summary>
    private int _alphaBlendType;

    /// <summary>
    /// コンストラクタ（Normal / Over で初期化）
    /// </summary>
    public CsmBlendMode()
    {
        _colorBlendType = CsmEnum.CsmColorBlendType_Normal;
        _alphaBlendType = CsmEnum.CsmAlphaBlendType_Over;
    }

    /// <summary>
    /// blendMode 値から生成する（下位 8 bit = カラー、次の 8 bit = アルファ）
    /// </summary>
    /// <param name="blendMode">blendMode 値</param>
    public CsmBlendMode(int blendMode) : this()
    {
        SetBlendMode(blendMode);
    }

    /// <summary>
    /// カラーブレンド種別を文字列に変換する（官方 csmBlendMode::ColorBlendModeToString）
    /// </summary>
    /// <param name="type">カラーブレンド種別</param>
    /// <returns>ブレンド名。未知の値は空文字列</returns>
    public static string ColorBlendModeToString(int type)
    {
        return (uint)type < (uint)ColorBlendTypeStrings.Length ? ColorBlendTypeStrings[type] : string.Empty;
    }

    /// <summary>
    /// アルファブレンド種別を文字列に変換する（官方 csmBlendMode::AlphaBlendModeToString）
    /// </summary>
    /// <param name="type">アルファブレンド種別</param>
    /// <returns>ブレンド名。未知の値は空文字列</returns>
    public static string AlphaBlendModeToString(int type)
    {
        return (uint)type < (uint)AlphaBlendTypeStrings.Length ? AlphaBlendTypeStrings[type] : string.Empty;
    }

    /// <summary>
    /// blendMode 値を設定する（官方 csmBlendMode::SetBlendMode）
    /// </summary>
    /// <param name="blendMode">下位 8 bit がカラー、次の 8 bit がアルファ</param>
    public void SetBlendMode(int blendMode)
    {
        _colorBlendType = blendMode & 0xFF;
        _alphaBlendType = (blendMode >> 8) & 0xFF;
    }

    /// <summary>
    /// カラーブレンド種別を取得する
    /// </summary>
    public readonly int GetColorBlendType()
    {
        return _colorBlendType;
    }

    /// <summary>
    /// アルファブレンド種別を取得する
    /// </summary>
    public readonly int GetAlphaBlendType()
    {
        return _alphaBlendType;
    }

    /// <summary>
    /// カラーブレンド種別の文字列を取得する
    /// </summary>
    public readonly string GetColorBlendTypeString()
    {
        return ColorBlendModeToString(_colorBlendType);
    }

    /// <summary>
    /// アルファブレンド種別の文字列を取得する
    /// </summary>
    public readonly string GetAlphaBlendTypeString()
    {
        return AlphaBlendModeToString(_alphaBlendType);
    }
}
