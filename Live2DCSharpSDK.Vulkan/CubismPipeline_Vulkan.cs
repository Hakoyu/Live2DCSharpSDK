using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

public class CubismPipeline_Vulkan(Vk vk, Device device) : IDisposable
{
    /// <summary>
    /// 官方 ShaderNames_ShaderCount = 494（P8c 报告 §1）。
    /// </summary>
    private const int ShaderCount = ShaderNames.ShaderCount;

    private PipelineResource[]? _pipelineResource;

    /// <summary>
    /// シェーダーごとにPipelineResourceのインスタンスを作成する
    /// （官方 CubismRenderer_Vulkan.cpp:643-735 CreatePipelines）。
    /// </summary>
    /// <param name="descriptorSetLayout">ディスクリプタセットレイアウト</param>
    public void CreatePipelines(DescriptorSetLayout descriptorSetLayout)
    {
        //パイプラインを作成済みの場合は生成する必要なし
        if (_pipelineResource != null)
        {
            return;
        }


        _pipelineResource = new PipelineResource[ShaderCount];

        for (int i = 0; i < ShaderCount; i++)
        {
            // 加算・乗算は通常と同じ PipelineResource を参照する（alias は下で設定）
            if (i >= ShaderNames.Add && i <= ShaderNames.MultMaskedInvertedPremultipliedAlpha)
            {
                continue;
            }

            _pipelineResource[i] = new(vk, device);
        }

        // Copy / SetupMask
        CreatePipelineResource(ShaderNames.Copy, "VertShaderSrcCopy.spv", "FragShaderSrcCopy.spv", descriptorSetLayout);
        CreatePipelineResource(ShaderNames.SetupMask, "VertShaderSrcSetupMask.spv", "FragShaderSrcSetupMask.spv", descriptorSetLayout);

        // 通常
        CreatePipelineResource(ShaderNames.Normal, "VertShaderSrc.spv", "FragShaderSrc.spv", descriptorSetLayout);
        CreatePipelineResource(ShaderNames.NormalMasked, "VertShaderSrcMasked.spv", "FragShaderSrcMask.spv", descriptorSetLayout);
        CreatePipelineResource(ShaderNames.NormalMaskedInverted, "VertShaderSrcMasked.spv", "FragShaderSrcMaskInverted.spv", descriptorSetLayout);
        CreatePipelineResource(ShaderNames.NormalPremultipliedAlpha, "VertShaderSrc.spv", "FragShaderSrcPremultipliedAlpha.spv", descriptorSetLayout);
        CreatePipelineResource(ShaderNames.NormalMaskedPremultipliedAlpha, "VertShaderSrcMasked.spv", "FragShaderSrcMaskPremultipliedAlpha.spv", descriptorSetLayout);
        CreatePipelineResource(ShaderNames.NormalMaskedInvertedPremultipliedAlpha, "VertShaderSrcMasked.spv", "FragShaderSrcMaskInvertedPremultipliedAlpha.spv", descriptorSetLayout);

        // 加算（shader は Normal と共用）
        _pipelineResource[ShaderNames.Add] = _pipelineResource[ShaderNames.Normal];
        _pipelineResource[ShaderNames.AddMasked] = _pipelineResource[ShaderNames.NormalMasked];
        _pipelineResource[ShaderNames.AddMaskedInverted] = _pipelineResource[ShaderNames.NormalMaskedInverted];
        _pipelineResource[ShaderNames.AddPremultipliedAlpha] = _pipelineResource[ShaderNames.NormalPremultipliedAlpha];
        _pipelineResource[ShaderNames.AddMaskedPremultipliedAlpha] = _pipelineResource[ShaderNames.NormalMaskedPremultipliedAlpha];
        _pipelineResource[ShaderNames.AddMaskedInvertedPremultipliedAlpha] = _pipelineResource[ShaderNames.NormalMaskedInvertedPremultipliedAlpha];

        // 乗算（shader は Normal と共用）
        _pipelineResource[ShaderNames.Mult] = _pipelineResource[ShaderNames.Normal];
        _pipelineResource[ShaderNames.MultMasked] = _pipelineResource[ShaderNames.NormalMasked];
        _pipelineResource[ShaderNames.MultMaskedInverted] = _pipelineResource[ShaderNames.NormalMaskedInverted];
        _pipelineResource[ShaderNames.MultPremultipliedAlpha] = _pipelineResource[ShaderNames.NormalPremultipliedAlpha];
        _pipelineResource[ShaderNames.MultMaskedPremultipliedAlpha] = _pipelineResource[ShaderNames.NormalMaskedPremultipliedAlpha];
        _pipelineResource[ShaderNames.MultMaskedInvertedPremultipliedAlpha] = _pipelineResource[ShaderNames.NormalMaskedInvertedPremultipliedAlpha];

        // ブレンドモードの組み合わせ分作成（官方 .cpp:690-734）
        // 官方と同じく ShaderNames_NormalAtop(=20) から offset++ で 474 個を埋める。
        int offset = ShaderNames.NormalAtop;
        for (int colorBlendType = 0; colorBlendType <= 17; ++colorBlendType)
        {
            // AddCompatible(1) / MultiplyCompatible(2) は 5.2 以前の shader を使うためスキップ
            if (colorBlendType == 1 || colorBlendType == 2)
            {
                continue;
            }

            // Normal + Over は 5.2 以前の shader を使うため alpha=1 から
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
    /// 指定したシェーダーのグラフィックスパイプラインを取得する
    /// </summary>
    /// <param name="shaderIndex">シェーダインデックス</param>
    /// <param name="blendIndex"> ブレンドモードのインデックス</param>
    /// <returns>指定したシェーダーのグラフィックスパイプライン</returns>
    public Pipeline GetPipeline(int shaderIndex, int blendIndex)
    {
        if (_pipelineResource == null || (uint)shaderIndex >= (uint)ShaderCount)
        {
            // リソース索引の越界は即座に検出する（P8c §3）
            CubismLog.Error("[Vulkan] shaderIndex out of range: {0} (ShaderCount={1})", shaderIndex, ShaderCount);
            return default;
        }

        return _pipelineResource[shaderIndex].GetPipeline(blendIndex);
    }

    /// <summary>
    /// 指定したブレンド方法のパイプラインレイアウトを取得する
    /// </summary>
    /// <param name="shaderIndex">シェーダインデックス</param>
    /// <param name="blendIndex">ブレンドモードのインデックス</param>
    /// <returns>指定したブレンド方法のパイプラインレイアウト</returns>
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
    /// リソースを開放する
    /// </summary>
    public void ReleaseShaderProgram()
    {
        if (_pipelineResource == null)
        {
            return;
        }

        // 8..19 は Normal の 2..7 を参照する alias のため二重解放しない
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
