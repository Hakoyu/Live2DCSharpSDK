using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Live2DCSharpSDK.Vulkan;

public class VulkanManager(LAppDelegateVulkan lapp, Vk vk, VulkanApi api)
{
    /// <summary>
    /// 宿主(用于获取背景色).
    /// </summary>
    public LAppDelegateVulkan LApp => lapp;

    public static readonly string[] deviceExtensions =
    [
        "VK_KHR_swapchain",
        "VK_EXT_extended_dynamic_state"
    ];

    public static readonly string[] validationLayers = ["VK_LAYER_KHRONOS_validation"];

    /// <summary>
    /// 是否启用验证层
    /// </summary>
    // 对应官方 VulkanManager.hpp 的 CSM_DEBUG 开关: Debug 构建启用, Release 关闭.
    // 原实现无条件 true, Release 机器未装 Vulkan SDK 时 CheckValidationLayerSupport 会直接抛异常.
#if DEBUG
    public const bool EnableValidationLayers = true;
#else
    public const bool EnableValidationLayers = false;
#endif

    /// <summary>
    /// image 格式
    /// </summary>
    public const Format SurfaceFormat = Format.R8G8B8A8Unorm;
    private KhrSwapchain _khrs;

    public QueueFamilyIndices Indices = new();

    /// <summary>
    /// 用于初始化库的 instance
    /// </summary>
    private Instance _instance;
    private KhrSurface khrSurface;
    /// <summary>
    /// 为在窗口系统中绘制所需的 surface
    /// </summary>
    public SurfaceKHR Surface { get; private set; }
    /// <summary>
    /// 物理设备
    /// </summary>
    public PhysicalDevice PhysicalDevice { get; private set; }
    /// <summary>
    /// 逻辑设备
    /// </summary>
    public Device Device => _device;

    private Device _device;

    /// <summary>
    /// 用于提交绘制命令的队列
    /// </summary>
    public Queue GraphicQueue => _graphicQueue;
    private Queue _graphicQueue;

    /// <summary>
    /// 用于显示命令的队列
    /// </summary>
    public Queue PresentQueue => _presentQueue;
    private Queue _presentQueue;
    /// <summary>
    /// 创建命令缓冲区所需的命令池
    /// </summary>
    public CommandPool CommandPool => _commandPool;

    private CommandPool _commandPool;
    /// <summary>
    /// semaphore
    /// </summary>
    private Semaphore _imageAvailableSemaphore;
    /// <summary>
    /// 负责 swapchain 管理的 swapchain 管理器
    /// </summary>
    public SwapchainManager SwapchainManager { get; private set; }
    /// <summary>
    /// 窗口尺寸是否已变更的标志
    /// </summary>
    public bool IsSwapchainInvalid { get; set; }
    /// <summary>
    ///  当前的 image 索引
    /// </summary>
    private uint _imageIndex = 0;
    /// <summary>
    /// 深度格式
    /// </summary>
    public Format DepthFormat { get; private set; }
    /// <summary>
    /// framebuffer 尺寸是否已变更
    /// </summary>
    public bool FramebufferResized { get; set; }

    private ExtDebugUtils? debugUtils;
    private DebugUtilsMessengerEXT debugMessenger;
    /// <summary>
    /// 是否已完成销毁, 防止退出路径重复释放
    /// </summary>
    private bool _destroyed;

    /// <summary>
    /// 确认验证层的支持情况
    /// </summary>
    /// <returns></returns>
    public unsafe bool CheckValidationLayerSupport()
    {
        uint layerCount = 0;
        vk.EnumerateInstanceLayerProperties(ref layerCount, null);

        var availableLayers = new LayerProperties[layerCount];
        fixed (LayerProperties* availableLayersPtr = availableLayers)
        {
            vk.EnumerateInstanceLayerProperties(ref layerCount, availableLayersPtr);
        }

        var availableLayerNames = availableLayers.Select(layer => Marshal.PtrToStringAnsi((IntPtr)layer.LayerName)).ToHashSet();

        return validationLayers.All(availableLayerNames.Contains);
    }

