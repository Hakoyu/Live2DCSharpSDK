using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// OpenGL 用的 Shader 名称 (官方 CubismShader_OpenGLES2.cpp:48-117 的 enum ShaderNames 展开).
/// 官方用宏生成 494 项 (Copy + SetupMask + Normal/Add/Mult 各 6 项 + 5.3 以后的混合组合),
/// C# 没有宏, 因此用常量加计算的方式表达相同排列.
/// 由于要用数组下标与 offset 加法 (掩码/预乘 Alpha 变种) 访问, 排列顺序与官方完全一致.
/// </summary>
internal static class ShaderNames
{
    /// <summary>
    /// 复制用 Shader
    /// </summary>
    public const int Copy = 0;

    /// <summary>
    /// 掩码生成用 Shader
    /// </summary>
    public const int SetupMask = 1;

    // ----- Normal (5.2 以前的绘制方式) -----
    /// <summary>
    /// 普通 (无 Mask)
    /// </summary>
    public const int Normal = 2;

    /// <summary>
    /// 普通 (Clipping)
    /// </summary>
    public const int NormalMasked = 3;

    /// <summary>
    /// 普通 (Clipping, 反相)
    /// </summary>
    public const int NormalMaskedInverted = 4;

    /// <summary>
    /// 普通 (预乘 Alpha 对应)
    /// </summary>
    public const int NormalPremultipliedAlpha = 5;

    /// <summary>
    /// 普通 (Clipping, 预乘 Alpha 对应)
    /// </summary>
    public const int NormalMaskedPremultipliedAlpha = 6;

    /// <summary>
    /// 普通 (Clipping, 反相, 预乘 Alpha 对应)
    /// </summary>
    public const int NormalMaskedInvertedPremultipliedAlpha = 7;

    // ----- Add (Shader 与 Normal 共用) -----
    public const int Add = 8;
    public const int AddMasked = 9;
    public const int AddMaskedInverted = 10;
    public const int AddPremultipliedAlpha = 11;
    public const int AddMaskedPremultipliedAlpha = 12;
    public const int AddMaskedInvertedPremultipliedAlpha = 13;

    // ----- Mult (Shader 与 Normal 共用) -----
    public const int Mult = 14;
    public const int MultMasked = 15;
    public const int MultMaskedInverted = 16;
    public const int MultPremultipliedAlpha = 17;
    public const int MultMaskedPremultipliedAlpha = 18;
    public const int MultMaskedInvertedPremultipliedAlpha = 19;

    // ----- Normal + Over 以外的 Alpha 混合 (5.3 以后) -----
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

    /// <summary>
    /// 混合模式组合 (Add/AddGlow/.../Color × Over/Atop/Out/ConjointOver/DisjointOver) 的开头.
    /// 每种颜色为 5 种 Alpha × 6 变种 = 30 项.
    /// </summary>
    public const int BlendOverlapBegin = 44;

    /// <summary>
    /// 掩码/预乘 Alpha 变种的数量 (官方 CSM_CREATE_SHADER_NAMES 的 6 项)
    /// </summary>
    public const int VariantCount = 6;

    /// <summary>
    /// Alpha 混合种类的数量 (官方 AlphaBlendMode_Count)
    /// </summary>
    public const int AlphaBlendModeCount = 5;

    /// <summary>
    /// Shader 总数 (官方 ShaderNames_ShaderCount = 494)
    /// </summary>
    public const int ShaderCount = BlendOverlapBegin + 15 * AlphaBlendModeCount * VariantCount;

    /// <summary>
    /// 获取使用哪个 Shader (官方 GetShaderNamesBegin).
    /// 返回值为该混合组合的 "无 Mask" 变种下标. 掩码/PA 变种用 +offset 取得.
    /// </summary>
    /// <param name="blendMode">混合模式</param>
    /// <returns>使用的 Shader 值</returns>
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
            // AddCompatible 使用 5.2 以前的绘制方式
            return Add;
        }

        if (colorBlendType == CsmEnum.CsmColorBlendType_MultiplyCompatible)
        {
            // MultiplyCompatible 使用 5.2 以前的绘制方式
            return Mult;
        }

        // 5.3 以后: Core 的 color 3..17 对应 Shader 内部的 ColorBlendMode 1..15
        int colorIndex = colorBlendType - 2;
        return BlendOverlapBegin
            + (colorIndex - 1) * AlphaBlendModeCount * VariantCount
            + alphaBlendType * VariantCount;
    }
}
