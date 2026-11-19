using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Live2DCSharpSDK.Vulkan;

public class CubismRenderer_Vulkan : CubismRenderer
{
    public delegate void FvkCmdSetCullMode(CommandBuffer commandBuffer, CullModeFlags cullMode);

    public static Device Device { get; set; }
    public static PhysicalDevice PhysicalDevice { get; set; }
    public static CommandPool CommandPool { get; set; }
    public static Queue Queue { get; set; }
    public static Extent2D SwapchainExtent { get; set; }
    public static Format SwapchainImageFormat { get; set; }
    public static Format ImageFormat { get; set; }
    public static Image SwapchainImage { get; set; }
    public static ImageView SwapchainImageView { get; set; }
    public static Format DepthFormat { get; set; }
    public static bool UseRenderTarget { get; set; }
    public static Image RenderTargetImage { get; set; }
    public static ImageView RenderTargetImageView { get; set; }

    /// <summary>
    /// 创建渲染器所需的各种设置
    /// 在加载模型之前只调用一次
    /// </summary>
    /// <param name="device">逻辑设备</param>
    /// <param name="physicalDevice">物理设备</param>
    /// <param name="commandPool">命令池</param>
    /// <param name="queue">队列</param>
    /// <param name="extent">绘制分辨率</param>
    /// <param name="depthFormat">深度格式</param>
    /// <param name="surfaceFormat">framebuffer 的格式</param>
    /// <param name="swapchainImage"></param>
    /// <param name="swapchainImageView">swapchain 的 image view</param>
    /// <param name="dFormat"></param>
    public static void SetConstantSettings(Device device, PhysicalDevice physicalDevice,
                                           CommandPool commandPool, Queue queue,
                                           Extent2D extent, Format depthFormat, Format surfaceFormat,
                                           Image swapchainImage,
                                           ImageView swapchainImageView,
                                           Format dFormat)
    {
        Device = device;
        PhysicalDevice = physicalDevice;
        CommandPool = commandPool;
        Queue = queue;
        SwapchainExtent = extent;
        SwapchainImageFormat = depthFormat;
        ImageFormat = surfaceFormat;
        SwapchainImage = swapchainImage;
        SwapchainImageView = swapchainImageView;
        DepthFormat = dFormat;
    }

    /// <summary>
    /// 启用渲染目标变更
    /// </summary>
    public static void EnableChangeRenderTarget()
    {
        UseRenderTarget = true;
    }

    /// <summary>
    /// 变更渲染目标时创建渲染器所需的各种设置
    /// </summary>
    /// <param name="image">image</param>
    /// <param name="imageview">image view</param>
    public static void SetRenderTarget(Image image, ImageView imageview)
    {
        RenderTargetImage = image;
        RenderTargetImageView = imageview;
    }

    /// <summary>
    /// 更新矩阵
    /// </summary>
    /// <param name="vkMat4">要更新的矩阵</param>
    /// <param name="cubismMat">新值</param>
    public static unsafe void UpdateMatrix(float* vkMat4, CubismMatrix44 cubismMat)
    {
        for (int i = 0; i < 16; i++)
        {
            vkMat4[i] = cubismMat.Tr[i];
        }
    }

    /// <summary>
    /// 更新颜色向量
    /// </summary>
    /// <param name="vkVec4">要更新的颜色向量</param>
    /// <param name="r">新值</param>
    /// <param name="g">新值</param>
    /// <param name="b">新值</param>
    /// <param name="a">新值</param>
    public static unsafe void UpdateColor(float* vkVec4, float r, float g, float b, float a)
    {
        vkVec4[0] = r;
        vkVec4[1] = g;
        vkVec4[2] = b;
        vkVec4[3] = a;
    }

    private static unsafe void SetColorChannel(ModelUBO* ubo, CubismClippingContext_Vulkan contextBuffer)
    {
        int channelIndex = contextBuffer.LayoutChannelIndex;
        var colorChannel = contextBuffer.Manager.GetChannelFlagAsColor(channelIndex);
        UpdateColor(ubo->ChannelFlag, colorChannel.R, colorChannel.G, colorChannel.B, colorChannel.A);
    }

    private static unsafe void SetColorUniformBuffer(ModelUBO* ubo, CubismTextureColor baseColor,
                                              CubismTextureColor multiplyColor, CubismTextureColor screenColor)
    {
        UpdateColor(ubo->BaseColor, baseColor.R, baseColor.G, baseColor.B, baseColor.A);
        UpdateColor(ubo->MultiplyColor, multiplyColor.R, multiplyColor.G, multiplyColor.B, multiplyColor.A);
        UpdateColor(ubo->ScreenColor, screenColor.R, screenColor.G, screenColor.B, screenColor.A);
    }

    private readonly Vk _vk;

    /// <summary>
    /// 每个设备的 pipeline 与离屏管理器(官方 CubismDeviceInfo_Vulkan)
    /// </summary>
    private CubismDeviceInfo_Vulkan _deviceInfo;

    /// <summary>
    /// 防止 Dispose 重复执行
    /// </summary>
    private bool _disposed;

    private FvkCmdSetCullMode vkCmdSetCullModeEXT;

    /// <summary>
    /// 裁剪遮罩管理对象
    /// </summary>
    private CubismClippingManager_Vulkan _clippingManager;
    /// <summary>
    /// 离屏用裁剪遮罩管理对象.
    /// </summary>
    private CubismClippingManager_Vulkan? _offscreenClippingManager;
    /// <summary>
    /// 离屏用裁剪上下文.
    /// </summary>
    public CubismClippingContext_Vulkan? ClippingContextBufferForOffscreen { get; set; }
    /// <summary>
    /// 模型的离屏列表(官方 _offscreenList)
    /// </summary>
    private readonly List<CubismOffscreenRenderTarget_Vulkan> _offscreenList = [];
    /// <summary>
    /// 当前正在绘制的离屏(官方 _currentOffscreen)
    /// </summary>
    private CubismOffscreenRenderTarget_Vulkan? _currentOffscreen;
    /// <summary>
    /// 当前的绘制目标渲染目标(官方 _currentRenderTarget)
    /// </summary>
    private CubismRenderTarget_Vulkan? _currentRenderTarget;
    /// <summary>
    /// 绘制整个模型的渲染目标(官方 _modelRenderTargets)
    /// </summary>
    private readonly List<CubismRenderTarget_Vulkan> _modelRenderTargets = [];
    /// <summary>
    /// blend shader 用的快照(直接采样绘制中的 RT 会导致 validation error)
    /// </summary>
    private CubismRenderTarget_Vulkan? _blendSnapshot;
    private bool _blendSnapshotInitialized;
    /// <summary>
    /// 离屏用遮罩缓冲区(官方 _offscreenMaskBuffers)
    /// </summary>
    private readonly List<CubismRenderTarget_Vulkan> _offscreenMaskBuffers = [];
    /// <summary>
    /// 是否因使用 blend / offscreen 而经由模型 RT 绘制
    /// </summary>
    private bool _useModelRenderTarget;
    /// <summary>
    /// 模型 RT 是否已清除(官方 _isClearedModelRenderTarget)
    /// </summary>
    private bool _isClearedModelRenderTarget;
    /// <summary>
    /// 是否正在绘制离屏(用于选择遮罩缓冲区)
    /// </summary>
    private bool _isDrawingOffscreen;
    /// <summary>
    /// 离屏合成用 descriptor(index = offscreenIndex)
    /// </summary>
    private readonly List<Descriptor> _offscreenDescriptors = [];
    /// <summary>
    /// 模型 RT / 离屏合成用拷贝 descriptor
    /// </summary>
    private Descriptor? _copyDescriptor;
    /// <summary>
    /// 合成用全屏 quad 的顶点/索引 buffer
    /// </summary>
    private CubismBufferVulkan? _quadVertexBuffer;
    private CubismBufferVulkan? _quadStagingBuffer;
    private CubismBufferVulkan? _quadIndexBuffer;
    /// <summary>
    /// 按绘制顺序排列的绘制对象索引列表
    /// </summary>
    private readonly int[] _sortedObjectsIndexList;
    /// <summary>
    /// 按绘制顺序排列的对象种类(renderOrder 的长度为 drawableCount + offscreenCount).
    /// </summary>
    private readonly CsmEnum.DrawableObjectType[] _sortedObjectsTypeList;
    /// <summary>
    /// 用于绘制到遮罩纹理的裁剪上下文
    /// </summary>
    public CubismClippingContext_Vulkan? ClippingContextBufferForMask { get; set; }
    /// <summary>
    /// 用于在画面上绘制的裁剪上下文
    /// </summary>
    public CubismClippingContext_Vulkan? ClippingContextBufferForDraw { get; set; }
    /// <summary>
    /// 遮罩绘制用的 framebuffer
    /// </summary>
    private CubismRenderTarget_Vulkan[]? _offscreenFrameBuffers;
    /// <summary>
    /// 顶点 buffer
    /// </summary>
    private CubismBufferVulkan[] _vertexBuffers;
    /// <summary>
    /// 更新顶点 buffer 时使用的 staging buffer
    /// </summary>
    private readonly List<CubismBufferVulkan> _stagingBuffers = [];
    /// <summary>
    /// 索引 buffer
    /// </summary>
    private CubismBufferVulkan[] _indexBuffers;
    /// <summary>
    /// descriptor pool
    /// </summary>
    private DescriptorPool _descriptorPool;
    /// <summary>
    /// descriptor set 的布局
    /// </summary>
    private DescriptorSetLayout _descriptorSetLayout;
    /// <summary>
    /// descriptor 管理对象
    /// </summary>
    private readonly List<Descriptor> _descriptorSets = [];
    /// <summary>
    /// 模型使用的纹理
    /// </summary>
    private readonly List<CubismImageVulkan> _textures = [];
    /// <summary>
    /// 保存离屏颜色信息的深度 image
    /// </summary>
    private readonly CubismImageVulkan _depthImage;
    /// <summary>
    /// semaphore
    /// </summary>
    private Semaphore _updateFinishedSemaphore;
    /// <summary>
    /// 更新用命令缓冲区
    /// </summary>
    private CommandBuffer _updateCommandBuffer;
    /// <summary>
    /// 绘制用命令缓冲区
    /// </summary>
    private CommandBuffer _drawCommandBuffer;

    /// <summary>
    /// swapchain / 深度 image 的当前布局(供 validation 的简易跟踪)
    /// </summary>
    private readonly Dictionary<ulong, ImageLayout> _imageLayouts = [];

    private readonly VulkanManager _vulkanManager;

    /// <summary>
    /// 模型的离屏数量(用于验证).
    /// </summary>
    public int OffscreenCount => _offscreenList.Count;

    /// <summary>
    /// DrawOffscreen 的执行次数(用于验证).
    /// </summary>
    public int OffscreenDrawCount { get; private set; }

    public CubismRenderer_Vulkan(Vk vk, VulkanManager manager, CubismModel model, int maskBufferCount = 1) : base(model)
    {
        _vk = vk;
        _vulkanManager = manager;

        if (Device.Handle == 0)
        {
            CubismLog.Error("Device has not been set.");
            return;
        }

        // 实测: csmGetRenderOrders 的数组长度 = drawableCount + offscreenCount.
        // 确保同时容纳 drawable / offscreen 的顺序数组(官方 DrawObjectLoop:2714-2727).
        int childCount = model.GetDrawableCount() + model.GetOffscreenCount();
        _sortedObjectsIndexList = new int[childCount];
        _sortedObjectsTypeList = new CsmEnum.DrawableObjectType[childCount];
        for (int a = 0; a < childCount; a++)
        {
            _sortedObjectsIndexList[a] = 0;
            _sortedObjectsTypeList[a] = CsmEnum.DrawableObjectType.Drawable;
        }

        if (model.IsUsingMasking())
        {
            // 仅在模型使用遮罩时执行
            _clippingManager = new CubismClippingManager_Vulkan(_vk);
            _clippingManager.Initialize(model, maskBufferCount);

            var bufferWidth = (uint)_clippingManager.ClippingMaskBufferSize.X;
            var bufferHeight = (uint)_clippingManager.ClippingMaskBufferSize.Y;

            _offscreenFrameBuffers = new CubismRenderTarget_Vulkan[maskBufferCount];
            // 按后备缓冲区数量确保空间
            for (int i = 0; i < maskBufferCount; i++)
            {
                _offscreenFrameBuffers[i] = new(_vk);
                if (_offscreenFrameBuffers[i].CreateRenderTarget(Device, PhysicalDevice, bufferWidth, bufferHeight,
                    ImageFormat, DepthFormat) is false)
                {
                    // 遮罩缓冲区是绘制遮罩的必需资源, 创建失败时抛明确异常而不是继续使用无效句柄.
                    throw new InvalidOperationException(
                        $"failed to create clipping mask render target ({bufferWidth}x{bufferHeight}).");
                }

                InitializeMaskBufferLayout(_offscreenFrameBuffers[i]);
            }
        }

        //------------ 离屏用裁剪遮罩 ------------
        if (model.IsUsingMaskingForOffscreen())
        {
            _offscreenClippingManager = new CubismClippingManager_Vulkan(_vk)
            {
                Type = CsmEnum.DrawableObjectType.Offscreen
            };
            _offscreenClippingManager.Initialize(model, maskBufferCount);

            for (int i = 0; i < maskBufferCount; ++i)
            {
                var mask = new CubismRenderTarget_Vulkan(_vk);
                if (mask.CreateRenderTarget(Device, PhysicalDevice,
                    (uint)_offscreenClippingManager.ClippingMaskBufferSize.X,
                    (uint)_offscreenClippingManager.ClippingMaskBufferSize.Y,
                    ImageFormat, DepthFormat) is false)
                {
                    // 离屏遮罩缓冲区同样属于必需资源, 失败时抛明确异常.
                    throw new InvalidOperationException(
                        $"failed to create offscreen clipping mask render target " +
                        $"({_offscreenClippingManager.ClippingMaskBufferSize.X}x{_offscreenClippingManager.ClippingMaskBufferSize.Y}).");
                }

                InitializeMaskBufferLayout(mask);
                _offscreenMaskBuffers.Add(mask);
            }
        }

        //------------ 绘制顺序排序 + 离屏列表 ------------
        int offscreenCount = model.GetOffscreenCount();
        if (offscreenCount > 0)
        {
            for (int offscreenIndex = 0; offscreenIndex < offscreenCount; ++offscreenIndex)
            {
                var renderTarget = new CubismOffscreenRenderTarget_Vulkan();
                renderTarget.SetOffscreenIndex(offscreenIndex);
                _offscreenList.Add(renderTarget);
            }

            SetupParentOffscreens(model, offscreenCount);
        }

        _useModelRenderTarget = model.IsBlendModeEnabled() || offscreenCount > 0;
        ApplyBlendModeMaskPrecision(model.IsBlendModeEnabled());

        _depthImage = new(vk);

        InitializeRenderer();
    }

