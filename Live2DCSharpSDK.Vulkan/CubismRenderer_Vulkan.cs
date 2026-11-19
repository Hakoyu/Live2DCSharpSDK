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
    /// レンダラを作成するための各種設定
    /// モデルを読み込む前に一度だけ呼び出す
    /// </summary>
    /// <param name="device">論理デバイス</param>
    /// <param name="physicalDevice">物理デバイス</param>
    /// <param name="commandPool">コマンドプール</param>
    /// <param name="queue">キュー</param>
    /// <param name="extent">描画解像度</param>
    /// <param name="depthFormat">深度フォーマット</param>
    /// <param name="surfaceFormat">フレームバッファのフォーマット</param>
    /// <param name="swapchainImage"></param>
    /// <param name="swapchainImageView">スワップチェーンのイメージビュー</param>
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
    /// レンダーターゲット変更を有効にする
    /// </summary>
    public static void EnableChangeRenderTarget()
    {
        UseRenderTarget = true;
    }

    /// <summary>
    /// レンダーターゲットを変更した際にレンダラを作成するための各種設定
    /// </summary>
    /// <param name="image">イメージ</param>
    /// <param name="imageview">イメージビュー</param>
    public static void SetRenderTarget(Image image, ImageView imageview)
    {
        RenderTargetImage = image;
        RenderTargetImageView = imageview;
    }

    /// <summary>
    /// 行列を更新する
    /// </summary>
    /// <param name="vkMat4">更新する行列</param>
    /// <param name="cubismMat">新しい値</param>
    public static unsafe void UpdateMatrix(float* vkMat4, CubismMatrix44 cubismMat)
    {
        for (int i = 0; i < 16; i++)
        {
            vkMat4[i] = cubismMat.Tr[i];
        }
    }

    /// <summary>
    /// カラーベクトルを更新する
    /// </summary>
    /// <param name="vkVec4">更新するカラーベクトル</param>
    /// <param name="r">新しい値</param>
    /// <param name="g">新しい値</param>
    /// <param name="b">新しい値</param>
    /// <param name="a">新しい値</param>
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
    /// デバイスごとのパイプライン／オフスクリーンマネージャ（官方 CubismDeviceInfo_Vulkan）
    /// </summary>
    private CubismDeviceInfo_Vulkan _deviceInfo;

    /// <summary>
    /// Dispose の二重実行を防ぐ
    /// </summary>
    private bool _disposed;

    private FvkCmdSetCullMode vkCmdSetCullModeEXT;

    /// <summary>
    /// クリッピングマスク管理オブジェクト
    /// </summary>
    private CubismClippingManager_Vulkan _clippingManager;
    /// <summary>
    /// オフスクリーン用クリッピングマスク管理オブジェクト（P8c-offscreen）
    /// </summary>
    private CubismClippingManager_Vulkan? _offscreenClippingManager;
    /// <summary>
    /// オフスクリーン用クリッピングコンテキスト（P8c-offscreen）
    /// </summary>
    public CubismClippingContext_Vulkan? ClippingContextBufferForOffscreen { get; set; }
    /// <summary>
    /// モデルのオフスクリーン一覧（官方 _offscreenList）
    /// </summary>
    private readonly List<CubismOffscreenRenderTarget_Vulkan> _offscreenList = [];
    /// <summary>
    /// 現在描画中のオフスクリーン（官方 _currentOffscreen）
    /// </summary>
    private CubismOffscreenRenderTarget_Vulkan? _currentOffscreen;
    /// <summary>
    /// 現在の描画先レンダーターゲット（官方 _currentRenderTarget）
    /// </summary>
    private CubismRenderTarget_Vulkan? _currentRenderTarget;
    /// <summary>
    /// モデル全体を描画するレンダーターゲット（官方 _modelRenderTargets）
    /// </summary>
    private readonly List<CubismRenderTarget_Vulkan> _modelRenderTargets = [];
    /// <summary>
    /// blend shader 用のスナップショット（描画中 RT を直接サンプルすると validation error になるため）
    /// </summary>
    private CubismRenderTarget_Vulkan? _blendSnapshot;
    private bool _blendSnapshotInitialized;
    /// <summary>
    /// オフスクリーン用マスクバッファ（官方 _offscreenMaskBuffers）
    /// </summary>
    private readonly List<CubismRenderTarget_Vulkan> _offscreenMaskBuffers = [];
    /// <summary>
    /// blend / offscreen を使用するためモデル RT 経由で描画するか
    /// </summary>
    private bool _useModelRenderTarget;
    /// <summary>
    /// モデル RT をクリア済みか（官方 _isClearedModelRenderTarget）
    /// </summary>
    private bool _isClearedModelRenderTarget;
    /// <summary>
    /// オフスクリーンを描画中か（マスクバッファ選択に使用）
    /// </summary>
    private bool _isDrawingOffscreen;
    /// <summary>
    /// オフスクリーン合成用ディスクリプタ（index = offscreenIndex）
    /// </summary>
    private readonly List<Descriptor> _offscreenDescriptors = [];
    /// <summary>
    /// モデル RT / オフスクリーン合成用コピーディスクリプタ
    /// </summary>
    private Descriptor? _copyDescriptor;
    /// <summary>
    /// 合成用フルスクリーンクワッドの頂点/インデックスバッファ
    /// </summary>
    private CubismBufferVulkan? _quadVertexBuffer;
    private CubismBufferVulkan? _quadStagingBuffer;
    private CubismBufferVulkan? _quadIndexBuffer;
    /// <summary>
    /// 描画オブジェクトのインデックスを描画順に並べたリスト
    /// </summary>
    private readonly int[] _sortedObjectsIndexList;
    /// <summary>
    /// 描画順に並べたオブジェクト種別（G1 門禁: renderOrder は drawableCount + offscreenCount）
    /// </summary>
    private readonly CsmEnum.DrawableObjectType[] _sortedObjectsTypeList;
    /// <summary>
    /// マスクテクスチャに描画するためのクリッピングコンテキスト
    /// </summary>
    public CubismClippingContext_Vulkan? ClippingContextBufferForMask { get; set; }
    /// <summary>
    /// 画面上描画するためのクリッピングコンテキスト
    /// </summary>
    public CubismClippingContext_Vulkan? ClippingContextBufferForDraw { get; set; }
    /// <summary>
    /// マスク描画用のフレームバッファ
    /// </summary>
    private CubismRenderTarget_Vulkan[]? _offscreenFrameBuffers;
    /// <summary>
    /// 頂点バッファ
    /// </summary>
    private CubismBufferVulkan[] _vertexBuffers;
    /// <summary>
    /// 頂点バッファを更新する際に使うステージングバッファ
    /// </summary>
    private readonly List<CubismBufferVulkan> _stagingBuffers = [];
    /// <summary>
    /// インデックスバッファ
    /// </summary>
    private CubismBufferVulkan[] _indexBuffers;
    /// <summary>
    /// ディスクリプタプール
    /// </summary>
    private DescriptorPool _descriptorPool;
    /// <summary>
    /// ディスクリプタセットのレイアウト
    /// </summary>
    private DescriptorSetLayout _descriptorSetLayout;
    /// <summary>
    /// ディスクリプタ管理オブジェクト
    /// </summary>
    private readonly List<Descriptor> _descriptorSets = [];
    /// <summary>
    /// モデルが使うテクスチャ
    /// </summary>
    private readonly List<CubismImageVulkan> _textures = [];
    /// <summary>
    /// オフスクリーンの色情報を保持する深度画像
    /// </summary>
    private readonly CubismImageVulkan _depthImage;
    /// <summary>
    /// セマフォ
    /// </summary>
    private Semaphore _updateFinishedSemaphore;
    /// <summary>
    /// 更新用コマンドバッファ
    /// </summary>
    private CommandBuffer _updateCommandBuffer;
    /// <summary>
    /// 描画用コマンドバッファ
    /// </summary>
    private CommandBuffer _drawCommandBuffer;

    /// <summary>
    /// swapchain / 深度イメージの現在レイアウト（validation 用の簡易トラッキング）
    /// </summary>
    private readonly Dictionary<ulong, ImageLayout> _imageLayouts = [];

    private readonly VulkanManager _vulkanManager;

    /// <summary>
    /// モデルのオフスクリーン数（t16 検証用）
    /// </summary>
    public int OffscreenCount => _offscreenList.Count;

    /// <summary>
    /// DrawOffscreen 実行回数（t16 検証用）
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

        // G1 門禁（tester 実測）: csmGetRenderOrders の配列長 = drawableCount + offscreenCount。
        // drawable / offscreen を合わせた順序配列を確保する（官方 DrawObjectLoop:2714-2727）。
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
            //モデルがマスクを使用している時のみにする
            _clippingManager = new CubismClippingManager_Vulkan(_vk);
            _clippingManager.Initialize(model, maskBufferCount);

            var bufferWidth = (uint)_clippingManager.ClippingMaskBufferSize.X;
            var bufferHeight = (uint)_clippingManager.ClippingMaskBufferSize.Y;

            _offscreenFrameBuffers = new CubismRenderTarget_Vulkan[maskBufferCount];
            // バックバッファ分確保
            for (int i = 0; i < maskBufferCount; i++)
            {
                _offscreenFrameBuffers[i] = new(_vk);
                _offscreenFrameBuffers[i].CreateRenderTarget(Device, PhysicalDevice, bufferWidth, bufferHeight,
                    ImageFormat, DepthFormat);
                InitializeMaskBufferLayout(_offscreenFrameBuffers[i]);
            }
        }

        //------------ オフスクリーン用クリッピングマスク（P8c-offscreen）------------
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
                mask.CreateRenderTarget(Device, PhysicalDevice,
                    (uint)_offscreenClippingManager.ClippingMaskBufferSize.X,
                    (uint)_offscreenClippingManager.ClippingMaskBufferSize.Y,
                    ImageFormat, DepthFormat);
                InitializeMaskBufferLayout(mask);
                _offscreenMaskBuffers.Add(mask);
            }
        }

        //------------ 描画順ソート + オフスクリーン一覧（P8c-offscreen）------------
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
    /// マスクバッファを初期レイアウト（ShaderReadOnly）へ遷移させる。
    /// 未使用のマスクを descriptor に束縛した際の validation error を避ける。
    /// </summary>
    private unsafe void InitializeMaskBufferLayout(CubismRenderTarget_Vulkan mask)
    {
        if (!mask.IsValid())
        {
            return;
        }

        // 描画パスを開かず、レイアウト遷移だけで初期化する
        // （dynamicRenderingUnusedAttachments の validation error を避ける）。
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
    /// オフスクリーンの親を探して設定する（官方 SetupParentOffscreens）。
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
    /// スワップチェーンを再作成したときに変更されるリソースを更新する
    /// </summary>
    /// <param name="extent">クリッピングマスクバッファのサイズ</param>
    /// <param name="image"></param>
    /// <param name="imageView">イメージビュー</param>
    public static void UpdateSwapchainVariable(Extent2D extent, Image image, ImageView imageView)
    {
        SwapchainExtent = extent;
        SwapchainImage = image;
        SwapchainImageView = imageView;
    }

    /// <summary>
    /// スワップチェーンイメージを更新する
    /// </summary>
    /// <param name="image">イメージ</param>
    /// <param name="imageView">イメージビュー</param>
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
    /// コマンドの記録を開始する。
    /// </summary>
    /// <returns>記録を開始したコマンドバッファ</returns>
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
    /// コマンドを実行する。
    /// </summary>
    /// <param name="commandBuffer">コマンドバッファ</param>
    /// <param name="signalUpdateFinishedSemaphore">フェンス</param>
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
    /// コマンドバッファを作成する。
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
    /// 空の頂点バッファを作成する。
    /// </summary>
    public void CreateVertexBuffer()
    {
        int drawableCount = Model.GetDrawableCount();
        _vertexBuffers = new CubismBufferVulkan[drawableCount];

        for (int drawAssign = 0; drawAssign < drawableCount; drawAssign++)
        {
            int vcount = Model.GetDrawableVertexCount(drawAssign);
            if (vcount != 0)
            {
                //頂点データは初期化できない

                var bufferSize = (ulong)(Unsafe.SizeOf<ModelVertex>() * vcount); // 総長 構造体サイズ*個数
                var stagingBuffer = new CubismBufferVulkan(_vk);
                stagingBuffer.CreateBuffer(Device, PhysicalDevice, bufferSize, BufferUsageFlags.TransferSrcBit,
                                          MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

                stagingBuffer.Map(Device, bufferSize);
                _stagingBuffers.Add(stagingBuffer);

                var vertexBuffer = new CubismBufferVulkan(_vk);
                vertexBuffer.CreateBuffer(Device, PhysicalDevice, bufferSize,
                                           BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
                                           MemoryPropertyFlags.DeviceLocalBit);
                _vertexBuffers[drawAssign] = vertexBuffer;
            }
        }
    }

    /// <summary>
    /// インデックスバッファを作成する。
    /// </summary>
    public unsafe void CreateIndexBuffer()
    {
        int drawableCount = Model.GetDrawableCount();
        _indexBuffers = new CubismBufferVulkan[drawableCount];

        for (int drawAssign = 0; drawAssign < drawableCount; drawAssign++)
        {
            int icount = Model.GetDrawableVertexIndexCount(drawAssign);
            if (icount != 0)
            {
                var bufferSize = (ulong)(2 * icount);
                var indices = Model.GetDrawableVertexIndices(drawAssign);

                var stagingBuffer = new CubismBufferVulkan(_vk);
                stagingBuffer.CreateBuffer(Device, PhysicalDevice, bufferSize, BufferUsageFlags.TransferSrcBit,
                                           MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
                stagingBuffer.Map(Device, bufferSize);
                stagingBuffer.MemCpy(indices, bufferSize);
                stagingBuffer.UnMap(Device);

                var indexBuffer = new CubismBufferVulkan(_vk);
                indexBuffer.CreateBuffer(Device, PhysicalDevice, bufferSize,
                                         BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
                                         MemoryPropertyFlags.DeviceLocalBit);

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
    }

    /// <summary>
    /// ユニフォームバッファとディスクリプタセットを作成する。
    /// ディスクリプタセットレイアウトはユニフォームバッファ1つとモデル用テクスチャ1つとマスク用テクスチャ1つを指定する。
    /// </summary>
    public unsafe void CreateDescriptorSets()
    {
        // ディスクリプタプールの作成
        int drawableCount = Model.GetDrawableCount();
        int offscreenCount = Model.GetOffscreenCount();
        // テクスチャは 3 枚: binding1=モデル, binding2=マスク, binding3=blend 用（P8c）
        int textureCount = 3;
        int drawModeCount = 2; // マスクされる描画と通常の描画
        // drawable 用 + offscreen 合成用（masked/unmasked）+ copy 用
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
                DescriptorCount = (uint)(descriptorSetCount * textureCount) // drawableCount * 描画方法の数 * テクスチャの数
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

        // ディスクリプタセットレイアウトの作成
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
                // 5.3 以降の blend shader 用（s_blendTexture）
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

            desc.UniformBuffer.CreateBuffer(Device, PhysicalDevice, (ulong)Unsafe.SizeOf<ModelUBO>(),
                                       BufferUsageFlags.UniformBufferBit,
                                      MemoryPropertyFlags.HostVisibleBit |
                                      MemoryPropertyFlags.HostCoherentBit);
            desc.UniformBuffer.Map(Device, ulong.MaxValue);
            _descriptorSets.Add(desc);
        }

        // オフスクリーン合成用（offscreen ごとに masked/unmasked の 2 セット）
        _offscreenDescriptors.Clear();
        for (int offscreenIndex = 0; offscreenIndex < offscreenCount; ++offscreenIndex)
        {
            var desc = new Descriptor
            {
                UniformBuffer = new CubismBufferVulkan(_vk)
            };
            desc.UniformBuffer.CreateBuffer(Device, PhysicalDevice, (ulong)Unsafe.SizeOf<ModelUBO>(),
                BufferUsageFlags.UniformBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            desc.UniformBuffer.Map(Device, ulong.MaxValue);
            _offscreenDescriptors.Add(desc);
        }

        // Copy 合成用（モデル RT → swapchain）
        _copyDescriptor = new Descriptor
        {
            UniformBuffer = new CubismBufferVulkan(_vk)
        };
        _copyDescriptor.UniformBuffer.CreateBuffer(Device, PhysicalDevice, (ulong)Unsafe.SizeOf<ModelUBO>(),
            BufferUsageFlags.UniformBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        _copyDescriptor.UniformBuffer.Map(Device, ulong.MaxValue);

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
    /// モデル RT / オフスクリーン合成用のフルスクリーンクワッドバッファを作成する。
    /// 公式 modelRenderTargetVertexArray / modelRenderTargetIndexArray と同じ頂点列（P8c-offscreen）。
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

        ulong vertexSize = (ulong)(Unsafe.SizeOf<ModelVertex>() * vertices.Length);
        _quadStagingBuffer = new(_vk);
        _quadStagingBuffer.CreateBuffer(Device, PhysicalDevice, vertexSize, BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        _quadStagingBuffer.Map(Device, vertexSize);
        fixed (ModelVertex* v = vertices)
        {
            _quadStagingBuffer.MemCpy(v, vertexSize);
        }
        _quadStagingBuffer.UnMap(Device);
        _quadVertexBuffer = new(_vk);
        _quadVertexBuffer.CreateBuffer(Device, PhysicalDevice, vertexSize,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit);

        ulong indexSize = (ulong)(sizeof(ushort) * indices.Length);
        var indexStaging = new CubismBufferVulkan(_vk);
        indexStaging.CreateBuffer(Device, PhysicalDevice, indexSize, BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        indexStaging.Map(Device, indexSize);
        fixed (ushort* i = indices)
        {
            indexStaging.MemCpy(i, indexSize);
        }
        indexStaging.UnMap(Device);
        _quadIndexBuffer = new(_vk);
        _quadIndexBuffer.CreateBuffer(Device, PhysicalDevice, indexSize,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit);

        var commandBuffer = BeginSingleTimeCommands();
        var vertexCopy = new BufferCopy { Size = vertexSize };
        _vk.CmdCopyBuffer(commandBuffer, _quadStagingBuffer.Buffer, _quadVertexBuffer.Buffer, 1, ref vertexCopy);
        var indexCopy = new BufferCopy { Size = indexSize };
        _vk.CmdCopyBuffer(commandBuffer, indexStaging.Buffer, _quadIndexBuffer.Buffer, 1, ref indexCopy);
        SubmitCommand(commandBuffer);
        indexStaging.Destroy(Device);
    }

    /// <summary>
    /// 深度バッファを作成する。
    /// </summary>
    public void CreateDepthBuffer()
    {
        _depthImage.CreateImage(Device, PhysicalDevice, SwapchainExtent.Width, SwapchainExtent.Height,
        1, DepthFormat, ImageTiling.Optimal,
         ImageUsageFlags.DepthStencilAttachmentBit);
        _depthImage.CreateView(Device, DepthFormat,
             ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit, 1);
    }

    /// <summary>
    /// レンダラーを初期化する。
    /// </summary>
    public unsafe void InitializeRenderer()
    {
        // デバイスごとのパイプライン／オフスクリーンマネージャを取得（官方 InitializeRenderer: _deviceInfo = GetDeviceInfo(s_device)）
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
    /// 頂点バッファを更新する。
    /// </summary>
    /// <param name="drawAssign">描画インデックス</param>
    /// <param name="vcount">頂点数</param>
    /// <param name="varray">頂点配列</param>
    /// <param name="uvarray">uv配列</param>
    /// <param name="commandBuffer">コマンドバッファ</param>
    public unsafe void CopyToBuffer(int drawAssign, int vcount, float* varray, float* uvarray,
                      CommandBuffer commandBuffer)
    {
        var vertices = new ModelVertex[vcount];

        for (int ct = 0, ct1 = 0; ct < vcount; ct++, ct1 += 2)
        {
            // モデルデータからのコピー
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

        fixed (void* ptr = vertices)
        {
            _stagingBuffers[drawAssign].MemCpy(ptr, (ulong)bufferSize);
        }

        var copyRegion = new BufferCopy
        {
            Size = (ulong)bufferSize
        };

        _vk.CmdCopyBuffer(commandBuffer, _stagingBuffers[drawAssign].Buffer, _vertexBuffers[drawAssign].Buffer, 1,
                        ref copyRegion);
    }

    /// <summary>
    /// ディスクリプタセットを更新する
    /// </summary>
    /// <param name="descriptor">1つのシェーダーが使用するディスクリプタセットとUBO</param>
    /// <param name="textureIndex">テクスチャインデックス</param>
    /// <param name="isMasked"></param>
    public unsafe void UpdateDescriptorSet(Descriptor descriptor, int textureIndex, bool isMasked)
    {
        var uniformBuffer = descriptor.UniformBuffer.Buffer;
        // descriptorが更新されていない最初の1回のみ行う
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

        // テクスチャ1はキャラクターのテクスチャ、テクスチャ2はマスク用、
        // テクスチャ3は 5.3 以降の blend shader 用（s_blendTexture）。
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

        // blending 用テクスチャの初期値（モデルテクスチャ）。実際の blend 時は
        // UpdateBlendTexture() で描画先テクスチャに差し替える。
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
    /// blend shader 用テクスチャ（binding 3）を更新する。
    /// 通常は描画先レンダーターゲットのテクスチャを渡す。未使用時はモデルテクスチャで埋める。
    /// </summary>
    /// <param name="descriptor">対象ディスクリプタ</param>
    /// <param name="isMasked">マスク用セットか</param>
    /// <param name="imageView">ブレンド用テクスチャビュー</param>
    /// <param name="sampler">サンプラー</param>
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
    /// メッシュ描画を実行する
    /// </summary>
    /// <param name="model">描画対象のモデル</param>
    /// <param name="index">描画オブジェクトのインデックス</param>
    /// <param name="cmdBuffer">フレームバッファ関連のコマンドバッファ</param>
    public unsafe void ExecuteDrawForDraw(CubismModel model, int index, CommandBuffer cmdBuffer)
    {
        // パイプラインレイアウト設定用のインデックスを取得
        int blendIndex;
        int shaderIndex;
        bool masked = ClippingContextBufferForDraw != null; // この描画オブジェクトはマスク対象か
        bool invertedMask = model.GetDrawableInvertedMask(index);
        int offset = (masked ? (invertedMask ? 2 : 1) : 0) + (IsPremultipliedAlpha ? 3 : 0);

        // 5-r.5: GetDrawableBlendModeType()（CsmBlendMode）から shader を選択する。
        // 5.2 以前の AddCompatible / MultiplyCompatible は Add / Mult（Normal shader 共用）に割り当て、
        // 5.3 以降は GetShaderNamesBegin() が返す blend shader 群（494 項）から選ぶ。
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
            // クリッピング用行列の設定
            UpdateMatrix(ubo.ClipMatrix, ClippingContextBufferForDraw!.MatrixForDraw); // テクスチャ座標の変換に使用するのでy軸の向きは反転しない

            // カラーチャンネルの設定
            SetColorChannel(&ubo, ClippingContextBufferForDraw);
        }

        // MVP行列の設定
        UpdateMatrix(ubo.ProjectionMatrix, GetMvpMatrix());

        // 色定数バッファの設定
        var baseColor = GetModelColorWithOpacity(model.GetDrawableOpacity(index));
        var multiplyColor = model.GetOverrideMultiplyAndScreenColor().GetDrawableMultiplyColor(index);
        var screenColor = model.GetOverrideMultiplyAndScreenColor().GetDrawableScreenColor(index);
        SetColorUniformBuffer(&ubo, baseColor, multiplyColor, screenColor);

        // ディスクリプタにユニフォームバッファをコピー
        descriptor.UniformBuffer.MemCpy(&ubo, (ulong)Unsafe.SizeOf<ModelUBO>());

        // 頂点バッファの設定
        BindVertexAndIndexBuffers(index, cmdBuffer);

        // テクスチャインデックス取得
        int textureIndex = model.GetDrawableTextureIndex(index);

        // ディスクリプタセットのバインド
        UpdateDescriptorSet(descriptor, textureIndex, masked);

        // blend shader 用テクスチャ（binding3）: OpenGL texture2 相当。
        // 描画中 RT を直接サンプルできないため、5.3 以降の blend ではスナップショットをコピーして使う。
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

        // パイプラインのバインド
        _vk.CmdBindPipeline(cmdBuffer, PipelineBindPoint.Graphics,
                          _deviceInfo.Pipeline.GetPipeline(shaderIndex, blendIndex));

        // 5.3 以降の blend は PrepareBlendSnapshot() で RT を再開済み
        // 描画
        _vk.CmdDrawIndexed(cmdBuffer, (uint)model.GetDrawableVertexIndexCount(index), 1, 0, 0, 0);
    }

    /// <summary>
    /// マスク描画を実行する
    /// </summary>
    /// <param name="model">描画対象のモデル</param>
    /// <param name="index">描画オブジェクトのインデックス</param>
    /// <param name="cmdBuffer">フレームバッファ関連のコマンドバッファ</param>
    public unsafe void ExecuteDrawForMask(CubismModel model, int index, CommandBuffer cmdBuffer)
    {
        int shaderIndex = ShaderNames.SetupMask;
        int blendIndex = Blend.Mask;

        var descriptor = _descriptorSets[index];
        var ubo = new ModelUBO();

        // クリッピング用行列の設定
        UpdateMatrix(ubo.ClipMatrix, ClippingContextBufferForMask!.MatrixForMask);

        // カラーチャンネルの設定
        SetColorChannel(&ubo, ClippingContextBufferForMask);

        // 色定数バッファの設定
        var rect = ClippingContextBufferForMask.LayoutBounds;
        var baseColor = new CubismTextureColor(rect.X * 2.0f - 1.0f, rect.Y * 2.0f - 1.0f, rect.GetRight() * 2.0f - 1.0f, rect.GetBottom() * 2.0f - 1.0f);
        var multiplyColor = model.GetOverrideMultiplyAndScreenColor().GetDrawableMultiplyColor(index);
        var screenColor = model.GetOverrideMultiplyAndScreenColor().GetDrawableScreenColor(index);
        SetColorUniformBuffer(&ubo, baseColor, multiplyColor, screenColor);

        // ディスクリプタにユニフォームバッファをコピー
        descriptor.UniformBuffer.MemCpy(&ubo, (ulong)Unsafe.SizeOf<ModelUBO>());

        // 頂点バッファの設定
        BindVertexAndIndexBuffers(index, cmdBuffer);

        // テクスチャインデックス取得
        int textureIndex = model.GetDrawableTextureIndex(index);

        // ディスクリプタセットのバインド
        UpdateDescriptorSet(descriptor, textureIndex, false);
        _vk.CmdBindDescriptorSets(cmdBuffer, PipelineBindPoint.Graphics,
                                _deviceInfo.Pipeline.GetPipelineLayout(shaderIndex, blendIndex), 0, 1,
                                ref _descriptorSets[index].DescriptorSet, 0, null);

        // パイプラインのバインド
        _vk.CmdBindPipeline(cmdBuffer, PipelineBindPoint.Graphics,
                          _deviceInfo.Pipeline.GetPipeline(shaderIndex, blendIndex));

        // 描画
        _vk.CmdDrawIndexed(cmdBuffer, (uint)model.GetDrawableVertexIndexCount(index), 1, 0, 0, 0);
    }

    /// <summary>
    /// [オーバーライド]
    /// <para></para>
    /// 描画オブジェクト（アートメッシュ）を描画する。
    /// </summary>
    /// <param name="model">描画対象のモデル</param>
    /// <param name="index">描画メッシュのインデックス</param>
    /// <param name="commandBuffer">コマンドバッファ</param>
    /// <param name="updateCommandBuffer">更新用コマンドバッファ</param>
    public unsafe void DrawMeshVulkan(CubismModel model, int index,
                        CommandBuffer commandBuffer, CommandBuffer updateCommandBuffer)
    {
        if (Device.Handle == 0)
        {
            // デバイス未設定
            return;
        }
        if (model.GetDrawableVertexIndexCount(index) == 0)
        {
            // 描画物無し
            return;
        }
        if (model.GetDrawableOpacity(index) <= 0.0f && ClippingContextBufferForMask == null)
        {
            // 描画不要なら描画処理をスキップする
            return;
        }

        int textureIndex = model.GetDrawableTextureIndex(index);
        if (_textures[textureIndex].Sampler.Handle == 0 || _textures[textureIndex].View.Handle == 0)
        {
            return;
        }

        // 裏面描画の有効・無効
        if (IsCulling)
        {
            vkCmdSetCullModeEXT(commandBuffer, CullModeFlags.BackBit);
        }
        else
        {
            vkCmdSetCullModeEXT(commandBuffer, CullModeFlags.None);
        }

        // 頂点バッファにコピー
        CopyToBuffer(index, model.GetDrawableVertexCount(index),
                     model.GetDrawableVertices(index),
                     (float*)model.GetDrawableVertexUvs(index),
                     updateCommandBuffer);

        if (ClippingContextBufferForMask != null) // マスク生成時
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
    /// レンダリング開始
    /// </summary>
    /// <param name="drawCommandBuffer">コマンドバッファ</param>
    /// <param name="isResume">レンダリング再開かのフラグ</param>
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
            // 非モデル RT パスではこの BeginRendering がフレーム先頭なので背景をクリアする
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
    /// レンダリング終了
    /// </summary>
    /// <param name="drawCommandBuffer">コマンドバッファ</param>
    public unsafe void EndRendering(CommandBuffer drawCommandBuffer)
    {
        _vk.CmdEndRendering(drawCommandBuffer);

        // レイアウト変更
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
    /// クリッピングマスクバッファのサイズを設定する
    /// </summary>
    /// <param name="width">クリッピングマスクバッファの横幅</param>
    /// <param name="height">クリッピングマスクバッファの立幅</param>
    public void SetClippingMaskBufferSize(float width, float height)
    {
        // インスタンス破棄前にレンダーテクスチャの数を保存
        var renderTextureCount = _clippingManager.RenderTextureCount;

        //FrameBufferのサイズを変更するためにインスタンスを破棄・再作成する
        _clippingManager.Dispose();

        _clippingManager = new CubismClippingManager_Vulkan(_vk);

        _clippingManager.SetClippingMaskBufferSize(width, height);

        _clippingManager.Initialize(Model, renderTextureCount);
    }

    /// <summary>
    /// クリッピングマスクバッファのサイズを取得する
    /// </summary>
    /// <returns>クリッピングマスクバッファのサイズ</returns>
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
    /// クリッピングマネージャの Type に応じてマスクバッファを返す（drawable / offscreen の 2 系統）。
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

    private void BindVertexAndIndexBuffers(int index, CommandBuffer cmdBuffer)
    {
        var vertexBuffers = new Buffer[] { _vertexBuffers[index].Buffer };
        var offsets = new ulong[] { 0 };
        _vk.CmdBindVertexBuffers(cmdBuffer, 0, 1, vertexBuffers, offsets);
        _vk.CmdBindIndexBuffer(cmdBuffer, _indexBuffers[index].Buffer, 0, IndexType.Uint16);
    }

    public unsafe override void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        // オフスクリーンを作成していたのなら開放
        // mask を使わないモデルでは _offscreenFrameBuffers が null のため、
        // 旧実装はここで NullReferenceException になっていた（本タスク §2.4 で修正）。
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

        // モデル RT / オフスクリーン用マスク / 合成用バッファの解放（P8c-offscreen）
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

        // テクスチャは TextureInfoVulkan / LAppTextureManager が所有しており、
        // ここで Destroy すると共有キャッシュの二重解放になる。参照だけ切る
        // （官方 CubismRenderer_Vulkan のデストラクタも _textures を破棄しない）。
        _textures.Clear();

        // デバイス情報の参照を解放する。最後のレンダラが消えた時点で
        // パイプラインとオフスクリーンマネージャも解放される。
        if (_deviceInfo != null)
        {
            CubismDeviceInfo_Vulkan.ReleaseDeviceInfo(Device);
            _deviceInfo = null!;
        }
    }

    /// <summary>
    /// モデル描画直前のオフスクリーン設定（官方 BeforeDrawModelRenderTarget）。
    /// blend / offscreen 使用時はモデル RT を描画先にし、最後に Copy で swapchain へ合成する。
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
            modelRenderTarget.CreateRenderTarget(Device, PhysicalDevice,
                SwapchainExtent.Width, SwapchainExtent.Height, SwapchainImageFormat, DepthFormat);
            _isClearedModelRenderTarget = false;
        }

        // vkCmdCopyImage 合成では alpha blend できないため、モデル RT 自体を背景色でクリアする
        var bgColor = _vulkanManager.LApp.BGColor;
        modelRenderTarget.BeginDraw(_drawCommandBuffer, bgColor.R, bgColor.G, bgColor.B, bgColor.A,
            !_isClearedModelRenderTarget);
        _currentRenderTarget = modelRenderTarget;
        _currentOffscreen = null;
        _isDrawingOffscreen = false;
        _isClearedModelRenderTarget = true;
    }

    /// <summary>
    /// モデル描画後のオフスクリーン設定（官方 AfterDrawModelRenderTarget）。
    /// モデル RT を終了し、Copy シェーダで swapchain へ合成する。
    /// </summary>
    protected override void AfterDrawModelRenderTarget()
    {
        if (!_useModelRenderTarget)
        {
            return;
        }

        // 念のため未フラッシュのオフスクリーンを親へ伝搬
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
    /// 現在の描画先を終了して次のレンダーターゲットへ切り替える（官方 EnsurePreviousRenderFinished）。
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
    /// レンダーターゲット描画開始（blend/offscreen 時はモデル RT、それ以外は swapchain）。
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
    /// レンダーターゲット描画終了（blend/offscreen 時は Copy 合成）。
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
    /// レンダーターゲットの内容を Copy シェーダで現在の swapchain へ合成する。
    /// </summary>
    private unsafe void CopyRenderTarget(CubismRenderTarget_Vulkan src)
    {
        if (!src.IsValid())
        {
            return;
        }

        // P6 の glBlitFramebuffer と等価な画像コピー（Vulkan の texture barrier 代替）。
        // Copy シェーダ（ExecuteDrawForRenderTarget）はクワッド経由の代替実装として残すが、
        // モデル RT → swapchain の合成はレイアウト遷移込みの vkCmdCopyImage で行う。
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

        // swapchain を背景色でクリアしてからモデル RT をコピーする
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

        // コピー元 RT を ShaderReadOnly に戻し、次フレームの BeginDraw と整合させる
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
    /// Copy シェーダで src の内容を全画面クワッドに描画する（官方 ExecuteDrawForRenderTarget）。
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
    /// Copy 用ディスクリプタ（UBO + 合成元テクスチャ）を更新する。
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
    /// フルスクリーンクワッドの頂点/インデックスバッファをバインドする。
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
    /// オフスクリーンを追加する（官方 AddOffscreen）。
    /// </summary>
    private unsafe void AddOffscreen(int offscreenIndex)
    {
        // 越界/所有権チェック: drawable index を offscreen として使わない（t16 §5）
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
    /// オフスクリーンを親へ伝搬する（官方 SubmitDrawToParentOffscreen）。
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
    /// オフスクリーンを描画して親のレンダーターゲットへ合成する（官方 DrawOffscreen）。
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

        // 親のレンダーターゲットを再開（なければモデル RT）
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
    /// オフスクリーン合成用クワッドを描画する（官方 ExecuteDrawForOffscreen）。
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
    /// オフスクリーン合成用ディスクリプタを更新する。
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
    /// blend shader 用に現在の描画先 RT をスナップショットへコピーし、RT を再開する。
    /// Vulkan では描画中アタッチメントを同時にサンプルできないため、
    /// 直前内容を別テクスチャへコピーして binding3 にバインドする（validation 対応）。
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
            _blendSnapshot.CreateRenderTarget(Device, PhysicalDevice,
                target.GetBufferWidth(), target.GetBufferHeight(), SwapchainImageFormat, DepthFormat);
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

        // target を TransferSrc から描画用レイアウトへ戻してから再開する
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
    /// モデルを描画する実際の処理
    /// </summary>
    protected unsafe override void DoDrawModel()
    {
        //------------ クリッピングマスク・バッファ前処理方式の場合 ------------
        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo
        };
        _vk.BeginCommandBuffer(_updateCommandBuffer, ref beginInfo);
        _vk.BeginCommandBuffer(_drawCommandBuffer, ref beginInfo);

        if (_clippingManager != null)
        {
            // サイズが違う場合はここで作成しなおし
            for (int i = 0; i < _clippingManager.RenderTextureCount; ++i)
            {
                if (_offscreenFrameBuffers![i].BufferWidth != _clippingManager.ClippingMaskBufferSize.X
                    || _offscreenFrameBuffers[i].BufferHeight != _clippingManager.ClippingMaskBufferSize.Y)
                {
                    _offscreenFrameBuffers[i].DestroyRenderTarget();
                    _offscreenFrameBuffers[i].CreateRenderTarget(
                        Device, PhysicalDevice,
                        (uint)_clippingManager.ClippingMaskBufferSize.X,
                        (uint)_clippingManager.ClippingMaskBufferSize.Y,
                        ImageFormat, DepthFormat
                    );
                }
            }
            if (UseHighPrecisionMask)
            {
                _clippingManager.SetupMatrixForHighPrecision(Model, false);
            }
            else
            {
                _clippingManager.SetupClippingContext(Model, _drawCommandBuffer, _updateCommandBuffer, this);
            }
        }

        //------------ オフスクリーン用クリッピングマスク前処理（P8c-offscreen）------------
        if (_offscreenClippingManager != null)
        {
            for (int i = 0; i < _offscreenClippingManager.RenderTextureCount; ++i)
            {
                if (_offscreenMaskBuffers[i].BufferWidth != _offscreenClippingManager.ClippingMaskBufferSize.X
                    || _offscreenMaskBuffers[i].BufferHeight != _offscreenClippingManager.ClippingMaskBufferSize.Y)
                {
                    _offscreenMaskBuffers[i].DestroyRenderTarget();
                    _offscreenMaskBuffers[i].CreateRenderTarget(
                        Device, PhysicalDevice,
                        (uint)_offscreenClippingManager.ClippingMaskBufferSize.X,
                        (uint)_offscreenClippingManager.ClippingMaskBufferSize.Y,
                        ImageFormat, DepthFormat
                    );
                }
            }

            if (UseHighPrecisionMask)
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

        // スワップチェーンを再作成した際に深度バッファのサイズを更新する
        if (_depthImage.Width != SwapchainExtent.Width || _depthImage.Height != SwapchainExtent.Height)
        {
            _depthImage.Destroy(Device);
            CreateDepthBuffer();
        }

        var drawableCount = Model.GetDrawableCount();
        var offscreenCount = Model.GetOffscreenCount();
        var totalCount = drawableCount + offscreenCount;
        var renderOrder = Model.GetRenderOrders();
        // インデックスを描画順でソート
        // G1 門禁（tester 実測）: renderOrder の配列長は totalCount。drawable だけで走査すると
        // offscreen の順序（および order 値が drawableCount 以上になる場合）を壊す。
        // 官方 DrawObjectLoop（CubismRenderer_Vulkan.cpp:2714-2727）と同じ二重ソートにする。
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

        //描画
        _vk.BeginCommandBuffer(_updateCommandBuffer, &beginInfo);
        _vk.BeginCommandBuffer(_drawCommandBuffer, &beginInfo);
        BeginRenderTarget(false);

        for (int i = 0; i < totalCount; ++i)
        {
            // offscreen の実描画（AddOffscreen / blend）は P8c の担当。
            // G1 門禁の順序配列だけ先に用意し、本フェーズは drawable のみ描画する（R1 语义維持）。
            if (_sortedObjectsTypeList[i] == CsmEnum.DrawableObjectType.Offscreen)
            {
                // インデックスは AddOffscreen 内で offscreen 空間として検証する
                AddOffscreen(_sortedObjectsIndexList[i]);
                continue;
            }

            int drawableIndex = _sortedObjectsIndexList[i];
            // Drawableが表示状態でなければ処理をパスする
            if (!Model.GetDrawableDynamicFlagIsVisible(drawableIndex))
            {
                continue;
            }

            // 現在のオフスクリーンの描画対象外なら、親オフスクリーンへ伝搬する
            SubmitDrawToParentOffscreen(drawableIndex, CsmEnum.DrawableObjectType.Drawable);

            // クリッピングマスクをセットする
            var clipContext = (_clippingManager != null) ? _clippingManager.ClippingContextListForDraw[drawableIndex]
                as CubismClippingContext_Vulkan : null;

            if (clipContext != null && UseHighPrecisionMask) // マスクを書く必要がある
            {
                if (clipContext.IsUsing) // 書くことになっていた
                {
                    // 一旦オフスクリーン描画に移る（blend/offscreen 時はモデル RT を終了）
                    EnsurePreviousRenderFinished(_offscreenFrameBuffers![clipContext.BufferIndex]);

                    // 描画順を考慮して今までに積んだコマンドを実行する
                    SubmitCommand(_updateCommandBuffer, _updateFinishedSemaphore);
                    SubmitCommand(_drawCommandBuffer, null, _updateFinishedSemaphore);
                    _vk.BeginCommandBuffer(_updateCommandBuffer, &beginInfo);
                    _vk.BeginCommandBuffer(_drawCommandBuffer, &beginInfo);

                    var currentHighPrecisionMaskColorBuffer = _offscreenFrameBuffers![clipContext.BufferIndex];
                    currentHighPrecisionMaskColorBuffer.BeginDraw(_drawCommandBuffer, 1.0f, 1.0f, 1.0f, 1.0f);

                    // 生成したFrameBufferと同じサイズでビューポートを設定
                    var viewport1 = GetViewport(_clippingManager!.ClippingMaskBufferSize.X, _clippingManager.ClippingMaskBufferSize.Y,
                        0.0f, 1.0f);
                    _vk.CmdSetViewport(_drawCommandBuffer, 0, 1, ref viewport1);

                    var rect1 = GetScissor(0.0f, 0.0f, _clippingManager.ClippingMaskBufferSize.X, _clippingManager.ClippingMaskBufferSize.Y);
                    _vk.CmdSetScissor(_drawCommandBuffer, 0, 1, ref rect1);

                    int clipDrawCount = clipContext.ClippingIdCount;
                    for (int ctx = 0; ctx < clipDrawCount; ctx++)
                    {
                        int clipDrawIndex = clipContext.ClippingIdList[ctx];

                        // 頂点情報が更新されておらず、信頼性がない場合は描画をパスする
                        if (!Model.GetDrawableDynamicFlagVertexPositionsDidChange(clipDrawIndex))
                        {
                            continue;
                        }

                        IsCulling = Model.GetDrawableCulling(clipDrawIndex) != 0;

                        ClippingContextBufferForMask = clipContext;
                        DrawMeshVulkan(Model, clipDrawIndex, _drawCommandBuffer, _updateCommandBuffer);
                    }
                    // --- 後処理 ---
                    currentHighPrecisionMaskColorBuffer.EndDraw(_drawCommandBuffer); // オフスクリーン描画終了
                    ClippingContextBufferForMask = null;
                    SubmitCommand(_updateCommandBuffer, _updateFinishedSemaphore);
                    SubmitCommand(_drawCommandBuffer, null, _updateFinishedSemaphore);
                    _vk.BeginCommandBuffer(_updateCommandBuffer, &beginInfo);
                    _vk.BeginCommandBuffer(_drawCommandBuffer, &beginInfo);

                    // 描画再開
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

            // ビューポートを設定する
            var viewport = GetViewport(SwapchainExtent.Width, SwapchainExtent.Height, 0.0f, 1.0f);
            _vk.CmdSetViewport(_drawCommandBuffer, 0, 1, &viewport);

            var rect = GetScissor(0.0f, 0.0f, SwapchainExtent.Width, SwapchainExtent.Height);
            _vk.CmdSetScissor(_drawCommandBuffer, 0, 1, &rect);

            // クリッピングマスクをセットする
            ClippingContextBufferForDraw = clipContext;
            IsCulling = Model.GetDrawableCulling(drawableIndex) != 0;
            DrawMeshVulkan(Model, drawableIndex, _drawCommandBuffer, _updateCommandBuffer);
        }

        // 残っているオフスクリーンを親へ伝搬してから合成
        while (_currentOffscreen != null)
        {
            DrawOffscreen(_currentOffscreen);
        }

        EndRenderTarget();
        SubmitCommand(_updateCommandBuffer, _updateFinishedSemaphore);
        SubmitCommand(_drawCommandBuffer, null, _updateFinishedSemaphore);
    }

    /// <summary>
    /// モデル描画直前のステートを保持する
    /// </summary>
    protected override void SaveProfile()
    {

    }

    /// <summary>
    /// モデル描画直前のステートを保持する
    /// </summary>
    protected override void RestoreProfile()
    {

    }
}
