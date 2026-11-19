using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

public class CubismPipeline_Vulkan(Vk vk, Device device) : IDisposable
{
    /// <summary>
    /// 官方 ShaderNames_ShaderCount = 494.
    /// </summary>
    private const int ShaderCount = ShaderNames.ShaderCount;

    private PipelineResource[]? _pipelineResource;

    /// <summary>
    /// 为每个 shader 创建 PipelineResource 实例
    /// (对应官方 CubismRenderer_Vulkan.cpp:643-735 CreatePipelines).
    /// </summary>
    /// <param name="descriptorSetLayout">descriptor set layout</param>
    public void CreatePipelines(DescriptorSetLayout descriptorSetLayout)
    {
        // 已创建 pipeline 时无需再次生成
        if (_pipelineResource != null)
        {
            return;
        }


        _pipelineResource = new PipelineResource[ShaderCount];

        for (int i = 0; i < ShaderCount; i++)
        {
            // 加法与乘法引用与通常相同的 PipelineResource(alias 在下方设置)
            if (i >= ShaderNames.Add && i <= ShaderNames.MultMaskedInvertedPremultipliedAlpha)
            {
                continue;
            }

            _pipelineResource[i] = new(vk, device);
        }

        // Copy / SetupMask
        CreatePipelineResource(ShaderNames.Copy, "VertShaderSrcCopy.spv", "FragShaderSrcCopy.spv", descriptorSetLayout);
        CreatePipelineResource(ShaderNames.SetupMask, "VertShaderSrcSetupMask.spv", "FragShaderSrcSetupMask.spv", descriptorSetLayout);

        // 常规(normal)
        CreatePipelineResource(ShaderNames.Normal, "VertShaderSrc.spv", "FragShaderSrc.spv", descriptorSetLayout);
        CreatePipelineResource(ShaderNames.NormalMasked, "VertShaderSrcMasked.spv", "FragShaderSrcMask.spv", descriptorSetLayout);
        CreatePipelineResource(ShaderNames.NormalMaskedInverted, "VertShaderSrcMasked.spv", "FragShaderSrcMaskInverted.spv", descriptorSetLayout);
        CreatePipelineResource(ShaderNames.NormalPremultipliedAlpha, "VertShaderSrc.spv", "FragShaderSrcPremultipliedAlpha.spv", descriptorSetLayout);
        CreatePipelineResource(ShaderNames.NormalMaskedPremultipliedAlpha, "VertShaderSrcMasked.spv", "FragShaderSrcMaskPremultipliedAlpha.spv", descriptorSetLayout);
        CreatePipelineResource(ShaderNames.NormalMaskedInvertedPremultipliedAlpha, "VertShaderSrcMasked.spv", "FragShaderSrcMaskInvertedPremultipliedAlpha.spv", descriptorSetLayout);

        // 加法(shader 与 Normal 共用)
        _pipelineResource[ShaderNames.Add] = _pipelineResource[ShaderNames.Normal];
        _pipelineResource[ShaderNames.AddMasked] = _pipelineResource[ShaderNames.NormalMasked];
        _pipelineResource[ShaderNames.AddMaskedInverted] = _pipelineResource[ShaderNames.NormalMaskedInverted];
        _pipelineResource[ShaderNames.AddPremultipliedAlpha] = _pipelineResource[ShaderNames.NormalPremultipliedAlpha];
        _pipelineResource[ShaderNames.AddMaskedPremultipliedAlpha] = _pipelineResource[ShaderNames.NormalMaskedPremultipliedAlpha];
        _pipelineResource[ShaderNames.AddMaskedInvertedPremultipliedAlpha] = _pipelineResource[ShaderNames.NormalMaskedInvertedPremultipliedAlpha];

        // 乘法(shader 与 Normal 共用)
        _pipelineResource[ShaderNames.Mult] = _pipelineResource[ShaderNames.Normal];
        _pipelineResource[ShaderNames.MultMasked] = _pipelineResource[ShaderNames.NormalMasked];
        _pipelineResource[ShaderNames.MultMaskedInverted] = _pipelineResource[ShaderNames.NormalMaskedInverted];
        _pipelineResource[ShaderNames.MultPremultipliedAlpha] = _pipelineResource[ShaderNames.NormalPremultipliedAlpha];
        _pipelineResource[ShaderNames.MultMaskedPremultipliedAlpha] = _pipelineResource[ShaderNames.NormalMaskedPremultipliedAlpha];
        _pipelineResource[ShaderNames.MultMaskedInvertedPremultipliedAlpha] = _pipelineResource[ShaderNames.NormalMaskedInvertedPremultipliedAlpha];

        // 按混合模式的组合创建(对应官方 .cpp:690-734)
        // 与官方相同, 从 ShaderNames_NormalAtop(=20) 起以 offset++ 填充 474 个.
        int offset = ShaderNames.NormalAtop;
        for (int colorBlendType = 0; colorBlendType <= 17; ++colorBlendType)
        {
            // AddCompatible(1) / MultiplyCompatible(2) 使用 5.2 以前的 shader, 因此跳过
            if (colorBlendType == 1 || colorBlendType == 2)
            {
                continue;
            }

            // Normal + Over 使用 5.2 以前的 shader, 因此从 alpha=1 开始
            int startAlpha = colorBlendType == 0 ? 1 : 0;
            string colorName = CsmBlendMode.ColorBlendModeToString(colorBlendType);

            for (int alphaBlendType = startAlpha; alphaBlendType <= 4; ++alphaBlendType)
            {
                string alphaName = CsmBlendMode.AlphaBlendModeToString(alphaBlendType);

                CreatePipelineResource(offset++, "VertShaderSrcBlend.spv",
                    "FragShaderSrcBlend" + colorName + alphaName + ".spv",
                    descriptorSetLayout, colorBlendType, alphaBlendType);

                CreatePipelineResource(offset++, "VertShaderSrcMaskedBlend.spv",
                    "FragShaderSrcMaskBlend" + colorName + alphaName + ".spv",
                    descriptorSetLayout, colorBlendType, alphaBlendType);

                CreatePipelineResource(offset++, "VertShaderSrcMaskedBlend.spv",
                    "FragShaderSrcMaskInvertedBlend" + colorName + alphaName + ".spv",
                    descriptorSetLayout, colorBlendType, alphaBlendType);

                CreatePipelineResource(offset++, "VertShaderSrcBlend.spv",
                    "FragShaderSrcPremultipliedAlphaBlend" + colorName + alphaName + ".spv",
                    descriptorSetLayout, colorBlendType, alphaBlendType);

                CreatePipelineResource(offset++, "VertShaderSrcMaskedBlend.spv",
                    "FragShaderSrcMaskPremultipliedAlphaBlend" + colorName + alphaName + ".spv",
                    descriptorSetLayout, colorBlendType, alphaBlendType);

                CreatePipelineResource(offset++, "VertShaderSrcMaskedBlend.spv",
                    "FragShaderSrcMaskInvertedPremultipliedAlphaBlend" + colorName + alphaName + ".spv",
                    descriptorSetLayout, colorBlendType, alphaBlendType);
            }
        }

        if (offset != ShaderCount)
        {
            CubismLog.Warning("[Vulkan] CreatePipelines offset mismatch: {0} != {1}", offset, ShaderCount);
        }
    }