    /// <summary>
    /// 把遮罩缓冲区转换到初始布局(ShaderReadOnly).
    /// 避免把未使用的遮罩绑定到 descriptor 时产生 validation error.
    /// </summary>
    private unsafe void InitializeMaskBufferLayout(CubismRenderTarget_Vulkan mask)
    {
        if (!mask.IsValid())
        {
            return;
        }

        // 不开启绘制通道, 仅通过布局转换完成初始化
        // (避免 dynamicRenderingUnusedAttachments 的 validation error).
        var commandBuffer = BeginSingleTimeCommands();
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.ShaderReadBit,
            OldLayout = ImageLayout.Undefined,
            NewLayout = ImageLayout.ShaderReadOnlyOptimal,
            Image = mask.GetTextureImage(),
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, ref barrier);
        mask.SetColorImageCurrentLayout(ImageLayout.ShaderReadOnlyOptimal);
        SubmitCommand(commandBuffer);
    }

    /// <summary>
    /// 查找并设置离屏的父级(对应官方 SetupParentOffscreens).
    /// </summary>
    private unsafe void SetupParentOffscreens(CubismModel model, int offscreenCount)
    {
        int* ownerIndices = model.GetOffscreenOwnerIndices();

        for (int offscreenIndex = 0; offscreenIndex < offscreenCount; ++offscreenIndex)
        {
            CubismOffscreenRenderTarget_Vulkan? parentOffscreen = null;
            int ownerIndex = ownerIndices[offscreenIndex];
            int parentIndex = model.GetPartParentPartIndex(ownerIndex);

            while (parentIndex != CubismModel.CubismNoIndex_Parent)
            {
                for (int i = 0; i < offscreenCount; ++i)
                {
                    if (ownerIndices[_offscreenList[i].GetOffscreenIndex()] != parentIndex)
                    {
                        continue;
                    }

                    parentOffscreen = _offscreenList[i];
                    break;
                }

                if (parentOffscreen != null)
                {
                    break;
                }

                parentIndex = model.GetPartParentPartIndex(parentIndex);
            }

            _offscreenList[offscreenIndex].SetParentPartOffscreen(parentOffscreen);
        }
    }

    /// <summary>
    /// 更新重建 swapchain 时会变化的资源
    /// </summary>
    /// <param name="extent">裁剪遮罩缓冲区的大小</param>
    /// <param name="image"></param>
    /// <param name="imageView">image view</param>
    public static void UpdateSwapchainVariable(Extent2D extent, Image image, ImageView imageView)
    {
        SwapchainExtent = extent;
        SwapchainImage = image;
        SwapchainImageView = imageView;
    }

    /// <summary>
    /// 更新 swapchain image
    /// </summary>
    /// <param name="image">image</param>
    /// <param name="imageView">image view</param>
    public static void UpdateRendererSettings(Image image, ImageView imageView)
    {
        SwapchainImage = image;
        SwapchainImageView = imageView;
    }

    public static Viewport GetViewport(float width, float height, float minDepth, float maxDepth)
    {
        return new()
        {
            Width = width,
            Height = height,
            MinDepth = minDepth,
            MaxDepth = maxDepth
        };
    }

    public static Rect2D GetScissor(float offsetX, float offsetY, float width, float height)
    {
        return new()
        {
            Offset = new()
            {
                X = (int)offsetX,
                Y = (int)offsetY
            },
            Extent = new()
            {
                Height = (uint)height,
                Width = (uint)width
            }
        };
    }

    /// <summary>
    /// 开始记录命令.
    /// </summary>
    /// <returns>已开始记录的命令缓冲区</returns>
    public CommandBuffer BeginSingleTimeCommands()
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = CommandPool,
            CommandBufferCount = 1
        };

        _vk.AllocateCommandBuffers(Device, ref allocInfo, out var commandBuffer);

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        _vk.BeginCommandBuffer(commandBuffer, ref beginInfo);

        return commandBuffer;
    }

    /// <summary>
    /// 执行命令.
    /// </summary>
    /// <param name="commandBuffer">命令缓冲区</param>
    /// <param name="signalUpdateFinishedSemaphore">fence</param>
    /// <param name="waitUpdateFinishedSemaphore"></param>
    public unsafe void SubmitCommand(CommandBuffer commandBuffer, Semaphore? signalUpdateFinishedSemaphore = null,
        Semaphore? waitUpdateFinishedSemaphore = null)
    {
        _vk.EndCommandBuffer(commandBuffer);
        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer
        };
        if (waitUpdateFinishedSemaphore is { } sem1)
        {
            var waitStages = new PipelineStageFlags[]
            {
                PipelineStageFlags.VertexInputBit, PipelineStageFlags.ColorAttachmentOutputBit
            };

            submitInfo.WaitSemaphoreCount = 1;
            submitInfo.PWaitSemaphores = &sem1;
            fixed (PipelineStageFlags* ptr = waitStages)
            {
                submitInfo.PWaitDstStageMask = ptr;
                _vk.QueueSubmit(Queue, 1, ref submitInfo, default);
            }
            _vk.QueueWaitIdle(Queue);
        }
        else if (signalUpdateFinishedSemaphore is { } sem2)
        {
            submitInfo.SignalSemaphoreCount = 1;
            submitInfo.PSignalSemaphores = &sem2;
            _vk.QueueSubmit(Queue, 1, ref submitInfo, default);
        }
        else
        {
            _vk.QueueSubmit(Queue, 1, ref submitInfo, default);
            _vk.QueueWaitIdle(Queue);
            _vk.FreeCommandBuffers(Device, CommandPool, 1, ref commandBuffer);
        }
    }

    /// <summary>
    /// 创建命令缓冲区.
    /// </summary>
    public unsafe void CreateCommandBuffer()
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };

        if (_vk.AllocateCommandBuffers(Device, ref allocInfo, out _updateCommandBuffer) != Result.Success
            || _vk.AllocateCommandBuffers(Device, ref allocInfo, out _drawCommandBuffer) != Result.Success)
        {
            CubismLog.Error("[Live2D Vulkan]failed to allocate command buffers!");
        }
    }

    /// <summary>
    /// 创建空的顶点 buffer.
    /// </summary>
    public void CreateVertexBuffer()
    {
        int drawableCount = Model.GetDrawableCount();
        _vertexBuffers = new CubismBufferVulkan[drawableCount];

        for (int drawAssign = 0; drawAssign < drawableCount; drawAssign++)
        {
            int vcount = Model.GetDrawableVertexCount(drawAssign);
            if (vcount == 0)
            {
                // 无顶点的 drawable 不创建 buffer, 绘制时会被跳过
                continue;
            }

            var bufferSize = (ulong)(Unsafe.SizeOf<ModelVertex>() * vcount); // 总长度 = 结构体大小 * 个数

            // 降级策略: staging / vertex buffer 任一步失败时只跳过该 drawable, 不影响其它部件.
            var stagingBuffer = new CubismBufferVulkan(_vk);
            bool stagingOk = stagingBuffer.CreateBuffer(Device, PhysicalDevice, bufferSize,
                                          BufferUsageFlags.TransferSrcBit,
                                          MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            if (stagingOk)
            {
                stagingOk = stagingBuffer.Map(Device, bufferSize);
            }

            // 始终占位, 保证 _stagingBuffers 的下标与 drawAssign 一致
            _stagingBuffers.Add(stagingBuffer);

            if (stagingOk is false)
            {
                CubismLog.Warning("[Live2D Vulkan]failed to create vertex staging buffer for drawable {0}.", drawAssign);
                continue;
            }

            var vertexBuffer = new CubismBufferVulkan(_vk);
            if (vertexBuffer.CreateBuffer(Device, PhysicalDevice, bufferSize,
                                           BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
                                           MemoryPropertyFlags.DeviceLocalBit) is false)
            {
                CubismLog.Warning("[Live2D Vulkan]failed to create vertex buffer for drawable {0}.", drawAssign);
                continue;
            }

            _vertexBuffers[drawAssign] = vertexBuffer;
        }
    }

    /// <summary>
    /// 创建索引 buffer.
    /// </summary>
    public unsafe void CreateIndexBuffer()
    {
        int drawableCount = Model.GetDrawableCount();
        _indexBuffers = new CubismBufferVulkan[drawableCount];

        for (int drawAssign = 0; drawAssign < drawableCount; drawAssign++)
        {
            int icount = Model.GetDrawableVertexIndexCount(drawAssign);
            if (icount == 0)
            {
                continue;
            }

            var bufferSize = (ulong)(2 * icount);
            var indices = Model.GetDrawableVertexIndices(drawAssign);

            // 降级策略: 索引 buffer 创建失败时只跳过该 drawable.
            var stagingBuffer = new CubismBufferVulkan(_vk);
            bool stagingOk = stagingBuffer.CreateBuffer(Device, PhysicalDevice, bufferSize,
                                           BufferUsageFlags.TransferSrcBit,
                                           MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            if (stagingOk)
            {
                stagingOk = stagingBuffer.Map(Device, bufferSize) && stagingBuffer.MemCpy(indices, bufferSize);
            }

            stagingBuffer.UnMap(Device);

            if (stagingOk is false)
            {
                CubismLog.Warning("[Live2D Vulkan]failed to create index staging buffer for drawable {0}.", drawAssign);
                stagingBuffer.Destroy(Device);
                continue;
            }

            var indexBuffer = new CubismBufferVulkan(_vk);
            if (indexBuffer.CreateBuffer(Device, PhysicalDevice, bufferSize,
                                         BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
                                         MemoryPropertyFlags.DeviceLocalBit) is false)
            {
                CubismLog.Warning("[Live2D Vulkan]failed to create index buffer for drawable {0}.", drawAssign);
                stagingBuffer.Destroy(Device);
                continue;
            }

            var commandBuffer = BeginSingleTimeCommands();

            var copyRegion = new BufferCopy
            {
                Size = bufferSize
            };
            _vk.CmdCopyBuffer(commandBuffer, stagingBuffer.Buffer, indexBuffer.Buffer, 1, ref copyRegion);
            SubmitCommand(commandBuffer);
            _indexBuffers[drawAssign] = indexBuffer;
            stagingBuffer.Destroy(Device);
        }
    }

    /// <summary>
    /// 创建 uniform buffer 与 descriptor set.
    /// descriptor set 布局指定 1 个 uniform buffer, 1 个模型用纹理与 1 个遮罩用纹理.
    /// </summary>
    public unsafe void CreateDescriptorSets()
    {
        // 创建 descriptor pool
        int drawableCount = Model.GetDrawableCount();
        int offscreenCount = Model.GetOffscreenCount();
        // 共 3 张纹理: binding1=模型, binding2=遮罩, binding3=blend 用.
        int textureCount = 3;
        int drawModeCount = 2; // 被遮罩绘制与通常绘制
        // drawable 用 + 离屏合成用(masked/unmasked) + copy 用
        int descriptorSetCount = drawableCount * drawModeCount + offscreenCount * drawModeCount + 1;

        var poolSizes = new DescriptorPoolSize[]
        {
            new()
            {
                Type = DescriptorType.UniformBuffer,
                DescriptorCount = (uint)descriptorSetCount
            },
            new()
            {
                Type = DescriptorType.CombinedImageSampler,
                DescriptorCount = (uint)(descriptorSetCount * textureCount) // drawableCount * 绘制方式数量 * 纹理数量
            }
        };

        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = (uint)poolSizes.Length,
            MaxSets = (uint)descriptorSetCount
        };
        fixed (DescriptorPoolSize* ptr = poolSizes)
        {
            poolInfo.PPoolSizes = ptr;
            if (_vk.CreateDescriptorPool(Device, ref poolInfo, null, out _descriptorPool) != Result.Success)
            {
                CubismLog.Error("failed to create descriptor pool!");
            }
        }

        // 创建 descriptor set 布局
        var bindings = new DescriptorSetLayoutBinding[]
        {
            new()
            {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType =  DescriptorType.UniformBuffer,
                PImmutableSamplers = null,
                StageFlags = ShaderStageFlags.All
            },
            new()
            {
                Binding = 1,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImmutableSamplers = null,
                StageFlags = ShaderStageFlags.FragmentBit
            },
            new()
            {
                Binding = 2,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImmutableSamplers = null,
                StageFlags = ShaderStageFlags.FragmentBit
            },
            new()
            {
                // 5.3 以后的 blend shader 用(s_blendTexture)
                Binding = 3,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImmutableSamplers = null,
                StageFlags = ShaderStageFlags.FragmentBit
            }
        };

        var layoutInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = (uint)bindings.Length
        };
        fixed (DescriptorSetLayoutBinding* ptr = bindings)
        {
            layoutInfo.PBindings = ptr;
            if (_vk.CreateDescriptorSetLayout(Device, ref layoutInfo, null, out _descriptorSetLayout) != Result.Success)
            {
                CubismLog.Error("failed to create descriptor set layout!");
            }
        }

        _descriptorSets.Clear();

        for (int drawAssign = 0; drawAssign < drawableCount; drawAssign++)
        {
            var desc = new Descriptor()
            {
                UniformBuffer = new CubismBufferVulkan(_vk)
            };

            if (CreateUniformBuffer(desc.UniformBuffer) is false)
            {
                throw new InvalidOperationException("failed to create uniform buffer for drawable descriptor set.");
            }

            _descriptorSets.Add(desc);
        }

        // 离屏合成用(每个 offscreen 各有 masked/unmasked 两套)
        _offscreenDescriptors.Clear();
        for (int offscreenIndex = 0; offscreenIndex < offscreenCount; ++offscreenIndex)
        {
            var desc = new Descriptor
            {
                UniformBuffer = new CubismBufferVulkan(_vk)
            };
            if (CreateUniformBuffer(desc.UniformBuffer) is false)
            {
                throw new InvalidOperationException("failed to create uniform buffer for offscreen descriptor set.");
            }

            _offscreenDescriptors.Add(desc);
        }

        // Copy 合成用(模型 RT -> swapchain)
        _copyDescriptor = new Descriptor
        {
            UniformBuffer = new CubismBufferVulkan(_vk)
        };
        if (CreateUniformBuffer(_copyDescriptor.UniformBuffer) is false)
        {
            throw new InvalidOperationException("failed to create uniform buffer for copy descriptor set.");
        }

        var allocInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = (uint)descriptorSetCount
        };
        var layouts = new DescriptorSetLayout[descriptorSetCount];
        for (int i = 0; i < descriptorSetCount; i++)
        {
            layouts[i] = _descriptorSetLayout;
        }
        fixed (DescriptorSetLayout* ptr = layouts)
        {
            allocInfo.PSetLayouts = ptr;
            var descriptorSets = new DescriptorSet[descriptorSetCount];
            fixed (DescriptorSet* ptr1 = descriptorSets)
            {
                if (_vk.AllocateDescriptorSets(Device, ref allocInfo, ptr1) != Result.Success)
                {
                    CubismLog.Error("failed to allocate descriptor sets!");
                }
            }

            int setIndex = 0;
            for (int drawAssign = 0; drawAssign < drawableCount; drawAssign++)
            {
                _descriptorSets[drawAssign].DescriptorSet = descriptorSets[setIndex++];
                _descriptorSets[drawAssign].DescriptorSetMasked = descriptorSets[setIndex++];
            }

            for (int offscreenIndex = 0; offscreenIndex < offscreenCount; ++offscreenIndex)
            {
                _offscreenDescriptors[offscreenIndex].DescriptorSet = descriptorSets[setIndex++];
                _offscreenDescriptors[offscreenIndex].DescriptorSetMasked = descriptorSets[setIndex++];
            }

            _copyDescriptor!.DescriptorSet = descriptorSets[setIndex++];
        }
    }

    /// <summary>
    /// 创建 descriptor 用的 uniform buffer 并映射整段
    /// </summary>
    /// <param name="uniformBuffer">目标 uniform buffer</param>
    /// <returns>创建并映射成功返回 true</returns>
    private bool CreateUniformBuffer(CubismBufferVulkan uniformBuffer)
    {
        if (uniformBuffer.CreateBuffer(Device, PhysicalDevice, (ulong)Unsafe.SizeOf<ModelUBO>(),
                BufferUsageFlags.UniformBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit) is false)
        {
            return false;
        }

        return uniformBuffer.Map(Device, ulong.MaxValue);
    }

    /// <summary>
    /// 创建模型 RT / 离屏合成用的全屏 quad buffer.
    /// 与官方 modelRenderTargetVertexArray / modelRenderTargetIndexArray 相同的顶点列.
    /// </summary>
    private unsafe void CreateQuadBuffers()
    {
        if (_quadVertexBuffer != null)
        {
            return;
        }

        ModelVertex[] vertices =
        [
            new() { Pos = new(-1.0f, -1.0f), TexCoord = new(0.0f, 0.0f) },
            new() { Pos = new(1.0f, -1.0f), TexCoord = new(1.0f, 0.0f) },
            new() { Pos = new(1.0f, 1.0f), TexCoord = new(1.0f, 1.0f) },
            new() { Pos = new(-1.0f, 1.0f), TexCoord = new(0.0f, 1.0f) },
        ];
        ushort[] indices = [0, 1, 2, 2, 3, 0];

        // 降级策略: quad buffer 任一步失败时放弃合成路径(调用方通过 null 检查跳过).
        ulong vertexSize = (ulong)(Unsafe.SizeOf<ModelVertex>() * vertices.Length);
        _quadStagingBuffer = new(_vk);
        bool quadStagingOk = _quadStagingBuffer.CreateBuffer(Device, PhysicalDevice, vertexSize,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        if (quadStagingOk)
        {
            quadStagingOk = _quadStagingBuffer.Map(Device, vertexSize);
        }

        if (quadStagingOk)
        {
            fixed (ModelVertex* v = vertices)
            {
                quadStagingOk = _quadStagingBuffer.MemCpy(v, vertexSize);
            }
        }

        _quadStagingBuffer.UnMap(Device);

        _quadVertexBuffer = new(_vk);
        bool quadVertexOk = quadStagingOk && _quadVertexBuffer.CreateBuffer(Device, PhysicalDevice, vertexSize,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit);

        ulong indexSize = (ulong)(sizeof(ushort) * indices.Length);
        var indexStaging = new CubismBufferVulkan(_vk);
        bool quadIndexStagingOk = indexStaging.CreateBuffer(Device, PhysicalDevice, indexSize,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        if (quadIndexStagingOk)
        {
            quadIndexStagingOk = indexStaging.Map(Device, indexSize);
        }

        if (quadIndexStagingOk)
        {
            fixed (ushort* i = indices)
            {
                quadIndexStagingOk = indexStaging.MemCpy(i, indexSize);
            }
        }

        indexStaging.UnMap(Device);

        _quadIndexBuffer = new(_vk);
        bool quadIndexOk = quadIndexStagingOk && _quadIndexBuffer.CreateBuffer(Device, PhysicalDevice, indexSize,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit);

        if (quadVertexOk is false || quadIndexOk is false)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to create fullscreen quad buffers, offscreen composition disabled.");
            indexStaging.Destroy(Device);
            _quadStagingBuffer.Destroy(Device);
            _quadVertexBuffer.Destroy(Device);
            _quadIndexBuffer.Destroy(Device);
            _quadStagingBuffer = null;
            _quadVertexBuffer = null;
            _quadIndexBuffer = null;
            return;
        }

        var commandBuffer = BeginSingleTimeCommands();
        var vertexCopy = new BufferCopy { Size = vertexSize };
        _vk.CmdCopyBuffer(commandBuffer, _quadStagingBuffer.Buffer, _quadVertexBuffer.Buffer, 1, ref vertexCopy);
        var indexCopy = new BufferCopy { Size = indexSize };
        _vk.CmdCopyBuffer(commandBuffer, indexStaging.Buffer, _quadIndexBuffer.Buffer, 1, ref indexCopy);
        SubmitCommand(commandBuffer);
        indexStaging.Destroy(Device);
    }

    /// <summary>
    /// 创建深度 buffer.
    /// </summary>
    public void CreateDepthBuffer()
    {
        // 深度附件是图形 pipeline 的必需项, 创建失败无法保证绘制正确性, 直接抛明确异常.
        if (_depthImage.CreateImage(Device, PhysicalDevice, SwapchainExtent.Width, SwapchainExtent.Height,
        1, DepthFormat, ImageTiling.Optimal,
         ImageUsageFlags.DepthStencilAttachmentBit) is false)
        {
            throw new InvalidOperationException(
                $"failed to create depth image ({SwapchainExtent.Width}x{SwapchainExtent.Height}).");
        }

        if (_depthImage.CreateView(Device, DepthFormat,
             ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit, 1) is false)
        {
            throw new InvalidOperationException(
                $"failed to create depth image view ({SwapchainExtent.Width}x{SwapchainExtent.Height}).");
        }
    }

    /// <summary>
    /// 初始化渲染器.
    /// </summary>
    public unsafe void InitializeRenderer()
    {
        // 获取每个设备的 pipeline 与离屏管理器(官方 InitializeRenderer: _deviceInfo = GetDeviceInfo(s_device))
        _deviceInfo = CubismDeviceInfo_Vulkan.GetDeviceInfo(Device);

        vkCmdSetCullModeEXT = Marshal.GetDelegateForFunctionPointer<FvkCmdSetCullMode>(_vk.GetDeviceProcAddr(Device, "vkCmdSetCullModeEXT"));

        var semaphoreInfo = new SemaphoreCreateInfo
        {
            SType = StructureType.SemaphoreCreateInfo
        };
        _vk.CreateSemaphore(Device, ref semaphoreInfo, null, out _updateFinishedSemaphore);

        CreateCommandBuffer();
        CreateVertexBuffer();
        CreateIndexBuffer();
        CreateDescriptorSets();
        CreateDepthBuffer();
        CreateQuadBuffers();

        _deviceInfo.Pipeline.CreatePipelines(_descriptorSetLayout);
    }

    /// <summary>
    /// 更新顶点 buffer.
    /// </summary>
    /// <param name="drawAssign">绘制索引</param>
    /// <param name="vcount">顶点数</param>
    /// <param name="varray">顶点数组</param>
    /// <param name="uvarray">uv 数组</param>
    /// <param name="commandBuffer">命令缓冲区</param>
    public unsafe void CopyToBuffer(int drawAssign, int vcount, float* varray, float* uvarray,
                      CommandBuffer commandBuffer)
    {
        var vertices = new ModelVertex[vcount];

        for (int ct = 0, ct1 = 0; ct < vcount; ct++, ct1 += 2)
        {
            // 从模型数据复制
            vertices[ct] = new()
            {
                Pos = new()
                {
                    X = varray[ct1 + 0],
                    Y = varray[ct1 + 1]
                },
                TexCoord = new()
                {
                    X = uvarray[ct1 + 0],
                    Y = uvarray[ct1 + 1]
                }
            };
        }
        var bufferSize = Unsafe.SizeOf<ModelVertex>() * vertices.Length;

        // 分配失败时该 drawable 的 buffer 可能无效, 此时跳过拷贝(绑定与绘制同样会跳过).
        if ((uint)drawAssign >= (uint)_stagingBuffers.Count
            || (uint)drawAssign >= (uint)_vertexBuffers.Length
            || _stagingBuffers[drawAssign].IsValid is false
            || _vertexBuffers[drawAssign].IsValid is false)
        {
            return;
        }

        fixed (void* ptr = vertices)
        {
            if (_stagingBuffers[drawAssign].MemCpy(ptr, (ulong)bufferSize) is false)
            {
                return;
            }
        }

        var copyRegion = new BufferCopy
        {
            Size = (ulong)bufferSize
        };

        _vk.CmdCopyBuffer(commandBuffer, _stagingBuffers[drawAssign].Buffer, _vertexBuffers[drawAssign].Buffer, 1,
                        ref copyRegion);
    }

    /// <summary>
    /// 更新 descriptor set
    /// </summary>
    /// <param name="descriptor">一个 shader 使用的 descriptor set 与 UBO</param>
    /// <param name="textureIndex">纹理索引</param>
    /// <param name="isMasked"></param>
    public unsafe void UpdateDescriptorSet(Descriptor descriptor, int textureIndex, bool isMasked)
    {
        var uniformBuffer = descriptor.UniformBuffer.Buffer;
        // 仅在 descriptor 尚未更新的第一次执行
        DescriptorSet descriptorSet;
        if (isMasked && !descriptor.IsDescriptorSetMaskedUpdated)
        {
            descriptorSet = descriptor.DescriptorSetMasked;
            descriptor.IsDescriptorSetMaskedUpdated = true;
        }
        else if (!descriptor.IsDescriptorSetUpdated)
        {
            descriptorSet = descriptor.DescriptorSet;
            descriptor.IsDescriptorSetUpdated = true;
        }
        else
        {
            return;
        }

        var descriptorWrites = new WriteDescriptorSet[4];

        var uniformBufferInfo = new DescriptorBufferInfo
        {
            Buffer = uniformBuffer,
            Offset = 0,
            Range = ulong.MaxValue
        };
        var ubo = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            PBufferInfo = &uniformBufferInfo
        };
        descriptorWrites[0] = ubo;

        // 纹理 1 为角色的纹理, 纹理 2 为遮罩用,
        // 纹理 3 为 5.3 以后的 blend shader 用(s_blendTexture).
        var imageInfo1 = new DescriptorImageInfo
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = _textures[textureIndex].View,
            Sampler = _textures[textureIndex].Sampler
        };
        var image1 = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            PImageInfo = &imageInfo1
        };
        descriptorWrites[1] = image1;

        // blending 用纹理的初始值(模型纹理). 实际 blend 时
        // 由 UpdateBlendTexture() 替换为绘制目标的纹理.
        var imageInfo3 = new DescriptorImageInfo
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = _textures[textureIndex].View,
            Sampler = _textures[textureIndex].Sampler
        };
        var image3 = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 3,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            PImageInfo = &imageInfo3
        };
        descriptorWrites[2] = image3;

        uint writeCount = 3;
        if (isMasked)
        {
            var imageInfo2 = new DescriptorImageInfo
            {
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = _offscreenFrameBuffers![ClippingContextBufferForDraw!.
                BufferIndex].GetTextureView(),
                Sampler = _offscreenFrameBuffers![ClippingContextBufferForDraw.
                BufferIndex].GetTextureSampler()
            };
            var image2 = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = descriptorSet,
                DstBinding = 2,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                PImageInfo = &imageInfo2
            };
            descriptorWrites[3] = image2;
            writeCount = 4;
        }

        fixed (WriteDescriptorSet* ptr = descriptorWrites)
        {
            _vk.UpdateDescriptorSets(Device, writeCount, ptr, 0, null);
        }
    }

    /// <summary>
    /// 更新 blend shader 用的纹理(binding 3).
    /// 通常传入绘制目标渲染目标的纹理. 未使用时以模型纹理填充.
    /// </summary>
    /// <param name="descriptor">目标 descriptor</param>
    /// <param name="isMasked">是否为遮罩用 set</param>
    /// <param name="imageView">blend 用 texture view</param>
    /// <param name="sampler">sampler</param>
    public unsafe void UpdateBlendTexture(Descriptor descriptor, bool isMasked, ImageView imageView, Sampler sampler)
    {
        DescriptorSet descriptorSet = isMasked ? descriptor.DescriptorSetMasked : descriptor.DescriptorSet;

        var imageInfo = new DescriptorImageInfo
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = imageView,
            Sampler = sampler
        };
        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 3,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            PImageInfo = &imageInfo
        };

        _vk.UpdateDescriptorSets(Device, 1, &write, 0, null);
    }

    /// <summary>
    /// 执行 mesh 绘制
    /// </summary>
    /// <param name="model">绘制目标的模型</param>
    /// <param name="index">绘制对象的索引</param>
    /// <param name="cmdBuffer">framebuffer 相关的命令缓冲区</param>
    public unsafe void ExecuteDrawForDraw(CubismModel model, int index, CommandBuffer cmdBuffer)
    {
        // 获取用于设置 pipeline layout 的索引
        int blendIndex;
        int shaderIndex;
        bool masked = ClippingContextBufferForDraw != null; // 该绘制对象是否为遮罩对象
        bool invertedMask = model.GetDrawableInvertedMask(index);
        int offset = (masked ? (invertedMask ? 2 : 1) : 0) + (IsPremultipliedAlpha ? 3 : 0);

        // 5-r.5: 从 GetDrawableBlendModeType()(CsmBlendMode) 选择 shader.
        // 5.2 以前的 AddCompatible / MultiplyCompatible 分配给 Add / Mult(共用 Normal shader),
        // 5.3 以后从 GetShaderNamesBegin() 返回的 blend shader 组(494 项)中选择.
        CsmBlendMode blendMode = model.GetDrawableBlendModeType(index);
        shaderIndex = ShaderNames.GetShaderNamesBegin(blendMode) + offset;
        blendIndex = blendMode.GetColorBlendType() switch
        {
            CsmEnum.CsmColorBlendType_AddCompatible => Blend.Add,
            CsmEnum.CsmColorBlendType_MultiplyCompatible => Blend.Mult,
            _ => Blend.Normal,
        };

        var descriptor = _descriptorSets[index];
        var ubo = new ModelUBO();
        if (masked)
        {
            // 设置裁剪用矩阵
            UpdateMatrix(ubo.ClipMatrix, ClippingContextBufferForDraw!.MatrixForDraw); // 用于纹理坐标转换, 因此不反转 y 轴方向

            // 设置颜色通道
            SetColorChannel(&ubo, ClippingContextBufferForDraw);
        }

        // 设置 MVP 矩阵
        UpdateMatrix(ubo.ProjectionMatrix, GetMvpMatrix());

        // 设置颜色常量缓冲区
        var baseColor = GetModelColorWithOpacity(model.GetDrawableOpacity(index));
        var multiplyColor = model.GetOverrideMultiplyAndScreenColor().GetDrawableMultiplyColor(index);
        var screenColor = model.GetOverrideMultiplyAndScreenColor().GetDrawableScreenColor(index);
        SetColorUniformBuffer(&ubo, baseColor, multiplyColor, screenColor);

        // 把 uniform buffer 复制到 descriptor
        descriptor.UniformBuffer.MemCpy(&ubo, (ulong)Unsafe.SizeOf<ModelUBO>());

        // 设置顶点 buffer(创建失败的 drawable 直接跳过)
        if (BindVertexAndIndexBuffers(index, cmdBuffer) is false)
        {
            return;
        }

        // 获取纹理索引
        int textureIndex = model.GetDrawableTextureIndex(index);

        // 绑定 descriptor set
        UpdateDescriptorSet(descriptor, textureIndex, masked);

        // blend shader 用纹理(binding3): 相当于 OpenGL texture2.
        // 无法直接采样绘制中的 RT, 因此 5.3 以后的 blend 复制快照后再使用.
        bool advancedBlend = shaderIndex >= ShaderNames.NormalAtop && Model.IsBlendModeEnabled();
        if (advancedBlend)
        {
            PrepareBlendSnapshot();
        }

        if (advancedBlend && _blendSnapshot is { } snapshot && snapshot.IsValid())
        {
            UpdateBlendTexture(descriptor, masked, snapshot.GetTextureView(), snapshot.GetTextureSampler());
        }
        else
        {
            UpdateBlendTexture(descriptor, masked,
                _textures[textureIndex].View, _textures[textureIndex].Sampler);
        }
        var descriptorSet = masked ? _descriptorSets[index].DescriptorSetMasked
                                                 : _descriptorSets[index].DescriptorSet;
        _vk.CmdBindDescriptorSets(cmdBuffer, PipelineBindPoint.Graphics,
                                    _deviceInfo.Pipeline.GetPipelineLayout(shaderIndex, blendIndex), 0, 1,
                                    ref descriptorSet, 0, null);

        // 绑定 pipeline
        _vk.CmdBindPipeline(cmdBuffer, PipelineBindPoint.Graphics,
                          _deviceInfo.Pipeline.GetPipeline(shaderIndex, blendIndex));

        // 5.3 以后的 blend 已由 PrepareBlendSnapshot() 重新开启 RT
        // 绘制
        _vk.CmdDrawIndexed(cmdBuffer, (uint)model.GetDrawableVertexIndexCount(index), 1, 0, 0, 0);
    }

    /// <summary>
    /// 执行遮罩绘制
    /// </summary>
    /// <param name="model">绘制目标的模型</param>
    /// <param name="index">绘制对象的索引</param>
    /// <param name="cmdBuffer">framebuffer 相关的命令缓冲区</param>
    public unsafe void ExecuteDrawForMask(CubismModel model, int index, CommandBuffer cmdBuffer)
    {
        int shaderIndex = ShaderNames.SetupMask;
        int blendIndex = Blend.Mask;

        var descriptor = _descriptorSets[index];
        var ubo = new ModelUBO();

        // 设置裁剪用矩阵
        UpdateMatrix(ubo.ClipMatrix, ClippingContextBufferForMask!.MatrixForMask);

        // 设置颜色通道
        SetColorChannel(&ubo, ClippingContextBufferForMask);

        // 设置颜色常量缓冲区
        var rect = ClippingContextBufferForMask.LayoutBounds;
        var baseColor = new CubismTextureColor(rect.X * 2.0f - 1.0f, rect.Y * 2.0f - 1.0f, rect.GetRight() * 2.0f - 1.0f, rect.GetBottom() * 2.0f - 1.0f);
        var multiplyColor = model.GetOverrideMultiplyAndScreenColor().GetDrawableMultiplyColor(index);
        var screenColor = model.GetOverrideMultiplyAndScreenColor().GetDrawableScreenColor(index);
        SetColorUniformBuffer(&ubo, baseColor, multiplyColor, screenColor);

        // 把 uniform buffer 复制到 descriptor
        descriptor.UniformBuffer.MemCpy(&ubo, (ulong)Unsafe.SizeOf<ModelUBO>());

        // 设置顶点 buffer(创建失败的 drawable 直接跳过)
        if (BindVertexAndIndexBuffers(index, cmdBuffer) is false)
        {
            return;
        }

        // 获取纹理索引
        int textureIndex = model.GetDrawableTextureIndex(index);

        // 绑定 descriptor set
        UpdateDescriptorSet(descriptor, textureIndex, false);
        _vk.CmdBindDescriptorSets(cmdBuffer, PipelineBindPoint.Graphics,
                                _deviceInfo.Pipeline.GetPipelineLayout(shaderIndex, blendIndex), 0, 1,
                                ref _descriptorSets[index].DescriptorSet, 0, null);

        // 绑定 pipeline
        _vk.CmdBindPipeline(cmdBuffer, PipelineBindPoint.Graphics,
                          _deviceInfo.Pipeline.GetPipeline(shaderIndex, blendIndex));

        // 绘制
        _vk.CmdDrawIndexed(cmdBuffer, (uint)model.GetDrawableVertexIndexCount(index), 1, 0, 0, 0);
    }

    /// <summary>
    /// [override]
    /// <para></para>
    /// 绘制绘制对象(art mesh).
    /// </summary>
    /// <param name="model">绘制目标的模型</param>
    /// <param name="index">绘制 mesh 的索引</param>
    /// <param name="commandBuffer">命令缓冲区</param>
    /// <param name="updateCommandBuffer">更新用命令缓冲区</param>
    public unsafe void DrawMeshVulkan(CubismModel model, int index,
                        CommandBuffer commandBuffer, CommandBuffer updateCommandBuffer)
    {
        if (Device.Handle == 0)
        {
            // 未设置设备
            return;
        }
        if (model.GetDrawableVertexIndexCount(index) == 0)
        {
            // 无绘制对象
            return;
        }
        if (model.GetDrawableOpacity(index) <= 0.0f && ClippingContextBufferForMask == null)
        {
            // 无需绘制时跳过绘制处理
            return;
        }

        int textureIndex = model.GetDrawableTextureIndex(index);
        if (_textures[textureIndex].Sampler.Handle == 0 || _textures[textureIndex].View.Handle == 0)
        {
            return;
        }

        // 背面绘制的启用与禁用
        if (IsCulling)
        {
            vkCmdSetCullModeEXT(commandBuffer, CullModeFlags.BackBit);
        }
        else
        {
            vkCmdSetCullModeEXT(commandBuffer, CullModeFlags.None);
        }

        // 复制到顶点 buffer
        CopyToBuffer(index, model.GetDrawableVertexCount(index),
                     model.GetDrawableVertices(index),
                     (float*)model.GetDrawableVertexUvs(index),
                     updateCommandBuffer);

        if (ClippingContextBufferForMask != null) // 生成遮罩时
        {
            ExecuteDrawForMask(model, index, commandBuffer);
        }
        else
        {
            ExecuteDrawForDraw(model, index, commandBuffer);
        }

        ClippingContextBufferForDraw = null;
        ClippingContextBufferForMask = null;
    }

    /// <summary>
    /// 开始渲染
    /// </summary>
    /// <param name="drawCommandBuffer">命令缓冲区</param>
    /// <param name="isResume">是否为渲染恢复的标志</param>
    public unsafe void BeginRendering(CommandBuffer drawCommandBuffer, bool isResume)
    {
        Image colorImage = UseRenderTarget ? RenderTargetImage : SwapchainImage;
        ImageLayout currentLayout = _imageLayouts.TryGetValue(colorImage.Handle, out ImageLayout tracked)
            ? tracked
            : ImageLayout.Undefined;

        var colorBarrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
            OldLayout = currentLayout,
            NewLayout = ImageLayout.ColorAttachmentOptimal,
            Image = colorImage,
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _vk.CmdPipelineBarrier(drawCommandBuffer, PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.ColorAttachmentOutputBit, 0, 0, null, 0, null, 1, ref colorBarrier);
        _imageLayouts[colorImage.Handle] = ImageLayout.ColorAttachmentOptimal;

        ImageLayout depthLayout = _imageLayouts.TryGetValue(_depthImage.Image.Handle, out ImageLayout trackedDepth)
            ? trackedDepth
            : ImageLayout.Undefined;
        var depthBarrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.DepthStencilAttachmentWriteBit,
            OldLayout = depthLayout,
            NewLayout = ImageLayout.DepthStencilAttachmentOptimal,
            Image = _depthImage.Image,
            SubresourceRange = new(ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit, 0, 1, 0, 1)
        };
        _vk.CmdPipelineBarrier(drawCommandBuffer, PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.EarlyFragmentTestsBit, 0, 0, null, 0, null, 1, ref depthBarrier);
        _imageLayouts[_depthImage.Image.Handle] = ImageLayout.DepthStencilAttachmentOptimal;

        var bgColor = _vulkanManager.LApp.BGColor;
        var clearValue = new ClearValue[2];
        clearValue[0].Color = new(bgColor.R, bgColor.G, bgColor.B, bgColor.A);
        clearValue[1].DepthStencil = new(1.0f, 0);

        var colorAttachment = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfoKhr
        };
        if (UseRenderTarget)
        {
            colorAttachment.ImageView = RenderTargetImageView;
            if (isResume)
            {
                colorAttachment.LoadOp = AttachmentLoadOp.Load;
            }
            else
            {
                colorAttachment.LoadOp = AttachmentLoadOp.Clear;
            }
        }
        else
        {
            colorAttachment.ImageView = SwapchainImageView;
            // 非模型 RT 通道中此 BeginRendering 位于帧首, 因此清除背景
            colorAttachment.LoadOp = isResume ? AttachmentLoadOp.Load : AttachmentLoadOp.Clear;
        }
        colorAttachment.ImageLayout = ImageLayout.AttachmentOptimal;
        colorAttachment.StoreOp = AttachmentStoreOp.Store;
        colorAttachment.ClearValue = clearValue[0];

        var depthStencilAttachment = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfoKhr,
            ImageView = _depthImage.View,
            ImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            ClearValue = new(new(1.0f, 0))
        };

        var renderingInfo = new RenderingInfo
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new()
            {
                Offset = new(0, 0),
                Extent = SwapchainExtent
            },
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachment,
            PDepthAttachment = &depthStencilAttachment
        };

        _vk.CmdBeginRendering(drawCommandBuffer, &renderingInfo);
    }

    /// <summary>
    /// 结束渲染
    /// </summary>
    /// <param name="drawCommandBuffer">命令缓冲区</param>
    public unsafe void EndRendering(CommandBuffer drawCommandBuffer)
    {
        _vk.CmdEndRendering(drawCommandBuffer);

        // 变更布局
        Image colorImage = UseRenderTarget ? RenderTargetImage : SwapchainImage;
        ImageLayout newLayout = UseRenderTarget ? ImageLayout.ShaderReadOnlyOptimal : ImageLayout.PresentSrcKhr;
        var memoryBarrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstAccessMask = UseRenderTarget ? AccessFlags.ShaderReadBit : AccessFlags.None,
            OldLayout = ImageLayout.ColorAttachmentOptimal,
            NewLayout = newLayout,
            Image = colorImage,
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };

        _vk.CmdPipelineBarrier(drawCommandBuffer, PipelineStageFlags.ColorAttachmentOutputBit,
            UseRenderTarget ? PipelineStageFlags.FragmentShaderBit : PipelineStageFlags.BottomOfPipeBit,
            0, 0, null, 0, null, 1, ref memoryBarrier);
        _imageLayouts[colorImage.Handle] = newLayout;
    }

    public void BindTexture(CubismImageVulkan image)
    {
        _textures.Add(image);
    }

    /// <summary>
    /// 设置裁剪遮罩缓冲区的大小
    /// </summary>
    /// <param name="width">裁剪遮罩缓冲区的宽度</param>
    /// <param name="height">裁剪遮罩缓冲区的高度</param>
    public void SetClippingMaskBufferSize(float width, float height)
    {
        // 在销毁实例前保存渲染纹理的数量
        var renderTextureCount = _clippingManager.RenderTextureCount;

        // 为变更 FrameBuffer 尺寸而销毁并重建实例
        _clippingManager.Dispose();

        _clippingManager = new CubismClippingManager_Vulkan(_vk);

        _clippingManager.SetClippingMaskBufferSize(width, height);

        _clippingManager.Initialize(Model, renderTextureCount);
    }

    /// <summary>
    /// 获取裁剪遮罩缓冲区的大小
    /// </summary>
    /// <returns>裁剪遮罩缓冲区的大小</returns>
    public Vector2 GetClippingMaskBufferSize()
    {
        return _clippingManager.ClippingMaskBufferSize;
    }

    public CubismRenderTarget_Vulkan GetMaskBuffer(int index)
    {
        if (_offscreenFrameBuffers == null)
        {
            CubismLog.Error("offscreenFrameBuffers is null.");
        }

        return _offscreenFrameBuffers![index];
    }

    /// <summary>
    /// 按裁剪管理器的 Type 返回遮罩缓冲区(drawable / offscreen 两套).
    /// </summary>
    public CubismRenderTarget_Vulkan GetMaskBufferForType(CsmEnum.DrawableObjectType type, int index)
    {
        if (type == CsmEnum.DrawableObjectType.Offscreen)
        {
            if ((uint)index >= (uint)_offscreenMaskBuffers.Count)
            {
                CubismLog.Error("[Vulkan] offscreen mask buffer index out of range: {0}", index);
            }

            return _offscreenMaskBuffers[index];
        }

        return GetMaskBuffer(index);
    }

    /// <summary>
    /// 绑定 drawable 的顶点 / 索引 buffer
    /// </summary>
    /// <param name="index">绘制对象索引</param>
    /// <param name="cmdBuffer">命令缓冲区</param>
    /// <returns>buffer 可用并已绑定返回 true, 否则返回 false(调用方应跳过绘制)</returns>
    private bool BindVertexAndIndexBuffers(int index, CommandBuffer cmdBuffer)
    {
        if ((uint)index >= (uint)_vertexBuffers.Length || (uint)index >= (uint)_indexBuffers.Length)
        {
            CubismLog.Warning("[Live2D Vulkan]vertex/index buffer index out of range: {0}.", index);
            return false;
        }

        // 分配失败时 buffer 可能是无效对象, 此时不得绑定, 跳过该绘制对象.
        if (_vertexBuffers[index].IsValid is false || _indexBuffers[index].IsValid is false)
        {
            return false;
        }

        var vertexBuffers = new Buffer[] { _vertexBuffers[index].Buffer };
        var offsets = new ulong[] { 0 };
        _vk.CmdBindVertexBuffers(cmdBuffer, 0, 1, vertexBuffers, offsets);
        _vk.CmdBindIndexBuffer(cmdBuffer, _indexBuffers[index].Buffer, 0, IndexType.Uint16);
        return true;
    }

    public unsafe override void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        // 若已创建离屏则释放
        // 不使用 mask 的模型中 _offscreenFrameBuffers 为 null,
        // 旧实现会在此处产生 NullReferenceException(本任务 §2.4 已修正).
        if (_offscreenFrameBuffers != null)
        {
            for (int i = 0; i < _offscreenFrameBuffers.Length; i++)
            {
                _offscreenFrameBuffers[i].DestroyRenderTarget();
            }

            _offscreenFrameBuffers = null;
        }

        _depthImage.Destroy(Device);
        _vk.DestroySemaphore(Device, _updateFinishedSemaphore, null);
        _vk.DestroyDescriptorPool(Device, _descriptorPool, null);
        _vk.DestroyDescriptorSetLayout(Device, _descriptorSetLayout, null);

        for (int i = 0; i < _vertexBuffers.Length; i++)
        {
            _vertexBuffers[i].Destroy(Device);
        }

        for (int i = 0; i < _stagingBuffers.Count; i++)
        {
            _stagingBuffers[i].Destroy(Device);
        }

        for (int i = 0; i < _indexBuffers.Length; i++)
        {
            _indexBuffers[i].Destroy(Device);
        }

        for (int i = 0; i < _descriptorSets.Count; i++)
        {
            _descriptorSets[i].UniformBuffer.Destroy(Device);
        }

        _clippingManager?.Dispose();
        _offscreenClippingManager?.Dispose();

        // 释放模型渲染目标 / 离屏用遮罩 / 合成用缓冲.
        for (int i = 0; i < _modelRenderTargets.Count; i++)
        {
            _modelRenderTargets[i].DestroyRenderTarget();
        }
        _modelRenderTargets.Clear();

        _blendSnapshot?.DestroyRenderTarget();
        _blendSnapshot = null;

        for (int i = 0; i < _offscreenMaskBuffers.Count; i++)
        {
            _offscreenMaskBuffers[i].DestroyRenderTarget();
        }
        _offscreenMaskBuffers.Clear();

        for (int i = 0; i < _offscreenDescriptors.Count; i++)
        {
            _offscreenDescriptors[i].UniformBuffer.Destroy(Device);
        }
        _offscreenDescriptors.Clear();

        _copyDescriptor?.UniformBuffer.Destroy(Device);
        _copyDescriptor = null;

        _quadStagingBuffer?.Destroy(Device);
        _quadVertexBuffer?.Destroy(Device);
        _quadIndexBuffer?.Destroy(Device);
        _quadStagingBuffer = null;
        _quadVertexBuffer = null;
        _quadIndexBuffer = null;

        // 纹理由 TextureInfoVulkan / LAppTextureManager 持有,
        // 在此处 Destroy 会导致共享缓存的双重释放. 只解除引用
        // (官方 CubismRenderer_Vulkan 的析构函数同样不销毁 _textures).
        _textures.Clear();

        // 释放设备信息的引用. 最后一个渲染器消失时
        // pipeline 与离屏管理器也会被释放.
        if (_deviceInfo != null)
        {
            CubismDeviceInfo_Vulkan.ReleaseDeviceInfo(Device);
            _deviceInfo = null!;
        }
    }

    /// <summary>
    /// 绘制模型前的离屏设置(对应官方 BeforeDrawModelRenderTarget).
    /// 使用 blend / offscreen 时以模型 RT 为绘制目标, 最后用 Copy 合成到 swapchain.
    /// </summary>
    protected override void BeforeDrawModelRenderTarget()
    {
        if (!_useModelRenderTarget)
        {
            return;
        }

        if (_modelRenderTargets.Count == 0)
        {
            _modelRenderTargets.Add(new CubismRenderTarget_Vulkan(_vk));
        }

        var modelRenderTarget = _modelRenderTargets[0];
        if (!modelRenderTarget.IsValid() ||
            modelRenderTarget.GetBufferWidth() != SwapchainExtent.Width ||
            modelRenderTarget.GetBufferHeight() != SwapchainExtent.Height)
        {
            modelRenderTarget.DestroyRenderTarget();
            if (modelRenderTarget.CreateRenderTarget(Device, PhysicalDevice,
                SwapchainExtent.Width, SwapchainExtent.Height, SwapchainImageFormat, DepthFormat) is false)
            {
                // 降级: 模型 RT 不可用时退回直接绘制到 swapchain, 避免使用无效附件.
                CubismLog.Warning("[Live2D Vulkan]model render target unavailable, fallback to direct swapchain rendering.");
                _useModelRenderTarget = false;
                _currentRenderTarget = null;
                _currentOffscreen = null;
                _isDrawingOffscreen = false;
                BeginRendering(_drawCommandBuffer, false);
                return;
            }

            _isClearedModelRenderTarget = false;
        }

        // 由于 vkCmdCopyImage 合成无法做 alpha blend, 因此用背景色清除模型 RT 本身
        var bgColor = _vulkanManager.LApp.BGColor;
        modelRenderTarget.BeginDraw(_drawCommandBuffer, bgColor.R, bgColor.G, bgColor.B, bgColor.A,
            !_isClearedModelRenderTarget);
        _currentRenderTarget = modelRenderTarget;
        _currentOffscreen = null;
        _isDrawingOffscreen = false;
        _isClearedModelRenderTarget = true;
    }

    /// <summary>
    /// 绘制模型后的离屏设置(对应官方 AfterDrawModelRenderTarget).
    /// 结束模型 RT, 并用 Copy shader 合成到 swapchain.
    /// </summary>
    protected override void AfterDrawModelRenderTarget()
    {
        if (!_useModelRenderTarget)
        {
            return;
        }

        // 为保险起见把未刷新的离屏传递到父级
        while (_currentOffscreen != null)
        {
            DrawOffscreen(_currentOffscreen);
        }

        var modelRenderTarget = _modelRenderTargets[0];
        if (modelRenderTarget.IsRendering())
        {
            modelRenderTarget.EndDraw(_drawCommandBuffer);
        }

        _currentRenderTarget = null;
        CopyRenderTarget(modelRenderTarget);
    }

    /// <summary>
    /// 结束当前绘制目标并切换到下一个渲染目标(对应官方 EnsurePreviousRenderFinished).
    /// </summary>
    private void EnsurePreviousRenderFinished(CubismRenderTarget_Vulkan nextRenderTarget)
    {
        if (_currentRenderTarget == null)
        {
            EndRendering(_drawCommandBuffer);
        }
        else if (!ReferenceEquals(_currentRenderTarget, nextRenderTarget) && _currentRenderTarget.IsRendering())
        {
            _currentRenderTarget.EndDraw(_drawCommandBuffer);
        }
    }

    /// <summary>
    /// 渲染目标绘制开始(blend/offscreen 时为模型 RT, 否则为 swapchain).
    /// </summary>
    private void BeginRenderTarget(bool isResume)
    {
        if (_useModelRenderTarget)
        {
            BeforeDrawModelRenderTarget();
        }
        else
        {
            BeginRendering(_drawCommandBuffer, isResume);
        }
    }

    /// <summary>
    /// 渲染目标绘制结束(blend/offscreen 时为 Copy 合成).
    /// </summary>
    private void EndRenderTarget()
    {
        if (_useModelRenderTarget)
        {
            AfterDrawModelRenderTarget();
        }
        else
        {
            EndRendering(_drawCommandBuffer);
        }
    }

    /// <summary>
    /// 使用 Copy shader 把渲染目标的内容合成到当前 swapchain.
    /// </summary>
    private unsafe void CopyRenderTarget(CubismRenderTarget_Vulkan src)
    {
        if (!src.IsValid())
        {
            return;
        }

        // 与 glBlitFramebuffer 等价的图像拷贝(Vulkan 中作为 texture barrier 的替代).
        // Copy shader(ExecuteDrawForRenderTarget) 保留为经由 quad 的替代实现,
        // 模型 RT -> swapchain 的合成使用带布局转换的 vkCmdCopyImage 进行.
        var srcImage = src.GetTextureImage();
        var dstImage = SwapchainImage;

        var srcToTransfer = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.ShaderReadBit,
            DstAccessMask = AccessFlags.TransferReadBit,
            OldLayout = ImageLayout.ShaderReadOnlyOptimal,
            NewLayout = ImageLayout.TransferSrcOptimal,
            Image = srcImage,
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _vk.CmdPipelineBarrier(_drawCommandBuffer, PipelineStageFlags.FragmentShaderBit,
            PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, ref srcToTransfer);

        ImageLayout dstOldLayout = _imageLayouts.TryGetValue(dstImage.Handle, out ImageLayout tracked)
            ? tracked
            : ImageLayout.Undefined;
        var dstToTransfer = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.TransferWriteBit,
            OldLayout = dstOldLayout,
            NewLayout = ImageLayout.TransferDstOptimal,
            Image = dstImage,
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _vk.CmdPipelineBarrier(_drawCommandBuffer, PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, ref dstToTransfer);

        // 用背景色清除 swapchain 后再复制模型 RT
        var bgColor = _vulkanManager.LApp.BGColor;
        var clearColor = new ClearColorValue(bgColor.R, bgColor.G, bgColor.B, bgColor.A);
        var clearRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1);
        _vk.CmdClearColorImage(_drawCommandBuffer, dstImage, ImageLayout.TransferDstOptimal,
            ref clearColor, 1, &clearRange);

        var copy = new ImageCopy
        {
            SrcSubresource = new(ImageAspectFlags.ColorBit, 0, 0, 1),
            DstSubresource = new(ImageAspectFlags.ColorBit, 0, 0, 1),
            Extent = new(src.GetBufferWidth(), src.GetBufferHeight(), 1)
        };
        _vk.CmdCopyImage(_drawCommandBuffer, srcImage, ImageLayout.TransferSrcOptimal,
            dstImage, ImageLayout.TransferDstOptimal, 1, ref copy);

        var dstToPresent = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = AccessFlags.None,
            OldLayout = ImageLayout.TransferDstOptimal,
            NewLayout = ImageLayout.PresentSrcKhr,
            Image = dstImage,
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _vk.CmdPipelineBarrier(_drawCommandBuffer, PipelineStageFlags.TransferBit,
            PipelineStageFlags.BottomOfPipeBit, 0, 0, null, 0, null, 1, ref dstToPresent);
        _imageLayouts[dstImage.Handle] = ImageLayout.PresentSrcKhr;

        // 把拷贝源 RT 恢复为 ShaderReadOnly, 与下一帧的 BeginDraw 保持一致
        var srcToRead = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.TransferReadBit,
            DstAccessMask = AccessFlags.ShaderReadBit,
            OldLayout = ImageLayout.TransferSrcOptimal,
            NewLayout = ImageLayout.ShaderReadOnlyOptimal,
            Image = srcImage,
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _vk.CmdPipelineBarrier(_drawCommandBuffer, PipelineStageFlags.TransferBit,
            PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, ref srcToRead);
        src.SetColorImageCurrentLayout(ImageLayout.ShaderReadOnlyOptimal);
    }

    /// <summary>
    /// 使用 Copy shader 把 src 的内容绘制到全屏 quad(对应官方 ExecuteDrawForRenderTarget).
    /// </summary>
    private unsafe void ExecuteDrawForRenderTarget(CubismRenderTarget_Vulkan src)
    {
        if (_copyDescriptor == null || _quadVertexBuffer == null || _quadIndexBuffer == null)
        {
            return;
        }

        var baseColor = GetModelColorWithOpacity(1.0f);
        var ubo = new ModelUBO();
        UpdateColor(ubo.BaseColor, baseColor.R * baseColor.A, baseColor.G * baseColor.A,
            baseColor.B * baseColor.A, baseColor.A);
        UpdateColor(ubo.MultiplyColor, 1.0f, 1.0f, 1.0f, 1.0f);
        UpdateColor(ubo.ScreenColor, 0.0f, 0.0f, 0.0f, 1.0f);
        _copyDescriptor.UniformBuffer.MemCpy(&ubo, (ulong)Unsafe.SizeOf<ModelUBO>());

        UpdateCopyDescriptor(src);

        var descriptorSet = _copyDescriptor.DescriptorSet;
        _vk.CmdBindDescriptorSets(_drawCommandBuffer, PipelineBindPoint.Graphics,
            _deviceInfo.Pipeline.GetPipelineLayout(ShaderNames.Copy, 0), 0, 1, ref descriptorSet, 0, null);
        _vk.CmdBindPipeline(_drawCommandBuffer, PipelineBindPoint.Graphics,
            _deviceInfo.Pipeline.GetPipeline(ShaderNames.Copy, 0));

        vkCmdSetCullModeEXT(_drawCommandBuffer, CullModeFlags.None);
        BindQuadBuffers(_drawCommandBuffer);

        var viewport = GetViewport(SwapchainExtent.Width, SwapchainExtent.Height, 0.0f, 1.0f);
        _vk.CmdSetViewport(_drawCommandBuffer, 0, 1, &viewport);
        var rect = GetScissor(0.0f, 0.0f, SwapchainExtent.Width, SwapchainExtent.Height);
        _vk.CmdSetScissor(_drawCommandBuffer, 0, 1, &rect);

        _vk.CmdDrawIndexed(_drawCommandBuffer, 6, 1, 0, 0, 0);
    }

    /// <summary>
    /// 更新 Copy 用的 descriptor(UBO + 合成源纹理).
    /// </summary>
    private unsafe void UpdateCopyDescriptor(CubismRenderTarget_Vulkan src)
    {
        if (_copyDescriptor == null)
        {
            return;
        }

        var imageInfo = new DescriptorImageInfo
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = src.GetTextureView(),
            Sampler = src.GetTextureSampler()
        };
        Span<WriteDescriptorSet> writes = stackalloc WriteDescriptorSet[2];

        var bufferInfo = new DescriptorBufferInfo
        {
            Buffer = _copyDescriptor.UniformBuffer.Buffer,
            Offset = 0,
            Range = ulong.MaxValue
        };
        writes[0] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _copyDescriptor.DescriptorSet,
            DstBinding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            PBufferInfo = &bufferInfo
        };
        writes[1] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _copyDescriptor.DescriptorSet,
            DstBinding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            PImageInfo = &imageInfo
        };

        fixed (WriteDescriptorSet* ptr = writes)
        {
            _vk.UpdateDescriptorSets(Device, 2, ptr, 0, null);
        }
    }

    /// <summary>
    /// 绑定全屏 quad 的顶点/索引 buffer.
    /// </summary>
    private unsafe void BindQuadBuffers(CommandBuffer cmdBuffer)
    {
        if (_quadVertexBuffer == null || _quadIndexBuffer == null)
        {
            return;
        }

        var vertexBuffer = _quadVertexBuffer.Buffer;
        ulong offset = 0;
        _vk.CmdBindVertexBuffers(cmdBuffer, 0, 1, &vertexBuffer, &offset);
        _vk.CmdBindIndexBuffer(cmdBuffer, _quadIndexBuffer.Buffer, 0, IndexType.Uint16);
    }

    /// <summary>
    /// 添加离屏(对应官方 AddOffscreen).
    /// </summary>
    private unsafe void AddOffscreen(int offscreenIndex)
    {
        // 越界与所有权检查: 不把 drawable 索引当作 offscreen 使用.
        if ((uint)offscreenIndex >= (uint)_offscreenList.Count)
        {
            CubismLog.Error("[Vulkan] offscreenIndex out of range: {0} (offscreenCount={1})",
                offscreenIndex, _offscreenList.Count);
            return;
        }

        CubismOffscreenRenderTarget_Vulkan offscreen = _offscreenList[offscreenIndex];

        if (_currentOffscreen != null && !ReferenceEquals(_currentOffscreen, offscreen))
        {
            bool isParent = false;
            int ownerIndex = Model.GetOffscreenOwnerIndices()[offscreenIndex];
            int parentIndex = Model.GetPartParentPartIndex(ownerIndex);
            int currentOwnerIndex = Model.GetOffscreenOwnerIndices()[_currentOffscreen.GetOffscreenIndex()];

            while (parentIndex != CubismModel.CubismNoIndex_Parent)
            {
                if (parentIndex == currentOwnerIndex)
                {
                    isParent = true;
                    break;
                }
                parentIndex = Model.GetPartParentPartIndex(parentIndex);
            }

            if (!isParent)
            {
                SubmitDrawToParentOffscreen(offscreenIndex, CsmEnum.DrawableObjectType.Offscreen);
            }
        }

        offscreen.SetOffscreenRenderTarget(Device, PhysicalDevice, _deviceInfo.OffscreenManager,
            SwapchainExtent.Width, SwapchainExtent.Height, SwapchainImageFormat, DepthFormat);

        CubismRenderTarget_Vulkan? target = offscreen.GetRenderTarget();
        if (target == null || !target.IsValid())
        {
            return;
        }

        offscreen.SetOldOffscreen(offscreen.GetParentPartOffscreen());
        EnsurePreviousRenderFinished(target);
        target.BeginDraw(_drawCommandBuffer, 0.0f, 0.0f, 0.0f, 0.0f, true);

        var viewport = GetViewport(SwapchainExtent.Width, SwapchainExtent.Height, 0.0f, 1.0f);
        _vk.CmdSetViewport(_drawCommandBuffer, 0, 1, &viewport);
        var rect = GetScissor(0.0f, 0.0f, SwapchainExtent.Width, SwapchainExtent.Height);
        _vk.CmdSetScissor(_drawCommandBuffer, 0, 1, &rect);

        _currentOffscreen = offscreen;
        _currentRenderTarget = target;
        _isDrawingOffscreen = true;
        ClippingContextBufferForOffscreen = _offscreenClippingManager?.ClippingContextListForOffscreen[offscreenIndex]
            as CubismClippingContext_Vulkan;
    }

    /// <summary>
    /// 把离屏传递到父级(对应官方 SubmitDrawToParentOffscreen).
    /// </summary>
    private unsafe void SubmitDrawToParentOffscreen(int objectIndex, CsmEnum.DrawableObjectType objectType)
    {
        if (_currentOffscreen == null || objectIndex == CubismModel.CubismNoIndex_Offscreen)
        {
            return;
        }

        int currentOwnerIndex = Model.GetOffscreenOwnerIndices()[_currentOffscreen.GetOffscreenIndex()];
        if (currentOwnerIndex == CubismModel.CubismNoIndex_Offscreen)
        {
            return;
        }

        int targetParentIndex;
        if (objectType == CsmEnum.DrawableObjectType.Drawable)
        {
            targetParentIndex = Model.GetDrawableParentPartIndex(objectIndex);
        }
        else
        {
            if ((uint)objectIndex >= (uint)_offscreenList.Count)
            {
                CubismLog.Error("[Vulkan] offscreenIndex out of range: {0}", objectIndex);
                return;
            }
            targetParentIndex = Model.GetPartParentPartIndex(Model.GetOffscreenOwnerIndices()[objectIndex]);
        }

        while (targetParentIndex != CubismModel.CubismNoIndex_Parent)
        {
            if (targetParentIndex == currentOwnerIndex)
            {
                return;
            }
            targetParentIndex = Model.GetPartParentPartIndex(targetParentIndex);
        }

        DrawOffscreen(_currentOffscreen);
        SubmitDrawToParentOffscreen(objectIndex, objectType);
    }

    /// <summary>
    /// 绘制离屏并合成到父级的渲染目标(对应官方 DrawOffscreen).
    /// </summary>
    private unsafe void DrawOffscreen(CubismOffscreenRenderTarget_Vulkan offscreen)
    {
        int offscreenIndex = offscreen.GetOffscreenIndex();
        CubismRenderTarget_Vulkan? target = offscreen.GetRenderTarget();
        if (target == null || !target.IsValid())
        {
            _currentOffscreen = null;
            _currentRenderTarget = null;
            return;
        }

        if (target.IsRendering())
        {
            target.EndDraw(_drawCommandBuffer);
        }

        CubismOffscreenRenderTarget_Vulkan? parent = offscreen.GetOldOffscreen();
        _currentOffscreen = parent;
        _isDrawingOffscreen = parent != null;

        CubismClippingContext_Vulkan? clipContext = _offscreenClippingManager != null
            ? _offscreenClippingManager.ClippingContextListForOffscreen[offscreenIndex] as CubismClippingContext_Vulkan
            : null;
        ClippingContextBufferForOffscreen = clipContext;

        // 重新开启父级的渲染目标(没有则使用模型 RT)
        if (parent?.GetRenderTarget() is { } parentTarget)
        {
            if (!parentTarget.IsRendering())
            {
                parentTarget.BeginDraw(_drawCommandBuffer, 0.0f, 0.0f, 0.0f, 0.0f, false);
            }
            _currentRenderTarget = parentTarget;
        }
        else if (_useModelRenderTarget && _modelRenderTargets.Count > 0)
        {
            var modelRenderTarget = _modelRenderTargets[0];
            if (!modelRenderTarget.IsRendering())
            {
                modelRenderTarget.BeginDraw(_drawCommandBuffer, 0.0f, 0.0f, 0.0f, 0.0f, false);
            }
            _currentRenderTarget = modelRenderTarget;
        }
        else
        {
            BeginRendering(_drawCommandBuffer, true);
            _currentRenderTarget = null;
        }

        var viewport = GetViewport(SwapchainExtent.Width, SwapchainExtent.Height, 0.0f, 1.0f);
        _vk.CmdSetViewport(_drawCommandBuffer, 0, 1, &viewport);
        var rect = GetScissor(0.0f, 0.0f, SwapchainExtent.Width, SwapchainExtent.Height);
        _vk.CmdSetScissor(_drawCommandBuffer, 0, 1, &rect);

        IsCulling = Model.GetOffscreenCulling(offscreenIndex) != 0;
        ExecuteDrawForOffscreen(offscreen, clipContext);

        offscreen.StopUsingRenderTexture(_deviceInfo.OffscreenManager);
        ClippingContextBufferForOffscreen = null;
    }

    /// <summary>
    /// 绘制离屏合成用的 quad(对应官方 ExecuteDrawForOffscreen).
    /// </summary>
    private unsafe void ExecuteDrawForOffscreen(CubismOffscreenRenderTarget_Vulkan offscreen,
        CubismClippingContext_Vulkan? clipContext)
    {
        int offscreenIndex = offscreen.GetOffscreenIndex();
        if ((uint)offscreenIndex >= (uint)_offscreenDescriptors.Count)
        {
            CubismLog.Error("[Vulkan] offscreen descriptor missing: {0}", offscreenIndex);
            return;
        }

        bool masked = clipContext is { IsUsing: true };
        bool invertedMask = Model.GetOffscreenInvertedMask(offscreenIndex);
        const int usePremultipliedAlpha = 3;
        int offset = (masked ? (invertedMask ? 2 : 1) : 0) + usePremultipliedAlpha;

        CsmBlendMode blendMode = Model.GetOffscreenBlendModeType(offscreenIndex);
        int shaderNameBegin = ShaderNames.GetShaderNamesBegin(blendMode);
        int shaderIndex;
        int blendIndex;
        bool isBlendMode;
        switch (shaderNameBegin)
        {
            case ShaderNames.Normal:
                shaderIndex = ShaderNames.Normal + offset;
                blendIndex = Blend.Normal;
                isBlendMode = false;
                break;
            case ShaderNames.Add:
                shaderIndex = ShaderNames.Add + offset;
                blendIndex = Blend.Add;
                isBlendMode = false;
                break;
            case ShaderNames.Mult:
                shaderIndex = ShaderNames.Mult + offset;
                blendIndex = Blend.Mult;
                isBlendMode = false;
                break;
            default:
                shaderIndex = shaderNameBegin + offset;
                blendIndex = 0;
                isBlendMode = true;
                break;
        }

        var descriptor = _offscreenDescriptors[offscreenIndex];
        var ubo = new ModelUBO();

        if (masked)
        {
            UpdateMatrix(ubo.ClipMatrix, clipContext!.MatrixForDraw);
            SetColorChannel(&ubo, clipContext);
        }

        var identity = new CubismMatrix44();
        identity.LoadIdentity();
        UpdateMatrix(ubo.ProjectionMatrix, identity);

        float opacity = Model.GetOffscreenOpacity(offscreenIndex);
        var baseColor = new CubismTextureColor(opacity, opacity, opacity, opacity);
        Vector4 multiply = Model.GetOffscreenMultiplyColor(offscreenIndex);
        Vector4 screen = Model.GetOffscreenScreenColor(offscreenIndex);
        SetColorUniformBuffer(&ubo, baseColor,
            new CubismTextureColor(multiply.X, multiply.Y, multiply.Z, multiply.W),
            new CubismTextureColor(screen.X, screen.Y, screen.Z, screen.W));

        descriptor.UniformBuffer.MemCpy(&ubo, (ulong)Unsafe.SizeOf<ModelUBO>());

        if (isBlendMode)
        {
            PrepareBlendSnapshot();
        }

        ImageView blendView;
        Sampler blendSampler;
        if (isBlendMode && _blendSnapshot is { } snapshot && snapshot.IsValid())
        {
            blendView = snapshot.GetTextureView();
            blendSampler = snapshot.GetTextureSampler();
        }
        else
        {
            CubismRenderTarget_Vulkan? srcTarget = offscreen.GetRenderTarget();
            if (srcTarget == null)
            {
                return;
            }
            blendView = srcTarget.GetTextureView();
            blendSampler = srcTarget.GetTextureSampler();
        }

        UpdateOffscreenDescriptor(descriptor, masked, offscreen, clipContext, blendView, blendSampler);

        var descriptorSet = masked ? descriptor.DescriptorSetMasked : descriptor.DescriptorSet;
        _vk.CmdBindDescriptorSets(_drawCommandBuffer, PipelineBindPoint.Graphics,
            _deviceInfo.Pipeline.GetPipelineLayout(shaderIndex, blendIndex), 0, 1, ref descriptorSet, 0, null);
        _vk.CmdBindPipeline(_drawCommandBuffer, PipelineBindPoint.Graphics,
            _deviceInfo.Pipeline.GetPipeline(shaderIndex, blendIndex));

        BindQuadBuffers(_drawCommandBuffer);

        OffscreenDrawCount++;
        _vk.CmdDrawIndexed(_drawCommandBuffer, 6, 1, 0, 0, 0);
    }

    /// <summary>
    /// 更新离屏合成用的 descriptor.
    /// </summary>
    private unsafe void UpdateOffscreenDescriptor(Descriptor descriptor, bool masked,
        CubismOffscreenRenderTarget_Vulkan offscreen, CubismClippingContext_Vulkan? clipContext,
        ImageView blendView, Sampler blendSampler)
    {
        CubismRenderTarget_Vulkan? src = offscreen.GetRenderTarget();
        if (src == null)
        {
            return;
        }

        DescriptorSet descriptorSet = masked ? descriptor.DescriptorSetMasked : descriptor.DescriptorSet;
        Span<WriteDescriptorSet> writes = stackalloc WriteDescriptorSet[4];
        int count = 0;

        var bufferInfo = new DescriptorBufferInfo
        {
            Buffer = descriptor.UniformBuffer.Buffer,
            Offset = 0,
            Range = ulong.MaxValue
        };
        writes[count++] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            PBufferInfo = &bufferInfo
        };

        var srcInfo = new DescriptorImageInfo
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = src.GetTextureView(),
            Sampler = src.GetTextureSampler()
        };
        writes[count++] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            PImageInfo = &srcInfo
        };

        if (masked && clipContext != null && (uint)clipContext.BufferIndex < (uint)_offscreenMaskBuffers.Count)
        {
            var maskBuffer = _offscreenMaskBuffers[clipContext.BufferIndex];
            var maskInfo = new DescriptorImageInfo
            {
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = maskBuffer.GetTextureView(),
                Sampler = maskBuffer.GetTextureSampler()
            };
            writes[count++] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = descriptorSet,
                DstBinding = 2,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                PImageInfo = &maskInfo
            };
        }

        var blendInfo = new DescriptorImageInfo
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = blendView,
            Sampler = blendSampler
        };
        writes[count++] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 3,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            PImageInfo = &blendInfo
        };

        fixed (WriteDescriptorSet* ptr = writes)
        {
            _vk.UpdateDescriptorSets(Device, (uint)count, ptr, 0, null);
        }
    }

    /// <summary>
    /// 为 blend shader 把当前绘制目标 RT 复制到快照, 并重新开启 RT.
    /// Vulkan 中无法同时采样正在绘制的 attachment,
    /// 因此把上一刻的内容复制到另一纹理并绑定到 binding3(为通过 validation).
    /// </summary>
    private unsafe bool PrepareBlendSnapshot()
    {
        CubismRenderTarget_Vulkan? target = _currentRenderTarget
            ?? (_modelRenderTargets.Count > 0 ? _modelRenderTargets[0] : null);
        if (target == null || !target.IsValid())
        {
            return false;
        }

        if (_blendSnapshot == null)
        {
            _blendSnapshot = new CubismRenderTarget_Vulkan(_vk);
        }

        if (!_blendSnapshot.IsValid() ||
            _blendSnapshot.GetBufferWidth() != target.GetBufferWidth() ||
            _blendSnapshot.GetBufferHeight() != target.GetBufferHeight())
        {
            _blendSnapshot.DestroyRenderTarget();
            if (_blendSnapshot.CreateRenderTarget(Device, PhysicalDevice,
                target.GetBufferWidth(), target.GetBufferHeight(), SwapchainImageFormat, DepthFormat) is false)
            {
                // 降级: 快照不可用时调用方退回使用模型纹理作为 binding3.
                CubismLog.Warning("[Live2D Vulkan]failed to create blend snapshot render target, fallback to model texture.");
                return false;
            }

            _blendSnapshotInitialized = false;
        }

        if (!target.IsRendering())
        {
            return false;
        }

        target.EndDraw(_drawCommandBuffer);

        var srcBarrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.ShaderReadBit,
            DstAccessMask = AccessFlags.TransferReadBit,
            OldLayout = ImageLayout.ShaderReadOnlyOptimal,
            NewLayout = ImageLayout.TransferSrcOptimal,
            Image = target.GetTextureImage(),
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _vk.CmdPipelineBarrier(_drawCommandBuffer, PipelineStageFlags.FragmentShaderBit,
            PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, ref srcBarrier);

        var dstBarrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.TransferWriteBit,
            OldLayout = _blendSnapshotInitialized ? ImageLayout.ShaderReadOnlyOptimal : ImageLayout.Undefined,
            NewLayout = ImageLayout.TransferDstOptimal,
            Image = _blendSnapshot.GetTextureImage(),
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _vk.CmdPipelineBarrier(_drawCommandBuffer, PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, ref dstBarrier);

        var copy = new ImageCopy
        {
            SrcSubresource = new(ImageAspectFlags.ColorBit, 0, 0, 1),
            DstSubresource = new(ImageAspectFlags.ColorBit, 0, 0, 1),
            Extent = new(target.GetBufferWidth(), target.GetBufferHeight(), 1)
        };
        _vk.CmdCopyImage(_drawCommandBuffer, target.GetTextureImage(), ImageLayout.TransferSrcOptimal,
            _blendSnapshot.GetTextureImage(), ImageLayout.TransferDstOptimal, 1, ref copy);

        var toRead = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = AccessFlags.ShaderReadBit,
            OldLayout = ImageLayout.TransferDstOptimal,
            NewLayout = ImageLayout.ShaderReadOnlyOptimal,
            Image = _blendSnapshot.GetTextureImage(),
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _vk.CmdPipelineBarrier(_drawCommandBuffer, PipelineStageFlags.TransferBit,
            PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, ref toRead);
        _blendSnapshotInitialized = true;

        // 把 target 从 TransferSrc 恢复到绘制用布局后再重新开启
        var toColor = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.TransferReadBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
            OldLayout = ImageLayout.TransferSrcOptimal,
            NewLayout = ImageLayout.ColorAttachmentOptimal,
            Image = target.GetTextureImage(),
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        _vk.CmdPipelineBarrier(_drawCommandBuffer, PipelineStageFlags.TransferBit,
            PipelineStageFlags.ColorAttachmentOutputBit, 0, 0, null, 0, null, 1, ref toColor);
        target.SetColorImageCurrentLayout(ImageLayout.ColorAttachmentOptimal);

        target.BeginDraw(_drawCommandBuffer, 0.0f, 0.0f, 0.0f, 0.0f, false);
        return true;
    }

    /// <summary>
    /// 绘制模型的实际处理
    /// </summary>
    protected unsafe override void DoDrawModel()
    {
        //------------ 使用裁剪遮罩缓冲区预处理方式时 ------------
        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo
        };
        _vk.BeginCommandBuffer(_updateCommandBuffer, ref beginInfo);
        _vk.BeginCommandBuffer(_drawCommandBuffer, ref beginInfo);

        if (_clippingManager != null)
        {
            // 遮罩缓冲区重建失败时本帧按无遮罩绘制
            bool maskBuffersReady = true;

            // 尺寸不同时在此处重新创建
            for (int i = 0; i < _clippingManager.RenderTextureCount; ++i)
            {
                if (_offscreenFrameBuffers![i].BufferWidth != _clippingManager.ClippingMaskBufferSize.X
                    || _offscreenFrameBuffers[i].BufferHeight != _clippingManager.ClippingMaskBufferSize.Y)
                {
                    _offscreenFrameBuffers[i].DestroyRenderTarget();
                    if (_offscreenFrameBuffers[i].CreateRenderTarget(
                        Device, PhysicalDevice,
                        (uint)_clippingManager.ClippingMaskBufferSize.X,
                        (uint)_clippingManager.ClippingMaskBufferSize.Y,
                        ImageFormat, DepthFormat
                    ) is false)
                    {
                        // 降级: 本帧跳过遮罩生成(模型按无遮罩绘制), 下一帧会重新尝试创建.
                        CubismLog.Warning("[Live2D Vulkan]recreate clipping mask render target failed ({0}).", i);
                        maskBuffersReady = false;
                    }
                }
            }

            if (maskBuffersReady is false)
            {
                ClippingContextBufferForMask = null;
            }
            else if (UseHighPrecisionMask)
            {
                _clippingManager.SetupMatrixForHighPrecision(Model, false);
            }
            else
            {
                _clippingManager.SetupClippingContext(Model, _drawCommandBuffer, _updateCommandBuffer, this);
            }
        }

        //------------ 离屏用裁剪遮罩前处理 ------------
        if (_offscreenClippingManager != null)
        {
            // 离屏遮罩缓冲区重建失败时本帧跳过离屏遮罩
            bool offscreenMaskBuffersReady = true;

            for (int i = 0; i < _offscreenClippingManager.RenderTextureCount; ++i)
            {
                if (_offscreenMaskBuffers[i].BufferWidth != _offscreenClippingManager.ClippingMaskBufferSize.X
                    || _offscreenMaskBuffers[i].BufferHeight != _offscreenClippingManager.ClippingMaskBufferSize.Y)
                {
                    _offscreenMaskBuffers[i].DestroyRenderTarget();
                    if (_offscreenMaskBuffers[i].CreateRenderTarget(
                        Device, PhysicalDevice,
                        (uint)_offscreenClippingManager.ClippingMaskBufferSize.X,
                        (uint)_offscreenClippingManager.ClippingMaskBufferSize.Y,
                        ImageFormat, DepthFormat
                    ) is false)
                    {
                        // 降级: 本帧跳过离屏遮罩生成, 下一帧会重新尝试创建.
                        CubismLog.Warning("[Live2D Vulkan]recreate offscreen mask render target failed ({0}).", i);
                        offscreenMaskBuffersReady = false;
                    }
                }
            }

            if (offscreenMaskBuffersReady is false)
            {
                ClippingContextBufferForMask = null;
            }
            else if (UseHighPrecisionMask)
            {
                _offscreenClippingManager.SetupMatrixForHighPrecision(Model, false,
                    CsmEnum.DrawableObjectType.Offscreen, GetMvpMatrix());
            }
            else
            {
                _offscreenClippingManager.SetupClippingContext(Model, _drawCommandBuffer, _updateCommandBuffer, this);
            }
        }

        SubmitCommand(_updateCommandBuffer, _updateFinishedSemaphore);
        SubmitCommand(_drawCommandBuffer, null, _updateFinishedSemaphore);

        // 重建 swapchain 时更新深度 buffer 的尺寸
        if (_depthImage.Width != SwapchainExtent.Width || _depthImage.Height != SwapchainExtent.Height)
        {
            _depthImage.Destroy(Device);
            CreateDepthBuffer();
        }

        var drawableCount = Model.GetDrawableCount();
        var offscreenCount = Model.GetOffscreenCount();
        var totalCount = drawableCount + offscreenCount;
        var renderOrder = Model.GetRenderOrders();
        // 按绘制顺序排序索引
        // 实测: renderOrder 的数组长度是 totalCount, 只遍历 drawable 会
        // 破坏 offscreen 的顺序(以及 order 值大于等于 drawableCount 的情况).
        // 与官方 DrawObjectLoop(CubismRenderer_Vulkan.cpp:2714-2727)一样使用双重排序.
        for (int i = 0; i < totalCount; ++i)
        {
            int order = renderOrder[i];
            if (i < drawableCount)
            {
                _sortedObjectsIndexList[order] = i;
                _sortedObjectsTypeList[order] = CsmEnum.DrawableObjectType.Drawable;
            }
            else
            {
                _sortedObjectsIndexList[order] = i - drawableCount;
                _sortedObjectsTypeList[order] = CsmEnum.DrawableObjectType.Offscreen;
            }
        }

        // 绘制
        _vk.BeginCommandBuffer(_updateCommandBuffer, &beginInfo);
        _vk.BeginCommandBuffer(_drawCommandBuffer, &beginInfo);
        BeginRenderTarget(false);

        for (int i = 0; i < totalCount; ++i)
        {
            // 离屏的实际绘制(AddOffscreen / blend)尚未实现.
            // 这里只先准备好顺序数组, 本阶段仅绘制 drawable(保持既有语义).
            if (_sortedObjectsTypeList[i] == CsmEnum.DrawableObjectType.Offscreen)
            {
                // 索引在 AddOffscreen 内作为 offscreen 空间进行校验
                AddOffscreen(_sortedObjectsIndexList[i]);
                continue;
            }

            int drawableIndex = _sortedObjectsIndexList[i];
            // Drawable 非显示状态时跳过处理
            if (!Model.GetDrawableDynamicFlagIsVisible(drawableIndex))
            {
                continue;
            }

            // 若不在当前离屏的绘制范围内, 则传递到父级离屏
            SubmitDrawToParentOffscreen(drawableIndex, CsmEnum.DrawableObjectType.Drawable);

            // 设置裁剪遮罩
            var clipContext = (_clippingManager != null) ? _clippingManager.ClippingContextListForDraw[drawableIndex]
                as CubismClippingContext_Vulkan : null;

            // 高精度遮罩需要有效的遮罩渲染目标, 分配失败时跳过遮罩绘制(按无遮罩渲染).
            if (clipContext != null && UseHighPrecisionMask
                && _offscreenFrameBuffers != null
                && (uint)clipContext.BufferIndex < (uint)_offscreenFrameBuffers.Length
                && _offscreenFrameBuffers[clipContext.BufferIndex].IsValid()) // 需要绘制遮罩
            {
                if (clipContext.IsUsing) // 已确定需要绘制
                {
                    // 先切换到离屏绘制(blend/offscreen 时结束模型 RT)
                    EnsurePreviousRenderFinished(_offscreenFrameBuffers![clipContext.BufferIndex]);

                    // 按绘制顺序执行此前累积的命令
                    SubmitCommand(_updateCommandBuffer, _updateFinishedSemaphore);
                    SubmitCommand(_drawCommandBuffer, null, _updateFinishedSemaphore);
                    _vk.BeginCommandBuffer(_updateCommandBuffer, &beginInfo);
                    _vk.BeginCommandBuffer(_drawCommandBuffer, &beginInfo);

                    var currentHighPrecisionMaskColorBuffer = _offscreenFrameBuffers![clipContext.BufferIndex];
                    currentHighPrecisionMaskColorBuffer.BeginDraw(_drawCommandBuffer, 1.0f, 1.0f, 1.0f, 1.0f);

                    // 以与生成的 FrameBuffer 相同的尺寸设置 viewport
                    var viewport1 = GetViewport(_clippingManager!.ClippingMaskBufferSize.X, _clippingManager.ClippingMaskBufferSize.Y,
                        0.0f, 1.0f);
                    _vk.CmdSetViewport(_drawCommandBuffer, 0, 1, ref viewport1);

                    var rect1 = GetScissor(0.0f, 0.0f, _clippingManager.ClippingMaskBufferSize.X, _clippingManager.ClippingMaskBufferSize.Y);
                    _vk.CmdSetScissor(_drawCommandBuffer, 0, 1, ref rect1);

                    int clipDrawCount = clipContext.ClippingIdCount;
                    for (int ctx = 0; ctx < clipDrawCount; ctx++)
                    {
                        int clipDrawIndex = clipContext.ClippingIdList[ctx];

                        // 顶点信息未更新, 不可靠时跳过绘制
                        if (!Model.GetDrawableDynamicFlagVertexPositionsDidChange(clipDrawIndex))
                        {
                            continue;
                        }

                        IsCulling = Model.GetDrawableCulling(clipDrawIndex) != 0;

                        ClippingContextBufferForMask = clipContext;
                        DrawMeshVulkan(Model, clipDrawIndex, _drawCommandBuffer, _updateCommandBuffer);
                    }
                    // --- 后处理 ---
                    currentHighPrecisionMaskColorBuffer.EndDraw(_drawCommandBuffer); // 结束离屏绘制
                    ClippingContextBufferForMask = null;
                    SubmitCommand(_updateCommandBuffer, _updateFinishedSemaphore);
                    SubmitCommand(_drawCommandBuffer, null, _updateFinishedSemaphore);
                    _vk.BeginCommandBuffer(_updateCommandBuffer, &beginInfo);
                    _vk.BeginCommandBuffer(_drawCommandBuffer, &beginInfo);

                    // 恢复绘制
                    if (_currentRenderTarget != null)
                    {
                        _currentRenderTarget.BeginDraw(_drawCommandBuffer, 0.0f, 0.0f, 0.0f, 0.0f, false);
                    }
                    else
                    {
                        BeginRendering(_drawCommandBuffer, true);
                    }
                }
            }

            // 设置 viewport
            var viewport = GetViewport(SwapchainExtent.Width, SwapchainExtent.Height, 0.0f, 1.0f);
            _vk.CmdSetViewport(_drawCommandBuffer, 0, 1, &viewport);

            var rect = GetScissor(0.0f, 0.0f, SwapchainExtent.Width, SwapchainExtent.Height);
            _vk.CmdSetScissor(_drawCommandBuffer, 0, 1, &rect);

            // 设置裁剪遮罩
            ClippingContextBufferForDraw = clipContext;
            IsCulling = Model.GetDrawableCulling(drawableIndex) != 0;
            DrawMeshVulkan(Model, drawableIndex, _drawCommandBuffer, _updateCommandBuffer);
        }

        // 把剩余的离屏传递到父级后再合成
        while (_currentOffscreen != null)
        {
            DrawOffscreen(_currentOffscreen);
        }

        EndRenderTarget();
        SubmitCommand(_updateCommandBuffer, _updateFinishedSemaphore);
        SubmitCommand(_drawCommandBuffer, null, _updateFinishedSemaphore);
    }

    /// <summary>
    /// 保持绘制模型前的状态
    /// </summary>
    protected override void SaveProfile()
    {

    }

    /// <summary>
    /// 保持绘制模型前的状态
    /// </summary>
    protected override void RestoreProfile()
    {

    }
}
