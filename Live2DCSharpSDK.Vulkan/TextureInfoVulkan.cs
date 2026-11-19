using Live2DCSharpSDK.App;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Live2DCSharpSDK.Vulkan;

public class TextureInfoVulkan : TextureInfo
{
    private static int _sequenceId;

    private readonly Vk _vk;
    private readonly LAppDelegateVulkan _lapp;
    private readonly CubismImageVulkan _image;

    /// <summary>
    /// 读取图像文件
    /// </summary>
    /// <param name="fileName">要读取的图像文件路径名</param>
    /// <param name="format">图像格式</param>
    /// <param name="tiling">图像数据的 tiling 布局设置</param>
    /// <param name="usage">图像的使用标志</param>
    /// <param name="imageProperties"></param>
    public unsafe TextureInfoVulkan(
        Vk vk,
        LAppDelegateVulkan lapp,
        LAppModel model,
        Format format,
        ImageTiling tiling,
        ImageUsageFlags usage,
        uint width,
        uint height,
        nint data
    )
    {
        _vk = vk;
        _lapp = lapp;
        Id = ++_sequenceId;

        Device device = lapp.VulkanManager.Device;
        PhysicalDevice physicalDevice = lapp.VulkanManager.PhysicalDevice;

        ulong imageSize = (ulong)width * height * 4;

        // 纹理解算是模型加载的必经步骤, 任一步失败都抛出明确异常,
        // 且抛出前先释放已创建的 staging buffer / image, 避免泄漏与无效句柄外泄.
        var stagingBuffer = new CubismBufferVulkan(vk);
        if (
            stagingBuffer.CreateBuffer(
                device,
                physicalDevice,
                imageSize,
                BufferUsageFlags.TransferSrcBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            )
            is false
        )
        {
            throw new InvalidOperationException(
                $"failed to create texture staging buffer ({width}x{height}, {imageSize} bytes)."
            );
        }

        if (
            stagingBuffer.Map(device, imageSize) is false
            || stagingBuffer.MemCpy((void*)data, imageSize) is false
        )
        {
            stagingBuffer.Destroy(device);
            throw new InvalidOperationException(
                $"failed to upload texture data ({width}x{height})."
            );
        }

        stagingBuffer.UnMap(device);

        uint mipLevels = (uint)Math.Floor(Math.Log2(Math.Max(width, height))) + 1;

        _image = new CubismImageVulkan(vk);
        if (
            _image.CreateImage(
                device,
                physicalDevice,
                width,
                height,
                mipLevels,
                format,
                tiling,
                usage
            )
            is false
        )
        {
            stagingBuffer.Destroy(device);
            throw new InvalidOperationException(
                $"failed to create texture image ({width}x{height})."
            );
        }

        CommandBuffer commandBuffer = lapp.VulkanManager.BeginSingleTimeCommands();
        _image.SetImageLayout(
            commandBuffer,
            ImageLayout.TransferDstOptimal,
            mipLevels,
            ImageAspectFlags.ColorBit
        );
        CopyBufferToImage(commandBuffer, stagingBuffer.Buffer, _image.Image, width, height);
        lapp.VulkanManager.SubmitCommand(commandBuffer);
        GenerateMipmaps(_image, width, height, mipLevels);

        if (_image.CreateView(device, format, ImageAspectFlags.ColorBit, mipLevels) is false)
        {
            stagingBuffer.Destroy(device);
            _image.Destroy(device);
            throw new InvalidOperationException(
                $"failed to create texture image view ({width}x{height})."
            );
        }

        vk.GetPhysicalDeviceProperties(physicalDevice, out var properties);
        if (
            _image.CreateSampler(device, properties.Limits.MaxSamplerAnisotropy, mipLevels) is false
        )
        {
            stagingBuffer.Destroy(device);
            _image.Destroy(device);
            throw new InvalidOperationException(
                $"failed to create texture sampler ({width}x{height})."
            );
        }

        stagingBuffer.Destroy(device);

        (model.Renderer as CubismRenderer_Vulkan)?.BindTexture(_image);
    }

