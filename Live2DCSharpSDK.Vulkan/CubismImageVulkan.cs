using Live2DCSharpSDK.Framework;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// 处理 image 的类
/// </summary>
public class CubismImageVulkan(Vk vk)
{
    /// <summary>
    /// image
    /// </summary>
    public Image Image => _image;

    private Image _image;

    /// <summary>
    /// 内存
    /// </summary>
    public DeviceMemory Memory => _memory;

    private DeviceMemory _memory;

    /// <summary>
    /// 视图
    /// </summary>
    public ImageView View => _view;

    private ImageView _view;

    /// <summary>
    /// sampler
    /// </summary>
    public Sampler Sampler => _sampler;

    private Sampler _sampler;

    /// <summary>
    /// 当前的 image 布局
    /// </summary>
    public ImageLayout CurrentLayout { get; private set; }
    /// <summary>
    /// image 的宽度
    /// </summary>
    public uint Width { get; private set; }
    /// <summary>
    /// image 的高度
    /// </summary>
    public uint Height { get; private set; }

    /// <summary>
    /// image 与内存是否都已创建成功
    /// </summary>
    public bool IsValid => _image.Handle != 0 && _memory.Handle != 0;

    /// <summary>
    /// 查找物理设备内存类型的索引
    /// </summary>
    /// <param name="physicalDevice">物理设备</param>
    /// <param name="typeFilter">存在内存类型时被设置的位掩码</param>
    /// <param name="properties">内存访问设备时的类型</param>
    /// <returns>内存类型索引, 找不到兼容类型时返回 -1</returns>
    public unsafe int FindMemoryType(PhysicalDevice physicalDevice, uint typeFilter, MemoryPropertyFlags properties)
    {
        vk.GetPhysicalDeviceMemoryProperties(physicalDevice, out var memProperties);

        for (int i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                return i;
            }
        }

