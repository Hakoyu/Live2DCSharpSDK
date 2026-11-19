using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// 离屏绘制用的渲染目标(对应官方 Framework/Rendering/Vulkan/CubismRenderTarget_Vulkan).
/// 由旧的 CubismOffscreenSurface_Vulkan 更名而来, 实现 <see cref="CubismRenderTarget"/> 契约
/// 即 (DestroyRenderTarget / GetBufferWidth / GetBufferHeight / IsValid).
/// </summary>
public class CubismRenderTarget_Vulkan : CubismRenderTarget
{
    private readonly Vk _vk;

    /// <summary>
    /// 逻辑设备(官方 _device). 为让 DestroyRenderTarget() 不带参数而保留.
    /// </summary>
    private Device _device;

    /// <summary>
    /// 离屏的宽度
    /// </summary>
    public uint BufferWidth { get; private set; }

    /// <summary>
    /// 离屏的高度
    /// </summary>
    public uint BufferHeight { get; private set; }

    /// <summary>
    /// 颜色缓冲区
    /// </summary>
    private CubismImageVulkan? _colorImage;

    /// <summary>
    /// 深度缓冲区
    /// </summary>
    private CubismImageVulkan? _depthImage;

    /// <summary>
    /// 渲染通道是否处于活动状态(官方 _isRendering)
    /// </summary>
    private bool _isRendering;

    /// <summary>
    /// 无参构造函数.
    /// ICubismOffscreenManager&lt;T&gt; 用 new T() 创建实例, 因此必须提供无参构造(官方同样无参).
    /// </summary>
    public CubismRenderTarget_Vulkan() : this(Vk.GetApi())
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="vk">Vulkan API 实例</param>
    public CubismRenderTarget_Vulkan(Vk vk)
    {
        _vk = vk;
    }

    /// <summary>
    /// 清除渲染目标
    /// 调用时须在 BeginDraw 之后调用
    /// </summary>
    /// <param name="commandBuffer">命令缓冲区</param>
    /// <param name="r">红色(0.0~1.0)</param>
    /// <param name="g">绿色(0.0~1.0)</param>
    /// <param name="b">蓝色(0.0~1.0)</param>
    /// <param name="a">α(0.0~1.0)</param>
    /// <param name="isClear">是否清除渲染目标(官方 R5 新增参数)</param>
    /// <returns>渲染通道已开启时返回 true, 渲染目标无效时返回 false(后续绘制应跳过)</returns>
    public unsafe bool BeginDraw(CommandBuffer commandBuffer, float r, float g, float b, float a, bool isClear = true)
    {
        // 渲染通道处于活动状态时跳过(官方 R5)
        if (_isRendering)
        {
            return true;
        }

        // 附件缺失(例如分配失败导致的降级目标)时直接失败, 不再使用未初始化的句柄开启渲染通道.
        if (_colorImage is null || _depthImage is null
            || _colorImage.IsValid is false || _depthImage.IsValid is false)
        {
            CubismLog.Warning("[Live2D Vulkan]begin draw on invalid render target ({0}x{1}).", BufferWidth, BufferHeight);
            return false;
        }

        _colorImage.SetImageLayout(commandBuffer, ImageLayout.ColorAttachmentOptimal, 1, ImageAspectFlags.ColorBit);

        var colorAttachment = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfoKhr,
            ImageView = _colorImage.View,
            ImageLayout = ImageLayout.AttachmentOptimalKhr,
            LoadOp = isClear ? AttachmentLoadOp.Clear : AttachmentLoadOp.Load,
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = new(new(r, g, b, a))
        };

        _depthImage.SetImageLayout(commandBuffer, ImageLayout.DepthStencilAttachmentOptimal,
            1, ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit);