    /// <summary>
    /// 获取所需的扩展功能
    /// </summary>
    /// <returns>所需扩展的数组</returns>
    public unsafe string[] GetRequiredExtensions()
    {
        var glfwExtensions = api.GetRequiredExtensions(out var glfwExtensionCount);
        var extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);

        if (EnableValidationLayers)
        {
            return [.. extensions, ExtDebugUtils.ExtensionName];
        }

        return extensions;
    }

    private unsafe uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
    {
        CubismLog.Debug($"validation layer:" + Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage));

        return Vk.False;
    }

    /// <summary>
    /// 启用调试消息
    /// </summary>
    /// <param name="createInfo">调试 messenger 对象的信息</param>
    public unsafe void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
    {
        createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
        createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;
        createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;
        createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
    }

    /// <summary>
    /// 创建 instance
    /// </summary>
    public unsafe void CreateInstance()
    {
        // 确认验证层启用时是否可用
        if (EnableValidationLayers && !CheckValidationLayerSupport())
        {
            CubismLog.Error("validation layers requested, but not available!");
        }

        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)SilkMarshal.StringToPtr("Live2D"),
            PEngineName = (byte*)SilkMarshal.StringToPtr("Live2D"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version13
        };

        var createInfo = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo
        };

        var extensions = GetRequiredExtensions();
        createInfo.EnabledExtensionCount = (uint)extensions.Length;
        createInfo.PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions);
        createInfo.EnabledLayerCount = 0;
        createInfo.PNext = null;

        if (EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)validationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);

            DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
            PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
            createInfo.PNext = &debugCreateInfo;
        }
        else
        {
            createInfo.EnabledLayerCount = 0;
            createInfo.PNext = null;
        }

        if (vk.CreateInstance(ref createInfo, null, out _instance) != Result.Success)
        {
            throw new Exception("failed to create instance!");
        }

        Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
        Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
#if DEBUG
        if (EnableValidationLayers)
        {
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
        }
