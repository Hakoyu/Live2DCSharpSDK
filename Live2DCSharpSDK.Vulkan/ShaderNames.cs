using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// Vulkan 用着色器名(展开自官方 Framework/Rendering/Vulkan/CubismRenderer_Vulkan.hpp:107-176 的 enum ShaderNames).
/// 官方宏展开共 494 项:
///   Copy(1) + SetupMask(1) + Normal/Add/Mult(18) + NormalAtop..NormalDisjointOver(24)
///   + 15 color × 5 alpha × 6 variant(450) = 494.
/// 另外 spv 文件数为 488(14 个基础 + 474 个 blend), Add/Mult 共用 Normal 的 6 个基础 shader.
/// </summary>
public static class ShaderNames
{
    /// <summary>复制用 shader</summary>
    public const int Copy = 0;

    /// <summary>遮罩生成用 shader</summary>
    public const int SetupMask = 1;

    // ----- Normal(5.2 以前的绘制方式) -----
    public const int Normal = 2;
    public const int NormalMasked = 3;
    public const int NormalMaskedInverted = 4;
    public const int NormalPremultipliedAlpha = 5;
    public const int NormalMaskedPremultipliedAlpha = 6;
    public const int NormalMaskedInvertedPremultipliedAlpha = 7;

    // ----- Add(shader 与 Normal 共用) -----
    public const int Add = 8;
    public const int AddMasked = 9;
    public const int AddMaskedInverted = 10;
    public const int AddPremultipliedAlpha = 11;
    public const int AddMaskedPremultipliedAlpha = 12;
    public const int AddMaskedInvertedPremultipliedAlpha = 13;

    // ----- Mult(shader 与 Normal 共用) -----
    public const int Mult = 14;
    public const int MultMasked = 15;
    public const int MultMaskedInverted = 16;
    public const int MultPremultipliedAlpha = 17;
    public const int MultMaskedPremultipliedAlpha = 18;
    public const int MultMaskedInvertedPremultipliedAlpha = 19;

    // ----- Normal + Over 以外的 alpha 混合(5.3 以后) -----
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

    /// <summary>混合组合(AddOver 以后)的起始值. 每种颜色为 5 alpha × 6 variant = 30 项.</summary>
    public const int BlendOverlapBegin = 44;

    /// <summary>遮罩/已乘 alpha 变种的数量(官方 CSM_CREATE_SHADER_NAMES 的 6 项)</summary>
    public const int VariantCount = 6;

    /// <summary>alpha 混合类型的数量(官方 AlphaBlendMode_Count)</summary>
    public const int AlphaBlendModeCount = 5;

    /// <summary>shader 总数(官方 ShaderNames_ShaderCount = 494)</summary>
    public const int ShaderCount = BlendOverlapBegin + 15 * AlphaBlendModeCount * VariantCount;

    /// <summary>
    /// 获取使用哪一个 shader(对应官方 GetShaderNamesBegin).
    /// 返回值为该混合组合中 "无遮罩" 变种的下标. 遮罩/PA 变种需再加上 offset 索引.
    /// </summary>
    /// <param name="blendMode">混合模式</param>
    /// <returns>使用的 shader 值</returns>
    public static int GetShaderNamesBegin(CsmBlendMode blendMode)
    {
        int colorBlendType = blendMode.GetColorBlendType();
        int alphaBlendType = blendMode.GetAlphaBlendType();

        if (colorBlendType == CsmEnum.CsmColorBlendType_Normal)
        {
            // Normal Over 时使用 5.2 以前的绘制方式
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

        // 5.3 以后: Core 的 color 3..17 对应 shader 内部的 ColorBlendMode 1..15
        int colorIndex = colorBlendType - 2;
        return BlendOverlapBegin
            + (colorIndex - 1) * AlphaBlendModeCount * VariantCount
            + alphaBlendType * VariantCount;
    }
}
