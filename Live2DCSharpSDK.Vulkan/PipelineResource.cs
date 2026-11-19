using System.Reflection;
using Live2DCSharpSDK.Framework;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

public class PipelineResource(Vk vk, Device device)
{
    /// <summary>
    /// normal, add, multi, mask 各自的 pipeline
    /// (5.3 以后的 blend / Copy 只有 1 个)
    /// </summary>
    private Pipeline[] _pipeline;

    /// <summary>
    /// normal, add, multi, mask 各自的 pipeline layout
    /// </summary>
    private PipelineLayout[] _pipelineLayout;

    /// <summary>
    /// 是否为单一 pipeline(Copy / 5.3 以后的 blend)
    /// </summary>
    private bool _singlePipeline;

    /// <summary>
    /// 创建 shader module
    /// </summary>
    /// <param name="device">逻辑设备</param>
    /// <param name="filename">文件名</param>
    /// <returns></returns>
    public unsafe ShaderModule CreateShaderModule(Device device, string filename)
    {
        var assm = Assembly.GetExecutingAssembly();
        string name = "Live2DCSharpSDK.Vulkan.spv." + filename;
        // 内嵌 shader 资源缺失时必须立刻失败: 原实现先对返回值加 ! 再判空,
        // 判空分支永远不成立, 随后 item.CopyTo 抛 NullReferenceException.
        using var item = assm.GetManifestResourceStream(name);
        if (item == null)
        {
            // CubismLog.Error 内部会抛异常; 后面的 throw 仅用于让编译器的空值分析确认 item 非空
            CubismLog.Error("failed to open file! {0}", name);
            throw new InvalidOperationException($"Embedded shader resource not found: {name}");
        }
        using var mem = new MemoryStream();
        item.CopyTo(mem);
        var data = mem.ToArray();

        var createInfo = new ShaderModuleCreateInfo
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (uint)data.Length,
        };

        ShaderModule shaderModule;

        fixed (byte* codePtr = data)
        {
            createInfo.PCode = (uint*)codePtr;

            if (
                vk.CreateShaderModule(device, ref createInfo, null, out shaderModule)
                != Result.Success
            )
            {
                CubismLog.Error("failed to create shader module!");
            }
        }

