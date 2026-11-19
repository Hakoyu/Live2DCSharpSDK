using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// オフスクリーン描画用レンダーターゲット（官方 FW5/Vulkan/CubismRenderTarget_Vulkan）。
/// R1 の CubismOffscreenSurface_Vulkan をリネームし、P5 の <see cref="CubismRenderTarget"/> 契約
/// （DestroyRenderTarget / GetBufferWidth / GetBufferHeight / IsValid）を実装する。
/// </summary>
public class CubismRenderTarget_Vulkan : CubismRenderTarget
{
    private readonly Vk _vk;

    /// <summary>
    /// 論理デバイス（官方 _device）。DestroyRenderTarget() を引数なしにするため保持する。
    /// </summary>
    private Device _device;

    /// <summary>
    /// オフスクリーンの横幅
    /// </summary>
    public uint BufferWidth { get; private set; }

    /// <summary>
    /// オフスクリーンの縦幅
    /// </summary>
    public uint BufferHeight { get; private set; }

    /// <summary>
    /// カラーバッファ
    /// </summary>
    private CubismImageVulkan? _colorImage;

    /// <summary>
    /// 深度バッファ
    /// </summary>
    private CubismImageVulkan? _depthImage;

    /// <summary>
    /// レンダリングパスがアクティブかどうか（官方 _isRendering）
    /// </summary>
    private bool _isRendering;

    /// <summary>
    /// パラメータなしコンストラクタ。
    /// P5 の ICubismOffscreenManager&lt;T&gt; は new T() で生成するため必須（官方も引数なし）。
    /// </summary>
    public CubismRenderTarget_Vulkan() : this(Vk.GetApi())
    {
    }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="vk">Vulkan API インスタンス</param>
    public CubismRenderTarget_Vulkan(Vk vk)
    {
        _vk = vk;
    }

    /// <summary>
    /// レンダリングターゲットのクリア
    /// 呼ぶ場合はBeginDrawの後で呼ぶこと
    /// </summary>
    /// <param name="commandBuffer">コマンドバッファ</param>
    /// <param name="r">赤(0.0~1.0)</param>
    /// <param name="g">緑(0.0~1.0)</param>
    /// <param name="b">青(0.0~1.0)</param>
    /// <param name="a">α(0.0~1.0)</param>
    /// <param name="isClear">レンダーターゲットをクリアするか（官方 R5 追加引数）</param>
    public unsafe void BeginDraw(CommandBuffer commandBuffer, float r, float g, float b, float a, bool isClear = true)
    {
        // レンダリングパスがアクティブの場合はスキップ（官方 R5）
        if (_isRendering)
        {
            return;
        }

        if (_colorImage == null)
        {
            CubismLog.Error("colorImage is null.");
            return;
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

        if (_depthImage == null)
        {
            CubismLog.Error("depthImage is null.");
            return;
        }

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
    }

    /// <summary>
    /// 描画終了
    /// </summary>
    /// <param name="commandBuffer">コマンドバッファ</param>
    /// <param name="currentLayout">現在のイメージレイアウト（官方 R5 引数）</param>
    public unsafe void EndDraw(CommandBuffer commandBuffer, ImageLayout currentLayout = ImageLayout.ColorAttachmentOptimal)
    {
        // レンダリングパスがアクティブでない場合はスキップ（官方 R5）
        if (!_isRendering)
        {
            return;
        }

        _isRendering = false;

        if (_colorImage == null)
        {
            CubismLog.Error("colorImage is null.");
            return;
        }

        _vk.CmdEndRendering(commandBuffer);

        // レイアウト変更
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

        if (_depthImage == null)
        {
            CubismLog.Error("depthImage is null.");
            return;
        }

        memoryBarrier.DstAccessMask = AccessFlags.None;
        memoryBarrier.OldLayout = ImageLayout.DepthStencilAttachmentOptimal;
        memoryBarrier.NewLayout = ImageLayout.DepthStencilAttachmentOptimal;
        memoryBarrier.Image = _depthImage.Image;
        memoryBarrier.SubresourceRange = new(ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit, 0, 1, 0, 1);

        _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.BottomOfPipeBit, 0, 0, null, 0, null, 1, ref memoryBarrier);

        _depthImage.SetCurrentLayout(ImageLayout.DepthStencilAttachmentOptimal);
    }