#endif
    }

    /// <summary>
    /// 启用调试消息
    /// </summary>
    public unsafe void SetupDebugMessenger()
    {
        if (!EnableValidationLayers) return;

        //TryGetInstanceExtension equivilant to method CreateDebugUtilsMessengerEXT from original tutorial.
        if (!vk.TryGetInstanceExtension(_instance, out debugUtils)) return;

        DebugUtilsMessengerCreateInfoEXT createInfo = new();
        PopulateDebugMessengerCreateInfo(ref createInfo);

        if (debugUtils!.CreateDebugUtilsMessenger(_instance, in createInfo, null, out debugMessenger) != Result.Success)
        {
            throw new Exception("failed to set up debug messenger!");
        }
    }

    /// <summary>
    /// 检查设备的扩展
    /// </summary>
    /// <param name="physicalDevice">物理设备</param>
    /// <returns></returns>
    public unsafe bool CheckDeviceExtensionSupport(PhysicalDevice physicalDevice)
    {
        uint extentionsCount = 0;
        vk!.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, ref extentionsCount, null);

        var availableExtensions = new ExtensionProperties[extentionsCount];
        fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
        {
            vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, ref extentionsCount, availableExtensionsPtr);
        }

        var availableExtensionNames = availableExtensions.Select(extension => Marshal.PtrToStringAnsi((IntPtr)extension.ExtensionName)).ToHashSet();

        return deviceExtensions.All(availableExtensionNames.Contains);
    }

    /// <summary>
    /// 创建 surface
    /// </summary>
    public unsafe void CreateSurface()
    {
        if (!vk.TryGetInstanceExtension(_instance, out khrSurface))
        {
            throw new NotSupportedException("KHR_surface extension not found.");
        }

        Surface = api.CreateSurface<AllocationCallbacks>(_instance.ToHandle(), null).ToSurface();
    }

    /// <summary>
    /// 查找队列族
    /// </summary>
    /// <param name="device">物理设备</param>
    public unsafe void FindQueueFamilies(PhysicalDevice device)
    {
        uint queueFamilityCount = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilityCount, null);

        var queueFamilies = new QueueFamilyProperties[queueFamilityCount];
        fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
        {
            vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilityCount, queueFamiliesPtr);
        }

        uint i = 0;
        foreach (var queueFamily in queueFamilies)
        {
            if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                Indices.GraphicsFamily = (int)i;
            }

            khrSurface!.GetPhysicalDeviceSurfaceSupport(device, i, Surface, out var presentSupport);

            if (presentSupport)
            {
                Indices.PresentFamily = (int)i;
            }

            if (Indices.IsComplete())
            {
                break;
            }

            i++;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="device">确认设备是否可用</param>
    /// <returns>物理设备</returns>
    public bool IsDeviceSuitable(PhysicalDevice device)
    {
        FindQueueFamilies(device);
        bool extensionsSupported = CheckDeviceExtensionSupport(device);
        bool swapChainAdequate = false;
        // 设备扩展功能受支持时, 确认是否支持 swapchain
        if (extensionsSupported)
        {
            var swapchainSupport = SwapchainManager.QuerySwapchainSupport(khrSurface, device, Surface);
            swapChainAdequate = swapchainSupport.Formats.Length != 0 && swapchainSupport.PresentModes.Length != 0;
        }

        vk.GetPhysicalDeviceFeatures(device, out var supportedFeatures);

        return Indices.IsComplete() && swapChainAdequate && supportedFeatures.SamplerAnisotropy;
    }

    /// <summary>
    /// 获取物理设备
    /// </summary>
    public void PickPhysicalDevice()
    {
        var devices = vk.GetPhysicalDevices(_instance);

        foreach (var device in devices)
        {
            if (IsDeviceSuitable(device))
            {
                PhysicalDevice = device;
                break;
            }
        }

        if (PhysicalDevice.Handle == 0)
        {
            throw new Exception("failed to find a suitable GPU!");
        }
    }

    /// <summary>
    /// 创建逻辑设备
    /// </summary>
    public unsafe void CreateLogicalDevice()
    {
        FindQueueFamilies(PhysicalDevice);

        var uniqueQueueFamilies = new[] { (uint)Indices.GraphicsFamily, (uint)Indices.PresentFamily };
        uniqueQueueFamilies = uniqueQueueFamilies.Distinct().ToArray();

        using var mem = GlobalMemory.Allocate(uniqueQueueFamilies.Length * sizeof(DeviceQueueCreateInfo));
        var queueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref mem.GetPinnableReference());

        float queuePriority = 1.0f;
        for (int i = 0; i < uniqueQueueFamilies.Length; i++)
        {
            queueCreateInfos[i] = new()
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = uniqueQueueFamilies[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };
        }

        DeviceCreateInfo createInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
            PQueueCreateInfos = queueCreateInfos,
            EnabledExtensionCount = (uint)deviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(deviceExtensions)
        };

        // Vulkan 1.0 起设备层已禁用. 仅使用实例层(对应 validation VUID).
        createInfo.EnabledLayerCount = 0;
        createInfo.PpEnabledLayerNames = null;

        var dynamicRenderingF = new PhysicalDeviceDynamicRenderingFeatures
        {
            SType = StructureType.PhysicalDeviceDynamicRenderingFeaturesKhr,
            DynamicRendering = true
        };

        var dynamicStateF = new PhysicalDeviceExtendedDynamicStateFeaturesEXT
        {
            SType = StructureType.PhysicalDeviceExtendedDynamicStateFeaturesExt,
            ExtendedDynamicState = true,
            PNext = &dynamicRenderingF
        };

        var deviceFeatures2 = new PhysicalDeviceFeatures2
        {
            SType = StructureType.PhysicalDeviceFeatures2,
            PNext = &dynamicStateF
        };
        vk.GetPhysicalDeviceFeatures2(PhysicalDevice, &deviceFeatures2);
        createInfo.PNext = &deviceFeatures2;

        if (vk.CreateDevice(PhysicalDevice, in createInfo, null, out _device) != Result.Success)
        {
            throw new Exception("failed to create logical device!");
        }

        vk.GetDeviceQueue(_device, (uint)Indices.GraphicsFamily, 0, out _graphicQueue);
        vk.GetDeviceQueue(_device, (uint)Indices.PresentFamily, 0, out _presentQueue);

        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
    }

    /// <summary>
    /// 创建深度格式
    /// </summary>
    public void ChooseSupportedDepthFormat()
    {
        Format[] depthFormats =
        [
            Format.D32SfloatS8Uint, Format.D32Sfloat,
            Format.D24UnormS8Uint,
            Format.D16UnormS8Uint, Format.D16Unorm,
        ];

        for (int i = 0; i < depthFormats.Length; i++)
        {
            vk.GetPhysicalDeviceFormatProperties(PhysicalDevice, depthFormats[i], out var formatProps);

            if ((formatProps.OptimalTilingFeatures & FormatFeatureFlags.DepthStencilAttachmentBit)
                == FormatFeatureFlags.DepthStencilAttachmentBit)
            {
                DepthFormat = depthFormats[i];
                return;
            }
        }
        CubismLog.Error("can't find depth format!");
        DepthFormat = depthFormats[0];
    }

    /// <summary>
    /// 创建命令池
    /// </summary>
    public unsafe void CreateCommandPool()
    {
        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = (uint)Indices.GraphicsFamily
        };

        if (vk.CreateCommandPool(_device, ref poolInfo, null, out _commandPool) != Result.Success)
        {
            CubismLog.Error("failed to create graphics command pool!");
        }
    }

    /// <summary>
    /// 创建同步对象
    /// </summary>
    public unsafe void CreateSyncObjects()
    {
        var semaphoreInfo = new SemaphoreCreateInfo()
        {
            SType = StructureType.SemaphoreCreateInfo
        };
        vk.CreateSemaphore(_device, ref semaphoreInfo, null, out _imageAvailableSemaphore);
    }

    /// <summary>
    /// 初始化
    /// </summary>
    public void Initialize()
    {
        CreateInstance();
        SetupDebugMessenger();
        CreateSurface();
        PickPhysicalDevice();
        CreateLogicalDevice();

        if (_khrs is null)
        {
            if (!vk.TryGetDeviceExtension(_instance, _device, out _khrs))
            {
                CubismLog.Error("VK_KHR_swapchain extension not found.");
            }
        }

        ChooseSupportedDepthFormat();
        SwapchainManager = new SwapchainManager(_khrs, khrSurface, api, vk, PhysicalDevice, _device, Surface, Indices.GraphicsFamily,
                                                Indices.PresentFamily);
        CreateCommandPool();
        CreateSyncObjects();
    }

    /// <summary>
    /// 开始记录命令
    /// </summary>
    /// <returns></returns>
    public unsafe CommandBuffer BeginSingleTimeCommands()
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = _commandPool,
            CommandBufferCount = 1
        };

        vk.AllocateCommandBuffers(_device, ref allocInfo, out var commandBuffer);

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        vk.BeginCommandBuffer(commandBuffer, ref beginInfo);

        return commandBuffer;
    }

    /// <summary>
    /// 提交命令
    /// </summary>
    /// <param name="commandBuffer">命令缓冲区</param>
    /// <param name="isFirstDraw">是否为首次绘制命令</param>
    public unsafe void SubmitCommand(CommandBuffer commandBuffer, bool isFirstDraw = false)
    {
        vk.EndCommandBuffer(commandBuffer);

        var submitInfo = new SubmitInfo
        {
            SType = StructureType
            .SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer
        };
        var sem = _imageAvailableSemaphore;
        var waitStages = stackalloc PipelineStageFlags[] { PipelineStageFlags.ColorAttachmentOutputBit };
        if (isFirstDraw)
        {
            submitInfo.WaitSemaphoreCount = 1;
            submitInfo.PWaitSemaphores = &sem;
            submitInfo.PWaitDstStageMask = waitStages;
        }
        vk.QueueSubmit(_graphicQueue, 1, &submitInfo, default);
        // 等待命令执行结束
        vk.QueueWaitIdle(_graphicQueue);
        vk.FreeCommandBuffers(_device, _commandPool, 1, &commandBuffer);
    }

    /// <summary>
    /// 绘制
    /// </summary>
    public void UpdateDrawFrame()
    {
        Result result = _khrs.AcquireNextImage(_device, SwapchainManager.Swapchain, uint.MaxValue,
        _imageAvailableSemaphore, default,
        ref _imageIndex);
        if (result == Result.ErrorOutOfDateKhr)
        {
            IsSwapchainInvalid = true;
        }
        else if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            CubismLog.Error("failed to acquire swap chain image!");
        }
    }

    /// <summary>
    /// 绘制
    /// </summary>
    public void PostDraw()
    {
        Result result = SwapchainManager.QueuePresent(_presentQueue, _imageIndex);
        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr || FramebufferResized)
        {
            FramebufferResized = false;
            IsSwapchainInvalid = true;
        }
        else if (result != Result.Success)
        {
            CubismLog.Error("failed to present swap chain image!");
        }
    }

    /// <summary>
    /// 重建 swapchain
    /// </summary>
    public void RecreateSwapchain()
    {
        vk.DeviceWaitIdle(_device);
        SwapchainManager.Cleanup(_device);
        SwapchainManager.CreateSwapchain(PhysicalDevice, _device, Surface, Indices.GraphicsFamily,
                                          Indices.PresentFamily);
        SwapchainManager.ChangeLayout(_device, _commandPool, _graphicQueue);
    }

    /// <summary>
    /// 销毁资源
    /// 可重复调用: 已释放的句柄会被跳过, 不会重复释放.
    /// </summary>
    public unsafe void Destroy()
    {
        if (_destroyed)
        {
            return;
        }

        _destroyed = true;

        // 先等待 GPU 空闲, 避免销毁仍在使用的交换链/命令池资源(退出路径).
        if (_device.Handle != 0)
        {
            vk.DeviceWaitIdle(_device);
        }

        SwapchainManager.Cleanup(_device);

        if (_imageAvailableSemaphore.Handle != 0)
        {
            vk.DestroySemaphore(_device, _imageAvailableSemaphore, default);
            _imageAvailableSemaphore = default;
        }

        if (EnableValidationLayers && debugMessenger.Handle != 0)
        {
            debugUtils?.DestroyDebugUtilsMessenger(_instance, debugMessenger, null);
            debugMessenger = default;
        }

        if (_commandPool.Handle != 0)
        {
            vk.DestroyCommandPool(_device, _commandPool, default);
            _commandPool = default;
        }

        // 在销毁设备前释放绑定到设备的 pipeline 与离屏资源(对应官方 ReleaseAllDeviceInfo).
        // 渲染器释放后此处通常已为空, 仅作为兜底, 重复调用安全.
        CubismDeviceInfo_Vulkan.ReleaseAllDeviceInfo();

        if (_device.Handle != 0)
        {
            vk.DestroyDevice(_device, null);
            _device = default;
        }

        if (Surface.Handle != 0 && khrSurface is not null)
        {
            khrSurface.DestroySurface(_instance, Surface, null);
            Surface = default;
        }

        if (_instance.Handle != 0)
        {
            vk.DestroyInstance(_instance, null);
            _instance = default;
        }
    }

    /// <summary>
    /// 获取 swapchain image
    /// </summary>
    /// <returns>swapchain image</returns>
    public Image GetSwapchainImage() { return SwapchainManager.Images[_imageIndex]; }

    /// <summary>
    /// 获取 swapchain image
    /// </summary>
    /// <returns>swapchain image</returns>
    public ImageView GetSwapchainImageView() { return SwapchainManager.ImageViews[_imageIndex]; }

    public void Dispose()
    {
        Destroy();
    }
}