        return shaderModule;
    }

    /// <summary>
    /// 创建 pipeline
    /// </summary>
    /// <param name="vertFileName">Vertex shader 的文件</param>
    /// <param name="fragFileName">Fragment shader 的文件</param>
    /// <param name="descriptorSetLayout">descriptor set layout</param>
    /// <param name="shaderName">ShaderNames 的下标(用于 Copy 判定)</param>
    /// <param name="colorBlendMode">颜色混合模式(仅 5.3 以后)</param>
    /// <param name="alphaBlendMode">alpha 混合模式(仅 5.3 以后)</param>
    public unsafe void CreateGraphicsPipeline(
        string vertFileName,
        string fragFileName,
        DescriptorSetLayout descriptorSetLayout,
        int shaderName = -1,
        int colorBlendMode = -1,
        int alphaBlendMode = -1
    )
    {
        var vertShaderModule = CreateShaderModule(device, vertFileName);
        var fragShaderModule = CreateShaderModule(device, fragFileName);

        // 官方 CubismRenderer_Vulkan.cpp:544-551: Copy 与 5.3 以后的 blend 均为单一 pipeline
        _singlePipeline =
            shaderName == ShaderNames.Copy || (colorBlendMode != -1 && alphaBlendMode != -1);

        if (_singlePipeline)
        {
            _pipeline = new Pipeline[1];
            _pipelineLayout = new PipelineLayout[1];
        }
        else
        {
            _pipeline = new Pipeline[4];
            _pipelineLayout = new PipelineLayout[4];
        }

        var vertShaderStageInfo = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main"),
        };

        var fragShaderStageInfo = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main"),
        };

        var shaderStages = stackalloc[] { vertShaderStageInfo, fragShaderStageInfo };

        var vertexInputInfo = new PipelineVertexInputStateCreateInfo
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
        };

        var bindingDescription = ModelVertex.GetBindingDescription();
        var attributeDescriptions = stackalloc VertexInputAttributeDescription[2];
        ModelVertex.GetAttributeDescriptions(attributeDescriptions);
        vertexInputInfo.VertexBindingDescriptionCount = 1;
        vertexInputInfo.VertexAttributeDescriptionCount = 2;
        vertexInputInfo.PVertexBindingDescriptions = &bindingDescription;
        vertexInputInfo.PVertexAttributeDescriptions = attributeDescriptions;

        var inputAssembly = new PipelineInputAssemblyStateCreateInfo
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = false,
        };

        var viewportState = new PipelineViewportStateCreateInfo
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            ScissorCount = 1,
        };

        var rasterizer = new PipelineRasterizationStateCreateInfo
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = false,
            RasterizerDiscardEnable = false,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1.0f,
            FrontFace = FrontFace.CounterClockwise,
        };

        var multisampling = new PipelineMultisampleStateCreateInfo
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = false,
            RasterizationSamples = SampleCountFlags.Count1Bit,
        };

        // 初始值: 通常(Blend_Normal)
        var colorBlendAttachment = new PipelineColorBlendAttachmentState
        {
            ColorWriteMask =
                ColorComponentFlags.RBit
                | ColorComponentFlags.GBit
                | ColorComponentFlags.BBit
                | ColorComponentFlags.ABit,
            BlendEnable = true,
            SrcColorBlendFactor = BlendFactor.One,
            DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
            ColorBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = BlendFactor.One,
            DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
            AlphaBlendOp = BlendOp.Add,
        };

        var colorBlending = new PipelineColorBlendStateCreateInfo
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = false,
            LogicOp = LogicOp.Copy,
            AttachmentCount = 1,
            PAttachments = &colorBlendAttachment,
        };

        var pipelineLayoutInfo = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &descriptorSetLayout,
        };

        var dynamicState = stackalloc[] {
            DynamicState.Viewport,
            DynamicState.Scissor,
            DynamicState.CullMode,
        };

        var dynamicStateCI = new PipelineDynamicStateCreateInfo
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = 3,
            PDynamicStates = dynamicState,
        };

        var pipelineDepthStencilStateCreateInfo = new PipelineDepthStencilStateCreateInfo
        {
            SType = StructureType.PipelineDepthStencilStateCreateInfo,
            DepthTestEnable = true,
            DepthWriteEnable = true,
            DepthCompareOp = CompareOp.LessOrEqual,
            Back = new() { CompareOp = CompareOp.Always },
        };

        var renderingInfo = new PipelineRenderingCreateInfo
        {
            SType = StructureType.PipelineRenderingCreateInfo,
            ColorAttachmentCount = 1,
            DepthAttachmentFormat = CubismRenderer_Vulkan.DepthFormat,
        };
        var format = CubismRenderer_Vulkan.ImageFormat;
        var format1 = CubismRenderer_Vulkan.SwapchainImageFormat;
        if (CubismRenderer_Vulkan.UseRenderTarget)
        {
            renderingInfo.PColorAttachmentFormats = &format;
        }
        else
        {
            renderingInfo.PColorAttachmentFormats = &format1;
        }

        var pipelineInfo = new GraphicsPipelineCreateInfo
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = 2,
            PVertexInputState = &vertexInputInfo,
            PInputAssemblyState = &inputAssembly,
            PViewportState = &viewportState,
            PRasterizationState = &rasterizer,
            PMultisampleState = &multisampling,
            PColorBlendState = &colorBlending,
            Subpass = 0,
            BasePipelineHandle = new(),
            PDynamicState = &dynamicStateCI,
            PDepthStencilState = &pipelineDepthStencilStateCreateInfo,
            PNext = &renderingInfo,
            PStages = shaderStages,
        };

        if (_singlePipeline)
        {
            // Copy 使用通常的 alpha blend, 5.3 以后的 blend 在 shader 内部合成, 因此为 ONE/ZERO
            if (shaderName == ShaderNames.Copy)
            {
                colorBlendAttachment.SrcColorBlendFactor = BlendFactor.One;
                colorBlendAttachment.DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha;
                colorBlendAttachment.SrcAlphaBlendFactor = BlendFactor.One;
                colorBlendAttachment.DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha;
            }
            else
            {
                colorBlendAttachment.SrcColorBlendFactor = BlendFactor.One;
                colorBlendAttachment.DstColorBlendFactor = BlendFactor.Zero;
                colorBlendAttachment.SrcAlphaBlendFactor = BlendFactor.One;
                colorBlendAttachment.DstAlphaBlendFactor = BlendFactor.Zero;
            }

            if (
                vk.CreatePipelineLayout(
                    device,
                    ref pipelineLayoutInfo,
                    null,
                    out _pipelineLayout[0]
                ) != Result.Success
            )
            {
                CubismLog.Error("failed to create _pipeline layout!");
            }

            pipelineInfo.Layout = _pipelineLayout[0];
            if (
                vk.CreateGraphicsPipelines(
                    device,
                    default,
                    1,
                    ref pipelineInfo,
                    null,
                    out _pipeline[0]
                ) != Result.Success
            )
            {
                CubismLog.Error("failed to create graphics _pipeline!");
            }
        }
        else
        {
            // 5.2 以前: 通常 / 加法 / 乘法 / 遮罩 共 4 个
            if (
                vk.CreatePipelineLayout(
                    device,
                    ref pipelineLayoutInfo,
                    null,
                    out _pipelineLayout[Blend.Normal]
                ) != Result.Success
            )
            {
                CubismLog.Error("failed to create _pipeline layout!");
            }

            pipelineInfo.Layout = _pipelineLayout[Blend.Normal];

            if (
                vk.CreateGraphicsPipelines(
                    device,
                    default,
                    1,
                    ref pipelineInfo,
                    null,
                    out _pipeline[Blend.Normal]
                ) != Result.Success
            )
            {
                CubismLog.Error("failed to create graphics _pipeline!");
            }

            // 加法
            colorBlendAttachment.SrcColorBlendFactor = BlendFactor.One;
            colorBlendAttachment.DstColorBlendFactor = BlendFactor.One;
            colorBlendAttachment.SrcAlphaBlendFactor = BlendFactor.Zero;
            colorBlendAttachment.DstAlphaBlendFactor = BlendFactor.One;

            if (
                vk.CreatePipelineLayout(
                    device,
                    ref pipelineLayoutInfo,
                    null,
                    out _pipelineLayout[Blend.Add]
                ) != Result.Success
            )
            {
                CubismLog.Error("failed to create _pipeline layout!");
            }

            pipelineInfo.Layout = _pipelineLayout[Blend.Add];

            if (
                vk.CreateGraphicsPipelines(
                    device,
                    default,
                    1,
                    ref pipelineInfo,
                    null,
                    out _pipeline[Blend.Add]
                ) != Result.Success
            )
            {
                CubismLog.Error("failed to create graphics _pipeline!");
            }

            // 乘法
            colorBlendAttachment.SrcColorBlendFactor = BlendFactor.DstColor;
            colorBlendAttachment.DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha;
            colorBlendAttachment.SrcAlphaBlendFactor = BlendFactor.Zero;
            colorBlendAttachment.DstAlphaBlendFactor = BlendFactor.One;

            if (
                vk.CreatePipelineLayout(
                    device,
                    ref pipelineLayoutInfo,
                    null,
                    out _pipelineLayout[Blend.Mult]
                ) != Result.Success
            )
            {
                CubismLog.Error("failed to create _pipeline layout!");
            }

            pipelineInfo.Layout = _pipelineLayout[Blend.Mult];

            if (
                vk.CreateGraphicsPipelines(
                    device,
                    default,
                    1,
                    ref pipelineInfo,
                    null,
                    out _pipeline[Blend.Mult]
                ) != Result.Success
            )
            {
                CubismLog.Error("failed to create graphics _pipeline!");
            }

            // 遮罩
            colorBlendAttachment.SrcColorBlendFactor = BlendFactor.Zero;
            colorBlendAttachment.DstColorBlendFactor = BlendFactor.OneMinusSrcColor;
            colorBlendAttachment.SrcAlphaBlendFactor = BlendFactor.Zero;
            colorBlendAttachment.DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha;

            if (
                vk.CreatePipelineLayout(
                    device,
                    ref pipelineLayoutInfo,
                    null,
                    out _pipelineLayout[Blend.Mask]
                ) != Result.Success
            )
            {
                CubismLog.Error("failed to create _pipeline layout!");
            }

            renderingInfo.PColorAttachmentFormats = &format;
            pipelineInfo.Layout = _pipelineLayout[Blend.Mask];

            if (
                vk.CreateGraphicsPipelines(
                    device,
                    default,
                    1,
                    ref pipelineInfo,
                    null,
                    out _pipeline[Blend.Mask]
                ) != Result.Success
            )
            {
                CubismLog.Error("failed to create graphics _pipeline!");
            }
        }

        vk.DestroyShaderModule(device, vertShaderModule, null);
        vk.DestroyShaderModule(device, fragShaderModule, null);

        SilkMarshal.Free((IntPtr)vertShaderStageInfo.PName);
        SilkMarshal.Free((IntPtr)fragShaderStageInfo.PName);
    }

    public unsafe void Release()
    {
        for (int i = 0; i < _pipeline.Length; i++)
        {
            vk.DestroyPipeline(device, _pipeline[i], null);
            vk.DestroyPipelineLayout(device, _pipelineLayout[i], null);
        }
        _pipeline = [];
        _pipelineLayout = [];
    }

    public Pipeline GetPipeline(int index)
    {
        if (_pipeline == null || _pipeline.Length == 0)
        {
            return default;
        }

        if (_singlePipeline)
        {
            return _pipeline[0];
        }

        return (uint)index < (uint)_pipeline.Length ? _pipeline[index] : default;
    }

    public PipelineLayout GetPipelineLayout(int index)
    {
        if (_pipelineLayout == null || _pipelineLayout.Length == 0)
        {
            return default;
        }

        if (_singlePipeline)
        {
            return _pipelineLayout[0];
        }

        return (uint)index < (uint)_pipelineLayout.Length ? _pipelineLayout[index] : default;
    }
}