    /// <summary>
    /// 把 buffer 复制到 image
    /// </summary>
    /// <param name="commandBuffer">命令缓冲区</param>
    /// <param name="buffer">buffer</param>
    /// <param name="image">image</param>
    /// <param name="width">图像的宽度</param>
    /// <param name="height">图像的高度</param>
    public unsafe void CopyBufferToImage(
        CommandBuffer commandBuffer,
        Buffer buffer,
        Image image,
        uint width,
        uint height
    )
    {
        var region = new BufferImageCopy
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new()
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
            ImageOffset = new(0, 0, 0),
            ImageExtent = new(width, height, 1),
        };

        _vk.CmdCopyBufferToImage(
            commandBuffer,
            buffer,
            image,
            ImageLayout.TransferDstOptimal,
            1,
            ref region
        );
    }

    /// <summary>
    /// 生成 mipmap
    /// </summary>
    /// <param name="image">image</param>
    /// <param name="texWidth">图像的宽度</param>
    /// <param name="texHeight">图像的高度</param>
    /// <param name="mipLevels">mip 层级</param>
    public unsafe void GenerateMipmaps(
        CubismImageVulkan image,
        uint texWidth,
        uint texHeight,
        uint mipLevels
    )
    {
        CommandBuffer commandBuffer = _lapp.VulkanManager.BeginSingleTimeCommands();

        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            Image = image.Image,
            SrcQueueFamilyIndex = uint.MaxValue,
            DstQueueFamilyIndex = uint.MaxValue,
            SubresourceRange = new()
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseArrayLayer = 0,
                LayerCount = 1,
                LevelCount = 1,
            },
        };

        int mipWidth = (int)texWidth;
        int mipHeight = (int)texHeight;

        for (uint i = 1; i < mipLevels; i++)
        {
            barrier.SubresourceRange.BaseMipLevel = i - 1;
            barrier.OldLayout = ImageLayout.TransferDstOptimal;
            barrier.NewLayout = ImageLayout.TransferSrcOptimal;
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.TransferReadBit;

            _vk.CmdPipelineBarrier(
                commandBuffer,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.TransferBit,
                0,
                0,
                null,
                0,
                null,
                1,
                ref barrier
            );

            var blit = new ImageBlit
            {
                SrcSubresource = new()
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = i - 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
                DstSubresource = new()
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = i,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
                SrcOffsets = new(),
                DstOffsets = new(),
            };

            blit.SrcOffsets[0] = new(0, 0, 0);
            blit.SrcOffsets[1] = new(mipWidth, mipHeight, 1);
            blit.DstOffsets[0] = new(0, 0, 0);
            blit.DstOffsets[1] = new(
                mipWidth > 1 ? mipWidth / 2 : 1,
                mipHeight > 1 ? mipHeight / 2 : 1,
                1
            );

            _vk.CmdBlitImage(
                commandBuffer,
                image.Image,
                ImageLayout.TransferSrcOptimal,
                image.Image,
                ImageLayout.TransferDstOptimal,
                1,
                &blit,
                Filter.Linear
            );

            barrier.OldLayout = ImageLayout.TransferSrcOptimal;
            barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
            barrier.SrcAccessMask = AccessFlags.TransferReadBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;

            _vk.CmdPipelineBarrier(
                commandBuffer,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.FragmentShaderBit,
                0,
                0,
                null,
                0,
                null,
                1,
                ref barrier
            );

            if (mipWidth > 1)
                mipWidth /= 2;
            if (mipHeight > 1)
                mipHeight /= 2;
        }

        barrier.SubresourceRange.BaseMipLevel = mipLevels - 1;
        barrier.OldLayout = ImageLayout.TransferDstOptimal;
        barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
        barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
        barrier.DstAccessMask = AccessFlags.ShaderReadBit;

        _vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.TransferBit,
            PipelineStageFlags.FragmentShaderBit,
            0,
            0,
            null,
            0,
            null,
            1,
            ref barrier
        );

        _lapp.VulkanManager.SubmitCommand(commandBuffer);
    }

    /// <summary>
    /// 释放图像
    /// </summary>
    public override void Dispose()
    {
        _image.Destroy(_lapp.VulkanManager.Device);
    }
}
