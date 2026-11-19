using Live2DCSharpSDK.Framework;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Live2DCSharpSDK.Vulkan;

public class SwapchainManager
{
    /// <summary>
    /// 确认物理设备的支持情况
    /// </summary>
    /// <param name="physicalDevice">物理设备</param>
    /// <param name="surface">swapchain surface</param>
    /// <returns>物理设备的支持信息</returns>
    public static unsafe SwapchainSupportDetails QuerySwapchainSupport(KhrSurface vk, PhysicalDevice physicalDevice, SurfaceKHR surface)
    {
        var details = new SwapchainSupportDetails();
        vk.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, surface, out details.Capabilities);

        uint formatCount = 0;
        vk.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, ref formatCount, null);
        if (formatCount != 0)
        {
            details.Formats = new SurfaceFormatKHR[formatCount];
            fixed (SurfaceFormatKHR* ptr = details.Formats)
            {
                vk.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, ref formatCount, ptr);
            }
        }

        uint presentModeCount = 0;
        vk.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, ref presentModeCount, null);

        if (presentModeCount != 0)
        {
            details.PresentModes = new PresentModeKHR[presentModeCount];
            fixed (PresentModeKHR* ptr = details.PresentModes)
            {
                vk.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, ref presentModeCount, ptr);
            }
        }

        return details;
    }

    /// <summary>
    /// 选择显示模式
    /// </summary>
    /// <param name="availablePresentModes">可用的格式</param>
    /// <returns></returns>
    public static PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes)
    {
        for (int i = 0; i < availablePresentModes.Length; i++)
        {
            if (availablePresentModes[i] == PresentModeKHR.MailboxKhr)
            {
                return availablePresentModes[i];
            }
        }
        return PresentModeKHR.FifoKhr;
    }

    /// <summary>
    /// image 的分辨率
    /// </summary>
    public Extent2D Extent { get; private set; } = new(0, 0);
    /// <summary>
    /// image 数量
    /// </summary>
    public uint ImageCount => _imageCount;

    private uint _imageCount;

    /// <summary>
    /// swapchain 的 image
    /// </summary>
    public Image[] Images { get; private set; } = [];
    /// <summary>
    /// swapchain 的 image view
    /// </summary>
    public ImageView[] ImageViews { get; private set; } = [];
    /// <summary>
    /// swapchain
    /// </summary>
    public SwapchainKHR Swapchain => _swapchain;

    private SwapchainKHR _swapchain;

    /// <summary>
    /// swapchain 的格式
    /// </summary>
    public Format SwapchainFormat { get; private set; } = Format.B8G8R8A8Unorm;

    private readonly VulkanApi _api;
    private readonly KhrSurface _khr;
    private readonly KhrSwapchain _khrs;
    private readonly Vk _vk;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="api"></param>
    /// <param name="physicalDevice">物理设备</param>
    /// <param name="device">设备</param>
    /// <param name="surface">surface</param>
    /// <param name="graphicsFamily">绘制命令使用的队列族</param>
    /// <param name="presentFamily">显示命令使用的队列族</param>
    public SwapchainManager(KhrSwapchain swapchain, KhrSurface khr, VulkanApi api, Vk vk, PhysicalDevice physicalDevice, Device device, SurfaceKHR surface, int graphicsFamily, int presentFamily)
    {
        _khrs = swapchain;
        _khr = khr;
        _api = api;
        _vk = vk;

        CreateSwapchain(physicalDevice, device, surface, graphicsFamily, presentFamily);
    }

    /// <summary>
    /// 选择 surface 格式
    /// </summary>
    /// <param name="availableFormats">可用的 surface 格式</param>
    /// <returns>选定的 surface 格式</returns>
    public SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
    {
        for (int i = 0; i < availableFormats.Length; i++)
        {
            if (availableFormats[i].Format == SwapchainFormat && availableFormats[i].ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                return availableFormats[i];
            }
        }
        // 若没有其他可用的格式则使用第一个格式
        return availableFormats[0];
    }

    /// <summary>
    /// 选择 swapchain image 的分辨率
    /// </summary>
    /// <param name="capabilities">保存 surface 功能的结构体</param>
    /// <returns>swapchain image 的分辨率</returns>
    public Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
        {
            return capabilities.CurrentExtent;
        }
        else
        {
            //for Retina display
            _api.GetWindowSize(out var width, out var height);
            var actualExtent = new Extent2D((uint)width, (uint)height);

            if (actualExtent.Width < capabilities.MinImageExtent.Width)
            {
                actualExtent.Width = capabilities.MinImageExtent.Width;
            }
            else if (actualExtent.Width > capabilities.MaxImageExtent.Width)
            {
                actualExtent.Width = capabilities.MaxImageExtent.Width;
            }

            if (actualExtent.Height < capabilities.MinImageExtent.Height)
            {
                actualExtent.Height = capabilities.MinImageExtent.Height;
            }
            else if (actualExtent.Height > capabilities.MaxImageExtent.Height)
            {
                actualExtent.Height = capabilities.MaxImageExtent.Height;
            }

            return actualExtent;
        }
    }

    /// <summary>
    /// 创建 swapchain
    /// </summary>
    /// <param name="physicalDevice">物理设备</param>
    /// <param name="device">设备</param>
    /// <param name="surface">surface</param>
    /// <param name="graphicsFamily">绘制命令使用的队列族</param>
    /// <param name="presentFamily">显示命令使用的队列族</param>
    public unsafe void CreateSwapchain(PhysicalDevice physicalDevice, Device device, SurfaceKHR surface, int graphicsFamily, int presentFamily)
    {
        var swapChainSupport = QuerySwapchainSupport(_khr, physicalDevice, surface);
        var surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
        var presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);
        Extent = ChooseSwapExtent(swapChainSupport.Capabilities);

        _imageCount = swapChainSupport.Capabilities.MinImageCount + 1;
        if (swapChainSupport.Capabilities.MaxImageCount > 0 && _imageCount > swapChainSupport.Capabilities.MaxImageCount)
        {
            _imageCount = swapChainSupport.Capabilities.MaxImageCount;
        }

        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = surface,
            MinImageCount = _imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = Extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit |
                 ImageUsageFlags.TransferDstBit,
            PreTransform = swapChainSupport.Capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true,
            OldSwapchain = default
        };

        var ptr1 = stackalloc[] { (uint)graphicsFamily, (uint)presentFamily };

        if (graphicsFamily != presentFamily)
        {
            createInfo.PQueueFamilyIndices = ptr1;
            createInfo.ImageSharingMode = SharingMode.Concurrent;
            createInfo.QueueFamilyIndexCount = 2;
        }
        else
        {
            createInfo.ImageSharingMode = SharingMode.Exclusive;
            createInfo.QueueFamilyIndexCount = 0;
            createInfo.PQueueFamilyIndices = null;
        }

        if (_khrs.CreateSwapchain(device, ref createInfo, null, out _swapchain) != Result.Success)
        {
            CubismLog.Error("failed to create swap chain");
        }

        // 获取 swapchain image
        _khrs.GetSwapchainImages(device, _swapchain, ref _imageCount, null);
        Images = new Image[_imageCount];

        fixed (Image* ptr = Images)
        {
            _khrs.GetSwapchainImages(device, _swapchain, ref _imageCount, ptr);
        }

        ImageViews = new ImageView[_imageCount];
        for (int i = 0; i < _imageCount; i++)
        {
            var viewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = Images[i],
                ViewType = ImageViewType.Type2D,
                Format = SwapchainFormat,
                Components = new(0, 0, 0, 0),
                SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };

            if (_vk.CreateImageView(device, ref viewInfo, null, out ImageViews[i]) != Result.Success)
            {
                CubismLog.Error("failed to create texture image view");
            }
        }
    }

    /// <summary>
    /// 把显示命令放入队列
    /// </summary>
    /// <param name="queue">队列</param>
    /// <param name="imageIndex">image 的索引</param>
    /// <returns></returns>
    public unsafe Result QueuePresent(Queue queue, uint imageIndex)
    {
        var presentInfo = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr
        };

        var swapChains = stackalloc SwapchainKHR[] { _swapchain };
        presentInfo.SwapchainCount = 1;
        presentInfo.PSwapchains = swapChains;
        presentInfo.PImageIndices = &imageIndex;
        return _khrs.QueuePresent(queue, ref presentInfo);
    }

    /// <summary>
    /// 变更 swapchain 的布局
    /// </summary>
    /// <param name="device">逻辑设备</param>
    /// <param name="commandPool">命令池</param>
    /// <param name="queue">队列</param>
    public unsafe void ChangeLayout(Device device, CommandPool commandPool, Queue queue)
    {
        for (int i = 0; i < _imageCount; i++)
        {
            var barrier = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.PresentSrcKhr,
                Image = Images[i],
                SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
                SrcAccessMask = 0
            };

            var sourceStage = PipelineStageFlags.AllCommandsBit;
            var destinationStage = PipelineStageFlags.AllCommandsBit;

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                Level = CommandBufferLevel.Primary,
                CommandPool = commandPool,
                CommandBufferCount = 1
            };

            _vk.AllocateCommandBuffers(device, ref allocInfo, out var commandBuffer);

            var beginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };

            _vk.BeginCommandBuffer(commandBuffer, ref beginInfo);

            _vk.CmdPipelineBarrier(
                commandBuffer,
                sourceStage,
                destinationStage,
                0,
                0,
                null,
                0,
                null,
                1,
                &barrier
            );

            _vk.EndCommandBuffer(commandBuffer);

            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer
            };

            _vk.QueueSubmit(queue, 1, ref submitInfo, default);
            _vk.QueueWaitIdle(queue);
            _vk.FreeCommandBuffers(device, commandPool, 1, ref commandBuffer);
        }


        //sourceStage = VK_PIPELINE_STAGE_ALL_COMMANDS_BIT;
        //destinationStage = VK_PIPELINE_STAGE_ALL_COMMANDS_BIT;
    }

    /// <summary>
    /// 销毁并重建资源
    /// </summary>
    /// <param name="device">设备</param>
    public unsafe void Cleanup(Device device)
    {
        if (_swapchain.Handle != 0)
        {
            for (int i = 0; i < ImageViews.Length; i++)
            {
                _vk.DestroyImageView(device, ImageViews[i], null);
            }
            Images = [];
            ImageViews = [];
            _khrs.DestroySwapchain(device, _swapchain, null);
            // 置空句柄: 退出路径可能重复调用 Cleanup, 避免二次销毁
            _swapchain = default;
            Extent = new(0, 0);
        }
    }
}