    private void CreatePipelineResource(int shaderName, string vertFileName, string fragFileName,
        DescriptorSetLayout descriptorSetLayout, int colorBlendMode = -1, int alphaBlendMode = -1)
    {
        _pipelineResource![shaderName].CreateGraphicsPipeline(vertFileName, fragFileName,
            descriptorSetLayout, shaderName, colorBlendMode, alphaBlendMode);
    }

    /// <summary>
    /// 获取指定 shader 的图形 pipeline
    /// </summary>
    /// <param name="shaderIndex">shader 索引</param>
    /// <param name="blendIndex"> 混合模式的索引</param>
    /// <returns>指定 shader 的图形 pipeline</returns>
    public Pipeline GetPipeline(int shaderIndex, int blendIndex)
    {
        if (_pipelineResource == null || (uint)shaderIndex >= (uint)ShaderCount)
        {
            // 资源索引越界立即检测.
            CubismLog.Error("[Vulkan] shaderIndex out of range: {0} (ShaderCount={1})", shaderIndex, ShaderCount);
            return default;
        }

        return _pipelineResource[shaderIndex].GetPipeline(blendIndex);
    }

    /// <summary>
    /// 获取指定混合方式的 pipeline layout
    /// </summary>
    /// <param name="shaderIndex">shader 索引</param>
    /// <param name="blendIndex">混合模式的索引</param>
    /// <returns>指定混合方式的 pipeline layout</returns>
    public PipelineLayout GetPipelineLayout(int shaderIndex, int blendIndex)
    {
        if (_pipelineResource == null || (uint)shaderIndex >= (uint)ShaderCount)
        {
            CubismLog.Error("[Vulkan] shaderIndex out of range: {0} (ShaderCount={1})", shaderIndex, ShaderCount);
            return default;
        }

        return _pipelineResource[shaderIndex].GetPipelineLayout(blendIndex);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void ReleaseShaderProgram()
    {
        if (_pipelineResource == null)
        {
            return;
        }

        // 8..19 是引用 Normal 的 2..7 的 alias, 因此不重复释放
        for (int i = 0; i < ShaderCount; i++)
        {
            if (i >= ShaderNames.Add && i <= ShaderNames.MultMaskedInvertedPremultipliedAlpha)
            {
                continue;
            }

            _pipelineResource[i]?.Release();
        }

        _pipelineResource = null;
    }

    public void Dispose()
    {
        ReleaseShaderProgram();
        GC.SuppressFinalize(this);
    }
}