        var depthStencilAttachment = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfoKhr,
            ImageView = _depthImage.View,
            ImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            ClearValue = new(new(1.0f, 0.0f))
        };

        var renderingInfo = new RenderingInfo
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new(new(0, 0), new(BufferWidth, BufferHeight)),
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachment,
            PDepthAttachment = &depthStencilAttachment
        };

        _vk.CmdBeginRendering(commandBuffer, ref renderingInfo);
        _isRendering = true;
        return true;
    }

    /// <summary>
    /// 结束绘制
    /// </summary>
    /// <param name="commandBuffer">命令缓冲区</param>
    /// <param name="currentLayout">当前的 image 布局(官方 R5 参数)</param>
    public unsafe void EndDraw(CommandBuffer commandBuffer, ImageLayout currentLayout = ImageLayout.ColorAttachmentOptimal)
    {
        // 渲染通道未处于活动状态时跳过(官方 R5)
        if (!_isRendering)
        {
            return;
        }

        _isRendering = false;

        if (_colorImage is null || _colorImage.IsValid is false)
        {
            CubismLog.Warning("[Live2D Vulkan]end draw on invalid color image.");
            return;
        }

        _vk.CmdEndRendering(commandBuffer);

        // 变更布局
        var memoryBarrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstAccessMask = AccessFlags.ShaderReadBit,
            OldLayout = currentLayout,
            NewLayout = ImageLayout.ShaderReadOnlyOptimal,
            Image = _colorImage.Image,
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };

        _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.ColorAttachmentOutputBit,
                             PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, ref memoryBarrier);

        _colorImage.SetCurrentLayout(ImageLayout.ShaderReadOnlyOptimal);

        if (_depthImage is null || _depthImage.IsValid is false)
        {
            CubismLog.Warning("[Live2D Vulkan]end draw on invalid depth image.");
            return;
        }

        // depth/stencil 的写入发生在 early / late fragment tests 中,
        // 因此源阶段应使用 EarlyFragmentTests|LateFragmentTests 而不是 ColorAttachmentOutputBit
        // (官方 5-r.5 仍为 ColorAttachmentOutputBit, 但与深度附件的写入阶段不一致).
        // srcAccessMask 也配合深度写入(阶段与访问类型的组合保持一致. dstAccessMask 仍为 None).
        memoryBarrier.SrcAccessMask = AccessFlags.DepthStencilAttachmentWriteBit;
        memoryBarrier.DstAccessMask = AccessFlags.None;
        memoryBarrier.OldLayout = ImageLayout.DepthStencilAttachmentOptimal;
        memoryBarrier.NewLayout = ImageLayout.DepthStencilAttachmentOptimal;
        memoryBarrier.Image = _depthImage.Image;
        memoryBarrier.SubresourceRange = new(ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit, 0, 1, 0, 1);

        _vk.CmdPipelineBarrier(commandBuffer,
            PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
            PipelineStageFlags.BottomOfPipeBit, 0, 0, null, 0, null, 1, ref memoryBarrier);

        _depthImage.SetCurrentLayout(ImageLayout.DepthStencilAttachmentOptimal);
    }

    /// <summary>
    /// 创建 CubismRenderTarget(对应官方 CreateRenderTarget).
    /// </summary>
    /// <param name="device">逻辑设备</param>
    /// <param name="physicalDevice">物理设备</param>
    /// <param name="displayBufferWidth">离屏的宽度</param>
    /// <param name="displayBufferHeight">离屏的高度</param>
    /// <param name="surfaceFormat">surface 格式</param>
    /// <param name="depthFormat">深度格式</param>
    /// <returns>成功返回 true, 失败时已回收部分资源并返回 false(此时 IsValid() 为 false)</returns>
    public bool CreateRenderTarget(Device device, PhysicalDevice physicalDevice,
        uint displayBufferWidth, uint displayBufferHeight, Format surfaceFormat, Format depthFormat)
    {
        DestroyRenderTarget();

        if (displayBufferWidth == 0 || displayBufferHeight == 0)
        {
            CubismLog.Warning("[Live2D Vulkan]create render target with zero extent ({0}x{1}).",
                displayBufferWidth, displayBufferHeight);
            return false;
        }

        _device = device;

        _colorImage = new(_vk);
        if (_colorImage.CreateImage(device, physicalDevice, displayBufferWidth, displayBufferHeight,
                                 1, surfaceFormat, ImageTiling.Optimal,
                                 ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit |
                                 ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit) is false)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to create render target color image ({0}x{1}).",
                displayBufferWidth, displayBufferHeight);
            DestroyRenderTarget();
            return false;
        }

        if (_colorImage.CreateView(device, surfaceFormat, ImageAspectFlags.ColorBit, 1) is false
            || _colorImage.CreateSampler(device, 1.0f, 1) is false)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to create render target color image view/sampler ({0}x{1}).",
                displayBufferWidth, displayBufferHeight);
            DestroyRenderTarget();
            return false;
        }

        _depthImage = new(_vk);
        if (_depthImage.CreateImage(device, physicalDevice, displayBufferWidth, displayBufferHeight,
                                 1, depthFormat, ImageTiling.Optimal,
                                ImageUsageFlags.DepthStencilAttachmentBit) is false)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to create render target depth image ({0}x{1}).",
                displayBufferWidth, displayBufferHeight);
            DestroyRenderTarget();
            return false;
        }

        if (_depthImage.CreateView(device, depthFormat, ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit, 1) is false)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to create render target depth image view ({0}x{1}).",
                displayBufferWidth, displayBufferHeight);
            DestroyRenderTarget();
            return false;
        }

        BufferWidth = displayBufferWidth;
        BufferHeight = displayBufferHeight;
        return true;
    }

    /// <summary>
    /// 删除渲染目标(官方 DestroyRenderTarget 的无参版本).
    /// </summary>
    public override void DestroyRenderTarget()
    {
        if (_colorImage != null)
        {
            _colorImage.Destroy(_device);
            _colorImage = null;
        }
        if (_depthImage != null)
        {
            _depthImage.Destroy(_device);
            _depthImage = null;
        }

        _device = default;
        BufferWidth = 0;
        BufferHeight = 0;
    }

    /// <summary>
    /// image 的访问器
    /// </summary>
    public Image GetTextureImage()
    {
        if (_colorImage is null || _colorImage.IsValid is false)
        {
            CubismLog.Warning("[Live2D Vulkan]texture image is not available.");
            return default;
        }

        return _colorImage.Image;
    }

    /// <summary>
    /// texture view 的访问器
    /// </summary>
    public ImageView GetTextureView()
    {
        if (_colorImage is null || _colorImage.IsValid is false)
        {
            CubismLog.Warning("[Live2D Vulkan]texture view is not available.");
            return default;
        }

        return _colorImage.View;
    }

    /// <summary>
    /// texture sampler 的访问器
    /// </summary>
    public Sampler GetTextureSampler()
    {
        if (_colorImage is null || _colorImage.IsValid is false)
        {
            CubismLog.Warning("[Live2D Vulkan]texture sampler is not available.");
            return default;
        }

        return _colorImage.Sampler;
    }

    /// <summary>
    /// 返回缓冲区宽度.
    /// </summary>
    public override uint GetBufferWidth()
    {
        return BufferWidth;
    }

    /// <summary>
    /// 返回缓冲区高度.
    /// </summary>
    public override uint GetBufferHeight()
    {
        return BufferHeight;
    }

    /// <summary>
    /// 返回当前是否有效.
    /// </summary>
    public override bool IsValid()
    {
        return _colorImage != null && _depthImage != null;
    }

    /// <summary>
    /// 返回渲染通道是否处于活动状态(官方 IsRendering)
    /// </summary>
    public bool IsRendering()
    {
        return _isRendering;
    }

    /// <summary>
    /// 设置颜色 image 的当前布局(对应官方 SetColorImageCurrentLayout)
    /// </summary>
    /// <param name="newLayout">新的布局</param>
    public void SetColorImageCurrentLayout(ImageLayout newLayout)
    {
        _colorImage?.SetCurrentLayout(newLayout);
    }
}