        // 不能用 CubismLog.Error(会抛未类型化异常), 由调用方决定降级还是抛明确异常.
        CubismLog.Warning("[Live2D Vulkan]failed to find suitable memory type!");
        return -1;
    }

    /// <summary>
    /// 创建 image
    /// </summary>
    /// <param name="device">设备</param>
    /// <param name="physicalDevice">物理设备</param>
    /// <param name="w">宽度</param>
    /// <param name="h">高度</param>
    /// <param name="mipLevel">mipmap 的层级</param>
    /// <param name="format">格式</param>
    /// <param name="tiling">tiling 布局设置</param>
    /// <param name="usage">指定 image 用途的位掩码</param>
    /// <returns>成功返回 true, 失败时已回收部分资源并返回 false</returns>
    public unsafe bool CreateImage(Device device, PhysicalDevice physicalDevice, uint w, uint h,
                     uint mipLevel, Format format, ImageTiling tiling, ImageUsageFlags usage)
    {
        Destroy(device);

        if (w == 0 || h == 0)
        {
            CubismLog.Warning("[Live2D Vulkan]create image with zero extent ({0}x{1}).", w, h);
            return false;
        }

        var imageInfo = new ImageCreateInfo()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            MipLevels = mipLevel,
            ArrayLayers = 1,
            Format = format,
            Tiling = tiling,
            InitialLayout = ImageLayout.Undefined,
            Usage = usage,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive,
            Extent = new()
            {
                Width = w,
                Height = h,
                Depth = 1
            }
        };

        if (vk.CreateImage(device, ref imageInfo, null, out _image) != Result.Success)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to create image ({0}x{1}).", w, h);
            _image = default;
            return false;
        }

        vk.GetImageMemoryRequirements(device, _image, out var memRequirements);

        int memoryTypeIndex = FindMemoryType(physicalDevice, memRequirements.MemoryTypeBits,
            MemoryPropertyFlags.DeviceLocalBit);
        if (memoryTypeIndex < 0)
        {
            // 找不到兼容内存类型: 先回收已创建的 image, 避免句柄泄漏.
            ReleaseImage(device);
            return false;
        }

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = (uint)memoryTypeIndex
        };

        if (vk.AllocateMemory(device, ref allocInfo, null, out _memory) != Result.Success)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to allocate image memory ({0}x{1}, {2} bytes).",
                w, h, memRequirements.Size);
            _memory = default;
            ReleaseImage(device);
            return false;
        }

        if (vk.BindImageMemory(device, _image, _memory, 0) != Result.Success)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to bind image memory ({0}x{1}).", w, h);
            Destroy(device);
            return false;
        }

        Width = w;
        Height = h;
        return true;
    }

    /// <summary>
    /// 创建视图
    /// </summary>
    /// <param name="device">设备</param>
    /// <param name="format">格式</param>
    /// <param name="aspectFlags">指定视图包含哪些 aspect mask 的位掩码</param>
    /// <param name="mipLevel">mipmap 的层级</param>
    /// <returns>成功返回 true, 失败时 view 句柄保持为空并返回 false</returns>
    public unsafe bool CreateView(Device device, Format format, ImageAspectFlags aspectFlags, uint mipLevel)
    {
        if (_image.Handle == 0)
        {
            CubismLog.Warning("[Live2D Vulkan]create view without image.");
            return false;
        }

        var viewInfo = new ImageViewCreateInfo()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = Image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new(aspectFlags, 0, mipLevel, 0, 1)
        };

        if (vk.CreateImageView(device, ref viewInfo, null, out _view) != Result.Success)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to create texture image view.");
            _view = default;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 创建 sampler
    /// </summary>
    /// <param name="device">设备</param>
    /// <param name="maxAnistropy">各向异性值的最大值</param>
    /// <param name="mipLevel">mipmap 的层级</param>
    /// <returns>成功返回 true, 失败时 sampler 句柄保持为空并返回 false</returns>
    public unsafe bool CreateSampler(Device device, float maxAnistropy, uint mipLevel)
    {
        var samplerInfo = new SamplerCreateInfo()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            AnisotropyEnable = true,
            MaxAnisotropy = maxAnistropy,
            BorderColor = BorderColor.FloatTransparentBlack,
            UnnormalizedCoordinates = false,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            MipmapMode = SamplerMipmapMode.Linear,
            MinLod = 0.0f,
            MaxLod = mipLevel,
            MipLodBias = 0.0f
        };

        if (vk.CreateSampler(device, ref samplerInfo, null, out _sampler) != Result.Success)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to create texture sampler.");
            _sampler = default;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 变更 image 的布局
    /// </summary>
    /// <param name="commandBuffer">命令缓冲区</param>
    /// <param name="newLayout">新的布局</param>
    /// <param name="mipLevels">mipmap 的层级</param>
    /// <param name="aspectMask"></param>
    public unsafe void SetImageLayout(CommandBuffer commandBuffer, ImageLayout newLayout, uint mipLevels, ImageAspectFlags aspectMask)
    {
        var barrier = new ImageMemoryBarrier()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = CurrentLayout,
            NewLayout = newLayout,
            Image = Image,
            SubresourceRange = new(aspectMask, 0, mipLevels, 0, 1)
        };

        var sourceStage = PipelineStageFlags.AllCommandsBit;
        var destinationStage = PipelineStageFlags.AllCommandsBit;

        switch (CurrentLayout)
        {
            case ImageLayout.Undefined:
                barrier.SrcAccessMask = 0;
                break;

            case ImageLayout.ColorAttachmentOptimal:
                barrier.SrcAccessMask = AccessFlags.ColorAttachmentWriteBit;
                break;

            case ImageLayout.DepthStencilAttachmentOptimal:
                barrier.SrcAccessMask = AccessFlags.DepthStencilAttachmentWriteBit;
                break;

            case ImageLayout.TransferSrcOptimal:
                barrier.SrcAccessMask = AccessFlags.TransferReadBit;
                break;

            case ImageLayout.TransferDstOptimal:
                barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
                sourceStage = PipelineStageFlags.TransferBit;
                break;

            case ImageLayout.ShaderReadOnlyOptimal:
                barrier.SrcAccessMask = AccessFlags.ShaderReadBit;
                break;

            default:
                break;
        }

        switch (newLayout)
        {
            case ImageLayout.TransferSrcOptimal:
                barrier.DstAccessMask = AccessFlags.TransferReadBit;
                destinationStage = PipelineStageFlags.TransferBit;
                break;

            case ImageLayout.TransferDstOptimal:
                barrier.DstAccessMask = AccessFlags.TransferWriteBit;
                destinationStage = PipelineStageFlags.TransferBit;
                break;

            // 向只读布局的转换需要等待 fragment shader 的读取.
            // 官方 5-r.5(Framework/src/Rendering/Vulkan/CubismClass_Vulkan.cpp) 在此处
            // 用 VK_PIPELINE_STAGE_RAY_TRACING_SHADER_BIT_KHR 后置赋值, 覆盖了 FragmentShaderBit,
            // 但本设备未启用光线追踪扩展, 因此不使用
            // (源自上游的 bug. 纹理仅在 fragment shader 中被采样).
            case ImageLayout.ReadOnlyOptimal:
                barrier.DstAccessMask = AccessFlags.ShaderReadBit;
                destinationStage = PipelineStageFlags.FragmentShaderBit;
                break;

            default:
                break;
        }

        vk.CmdPipelineBarrier(
            commandBuffer,
            sourceStage,
            destinationStage,
            0,
            0,
            null,
            0,
            null,
            1,
            ref barrier
        );

        CurrentLayout = newLayout;
    }

    /// <summary>
    /// 保存 vkCmdEndRendering 后因 pipeline barrier 而改变的 image 布局
    /// </summary>
    /// <param name="newLayout">新的布局</param>
    public void SetCurrentLayout(ImageLayout newLayout)
    {
        CurrentLayout = newLayout;
    }

    /// <summary>
    /// 销毁资源
    /// </summary>
    /// <param name="device">设备</param>
    public unsafe void Destroy(Device device)
    {
        if (_view.Handle != 0)
        {
            vk.DestroyImageView(device, _view, null);
            _view.Handle = 0;
        }
        if (_sampler.Handle != 0)
        {
            vk.DestroySampler(device, _sampler, null);
            _sampler.Handle = 0;
        }

        ReleaseImage(device);

        if (_memory.Handle != 0)
        {
            vk.FreeMemory(device, _memory, null);
            _memory.Handle = 0;
        }

        Width = 0;
        Height = 0;
        CurrentLayout = ImageLayout.Undefined;
    }

    /// <summary>
    /// 释放已创建的 image 句柄, 内存句柄不动
    /// </summary>
    /// <param name="device">设备</param>
    private unsafe void ReleaseImage(Device device)
    {
        if (_image.Handle != 0)
        {
            vk.DestroyImage(device, _image, null);
            _image.Handle = 0;
        }
    }
}