    /// <summary>
    /// CubismRenderTargetを作成する（官方 CreateRenderTarget）。
    /// </summary>
    /// <param name="device">論理デバイス</param>
    /// <param name="physicalDevice">物理デバイス</param>
    /// <param name="displayBufferWidth">オフスクリーンの横幅</param>
    /// <param name="displayBufferHeight">オフスクリーンの縦幅</param>
    /// <param name="surfaceFormat">サーフェスフォーマット</param>
    /// <param name="depthFormat">深度フォーマット</param>
    public void CreateRenderTarget(Device device, PhysicalDevice physicalDevice,
        uint displayBufferWidth, uint displayBufferHeight, Format surfaceFormat, Format depthFormat)
    {
        _device = device;

        _colorImage = new(_vk);
        _colorImage.CreateImage(device, physicalDevice, displayBufferWidth, displayBufferHeight,
                                 1, surfaceFormat, ImageTiling.Optimal,
                                 ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit |
                                 ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit);
        _colorImage.CreateView(device, surfaceFormat, ImageAspectFlags.ColorBit, 1);
        _colorImage.CreateSampler(device, 1.0f, 1);

        _depthImage = new(_vk);
        _depthImage.CreateImage(device, physicalDevice, displayBufferWidth, displayBufferHeight,
                                 1, depthFormat, ImageTiling.Optimal,
                                ImageUsageFlags.DepthStencilAttachmentBit);

        _depthImage.CreateView(device, depthFormat, ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit, 1);

        BufferWidth = displayBufferWidth;
        BufferHeight = displayBufferHeight;
    }

    /// <summary>
    /// レンダーターゲットを削除する（官方 DestroyRenderTarget、P5 契約の引数なし版）
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
    }

    /// <summary>
    /// イメージへのアクセッサ
    /// </summary>
    public Image GetTextureImage()
    {
        if (_colorImage == null)
        {
            CubismLog.Error("colorImage is null.");
            return default;
        }

        return _colorImage.Image;
    }

    /// <summary>
    /// テクスチャビューへのアクセッサ
    /// </summary>
    public ImageView GetTextureView()
    {
        if (_colorImage == null)
        {
            CubismLog.Error("colorImage is null.");
            return default;
        }

        return _colorImage.View;
    }

    /// <summary>
    /// テクスチャサンプラーへのアクセッサ
    /// </summary>
    public Sampler GetTextureSampler()
    {
        if (_colorImage == null)
        {
            CubismLog.Error("colorImage is null.");
            return default;
        }

        return _colorImage.Sampler;
    }

    /// <summary>
    /// バッファ幅を返す（P5 契約）
    /// </summary>
    public override uint GetBufferWidth()
    {
        return BufferWidth;
    }

    /// <summary>
    /// バッファ高さを返す（P5 契約）
    /// </summary>
    public override uint GetBufferHeight()
    {
        return BufferHeight;
    }

    /// <summary>
    /// 現在有効かどうかを返す（P5 契約）
    /// </summary>
    public override bool IsValid()
    {
        return _colorImage != null && _depthImage != null;
    }

    /// <summary>
    /// レンダリングパスがアクティブかどうかを返す（官方 IsRendering）
    /// </summary>
    public bool IsRendering()
    {
        return _isRendering;
    }

    /// <summary>
    /// カラーイメージの現在のレイアウトを設定する（官方 SetColorImageCurrentLayout）
    /// </summary>
    /// <param name="newLayout">新しいレイアウト</param>
    public void SetColorImageCurrentLayout(ImageLayout newLayout)
    {
        _colorImage?.SetCurrentLayout(newLayout);
    }
}
