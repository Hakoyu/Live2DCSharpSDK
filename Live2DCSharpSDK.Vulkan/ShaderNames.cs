using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// Vulkan 用シェーダ名（官方 FW5/Vulkan/CubismRenderer_Vulkan.hpp:107-176 の enum ShaderNames を展開）。
/// 官方のマクロ展開は 494 項:
///   Copy(1) + SetupMask(1) + Normal/Add/Mult(18) + NormalAtop..NormalDisjointOver(24)
///   + 15 color × 5 alpha × 6 variant(450) = 494。
/// なお spv ファイル数は 488（14 基础 + 474 blend）で、Add/Mult は Normal の 6 個の基礎 shader を共用する。
/// </summary>
public static class ShaderNames
{
    /// <summary>コピー用シェーダ</summary>
    public const int Copy = 0;
    /// <summary>マスク生成用シェーダ</summary>
    public const int SetupMask = 1;

    // ----- Normal（5.2以前の描画方法）-----
    public const int Normal = 2;
    public const int NormalMasked = 3;
    public const int NormalMaskedInverted = 4;
    public const int NormalPremultipliedAlpha = 5;
    public const int NormalMaskedPremultipliedAlpha = 6;
    public const int NormalMaskedInvertedPremultipliedAlpha = 7;

    // ----- Add（shader は Normal と共用）-----
    public const int Add = 8;
    public const int AddMasked = 9;
    public const int AddMaskedInverted = 10;
    public const int AddPremultipliedAlpha = 11;
    public const int AddMaskedPremultipliedAlpha = 12;
    public const int AddMaskedInvertedPremultipliedAlpha = 13;

    // ----- Mult（shader は Normal と共用）-----
    public const int Mult = 14;
    public const int MultMasked = 15;
    public const int MultMaskedInverted = 16;
    public const int MultPremultipliedAlpha = 17;
    public const int MultMaskedPremultipliedAlpha = 18;
    public const int MultMaskedInvertedPremultipliedAlpha = 19;

    // ----- Normal + Over 以外のアルファブレンド（5.3以降）-----
    public const int NormalAtop = 20;
    public const int NormalAtopMasked = 21;
    public const int NormalAtopMaskedInverted = 22;
    public const int NormalAtopPremultipliedAlpha = 23;
    public const int NormalAtopMaskedPremultipliedAlpha = 24;
    public const int NormalAtopMaskedInvertedPremultipliedAlpha = 25;

    public const int NormalOut = 26;
    public const int NormalOutMasked = 27;
    public const int NormalOutMaskedInverted = 28;
    public const int NormalOutPremultipliedAlpha = 29;
    public const int NormalOutMaskedPremultipliedAlpha = 30;
    public const int NormalOutMaskedInvertedPremultipliedAlpha = 31;

    public const int NormalConjointOver = 32;
    public const int NormalConjointOverMasked = 33;
    public const int NormalConjointOverMaskedInverted = 34;
    public const int NormalConjointOverPremultipliedAlpha = 35;
    public const int NormalConjointOverMaskedPremultipliedAlpha = 36;
    public const int NormalConjointOverMaskedInvertedPremultipliedAlpha = 37;

    public const int NormalDisjointOver = 38;
    public const int NormalDisjointOverMasked = 39;
    public const int NormalDisjointOverMaskedInverted = 40;
    public const int NormalDisjointOverPremultipliedAlpha = 41;
    public const int NormalDisjointOverMaskedPremultipliedAlpha = 42;
    public const int NormalDisjointOverMaskedInvertedPremultipliedAlpha = 43;

    /// <summary>ブレンド組合せ（AddOver 以降）の先頭。各色は 5 alpha × 6 variant = 30 項。</summary>
    public const int BlendOverlapBegin = 44;

    /// <summary>マスク/乗算済アルファ変種の数（官方 CSM_CREATE_SHADER_NAMES の 6 項）</summary>
    public const int VariantCount = 6;
    /// <summary>アルファブレンド種別の数（官方 AlphaBlendMode_Count）</summary>
    public const int AlphaBlendModeCount = 5;

    /// <summary>シェーダ総数（官方 ShaderNames_ShaderCount = 494）</summary>
    public const int ShaderCount = BlendOverlapBegin + 15 * AlphaBlendModeCount * VariantCount;

    /// <summary>
    /// どのシェーダーを利用するかを取得する（官方 GetShaderNamesBegin）。
    /// 返り値はそのブレンド組合せの「マスク無」変種の添字。マスク/PA 変種は +offset で引く。
    /// </summary>
    /// <param name="blendMode">ブレンドモード</param>
    /// <returns>使用するシェーダの値</returns>
    public static int GetShaderNamesBegin(CsmBlendMode blendMode)
    {
        int colorBlendType = blendMode.GetColorBlendType();
        int alphaBlendType = blendMode.GetAlphaBlendType();

        if (colorBlendType == CsmEnum.CsmColorBlendType_Normal)
        {
            // Normal Over のときは 5.2 以前の描画方法を利用する
            return alphaBlendType switch
            {
                CsmEnum.CsmAlphaBlendType_Atop => NormalAtop,
                CsmEnum.CsmAlphaBlendType_Out => NormalOut,
                CsmEnum.CsmAlphaBlendType_ConjointOver => NormalConjointOver,
                CsmEnum.CsmAlphaBlendType_DisjointOver => NormalDisjointOver,
                _ => Normal,
            };
        }

        if (colorBlendType == CsmEnum.CsmColorBlendType_AddCompatible)
        {
            return Add;
        }

        if (colorBlendType == CsmEnum.CsmColorBlendType_MultiplyCompatible)
        {
            return Mult;
        }

        // 5.3 以降: Core の color 3..17 は shader 内部の ColorBlendMode 1..15 に対応する
        int colorIndex = colorBlendType - 2;
        return BlendOverlapBegin + (colorIndex - 1) * AlphaBlendModeCount * VariantCount
               + alphaBlendType * VariantCount;
    }

}
