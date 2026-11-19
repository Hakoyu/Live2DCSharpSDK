using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

public class LAppDelegateVulkan : LAppDelegate
{
    /// <summary>
    /// 每个模型的 mask buffer 张数上限(4 张 = 最多 128 组, 与 OpenGL 侧一致)
    /// </summary>
    private const int MaxMaskBufferCount = 4;

    public readonly VulkanApi _api;
    public readonly Vk _vk;
    public readonly VulkanManager VulkanManager;

    /// <summary>
    /// 是否已完成关停, 防止重复释放
    /// </summary>
    private bool _shutdown;

    public LAppDelegateVulkan(VulkanApi api)
    {
        _api = api;
        _vk = Vk.GetApi();

        VulkanManager = new VulkanManager(this, _vk, _api);
        VulkanManager.Initialize();

        var swapchainManager = VulkanManager.SwapchainManager;
        // 向渲染器传递 vulkanManager 的变量
        CubismRenderer_Vulkan.SetConstantSettings(
            VulkanManager.Device, VulkanManager.PhysicalDevice,
            VulkanManager.CommandPool, VulkanManager.GraphicQueue,
            swapchainManager.Extent, swapchainManager.SwapchainFormat,
            VulkanManager.SurfaceFormat,
            VulkanManager.GetSwapchainImage(), VulkanManager.GetSwapchainImageView(),
            VulkanManager.DepthFormat
        );

        View = new LAppViewVulkan(this);
        InitApp();
    }

    public new void Resize()
    {
        VulkanManager.FramebufferResized = true;
        base.Resize();
    }

    public override CubismRenderer CreateRenderer(CubismModel model)
    {
        // [与官方的差异] 官方 sample 固定 maskBufferCount = 1, 而本地根据模型的
        // 实际遮罩组数量决定所需张数(大肥魚: 49 组 > 每张上限 36 的应对).
        int maskBufferCount = CalculateMaskBufferCount(model);
        return new CubismRenderer_Vulkan(_vk, VulkanManager, model, maskBufferCount);
    }

    /// <summary>
    /// 根据模型的遮罩组数量算出所需的 maskBufferCount(与 OpenGL 侧同一规则).
    /// 单张 36 组, 两张及以上时每张 32 组(官方的上限公式).
    /// </summary>
    private static int CalculateMaskBufferCount(CubismModel model)
    {
        if (!model.IsUsingMasking())
        {
            return 1;
        }

        int groups = CubismMaskGroupUtility.CountDrawableMaskGroups(model);
        int maskBufferCount = 1;

        if (groups > CubismClippingManager.ClippingMaskMaxCountOnDefault)
        {
            maskBufferCount = (groups + CubismClippingManager.ClippingMaskMaxCountOnMultiRenderTexture - 1)
                / CubismClippingManager.ClippingMaskMaxCountOnMultiRenderTexture;

            if (maskBufferCount > MaxMaskBufferCount)
            {
                CubismLog.Warning("[Live2D Vulkan]mask groups ({0}) exceed the supported limit of {1} render textures; fallback to {1}.",
                    groups, MaxMaskBufferCount);
                maskBufferCount = MaxMaskBufferCount;
            }
        }

        CubismLog.Info("[Live2D Vulkan]maskBufferCount={0} maskGroups={1} drawables={2} offscreens={3}",
            maskBufferCount, groups, model.GetDrawableCount(), model.GetOffscreenCount());

        return maskBufferCount;
    }

    public override TextureInfo CreateTexture(LAppModel model, int index, int width, int height, nint data)
    {
        _vk.DeviceWaitIdle(VulkanManager.Device);

        return new TextureInfoVulkan(_vk, this, model, VulkanManager.SurfaceFormat, ImageTiling.Optimal,
                   ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
                    (uint)width, (uint)height, data);
    }

    public override void GetWindowSize(out int width, out int height)
    {
        _api.GetWindowSize(out width, out height);
    }

    public override void OnUpdatePre()
    {
        // 每帧同步 swapchain 重建后的 extent(用旧的 extent 创建模型 RT 会
        // 因与窗口分辨率不一致而导致绘制偏向右下).
        CubismRenderer_Vulkan.UpdateSwapchainVariable(
            VulkanManager.SwapchainManager.Extent,
            VulkanManager.GetSwapchainImage(),
            VulkanManager.GetSwapchainImageView());
    }

    public override void RunPost()
    {
        VulkanManager.PostDraw();
        RecreateSwapchain();
    }

    public override bool RunPre()
    {
        VulkanManager.UpdateDrawFrame();

        return RecreateSwapchain();
    }

    /// <summary>
    /// 按序关停宿主: 等待 GPU 空闲 -> 释放模型/渲染器/纹理 -> 释放 Vulkan 设备与实例.
    /// 与 OpenGL 宿主的释放顺序一致, 重复调用安全.
    /// </summary>
    public void Shutdown()
    {
        if (_shutdown)
        {
            return;
        }

        _shutdown = true;
        CubismLog.Debug("[Live2D Vulkan]shutdown: dispose models/renderers, then device/instance.");

        // 1) 等待 GPU 空闲, 避免销毁仍在使用的命令缓冲与交换链资源.
        if (VulkanManager.Device.Handle != 0)
        {
            _vk.DeviceWaitIdle(VulkanManager.Device);
        }

        // 2) 释放模型: 依次释放纹理(TextureInfoVulkan)与渲染器(CubismRenderer_Vulkan),
        //    渲染器释放时会把 CubismDeviceInfo 的引用计数减到 0.
        base.Dispose();

        // 3) 释放设备侧资源: 交换链 -> 命令池 -> 管线/离屏(ReleaseAllDeviceInfo 作为兜底)
        //    -> 设备 -> surface -> instance.
        VulkanManager.Dispose();
        CubismLog.Debug("[Live2D Vulkan]shutdown: done.");
    }

    private bool RecreateSwapchain()
    {
        if (VulkanManager.IsSwapchainInvalid)
        {
            GetWindowSize(out var width, out var height);
            if (width == 0 || height == 0)
            {
                return true;
            }

            VulkanManager.RecreateSwapchain();
            CubismRenderer_Vulkan.UpdateSwapchainVariable(
                VulkanManager.SwapchainManager.Extent,
                VulkanManager.GetSwapchainImage(),
                VulkanManager.GetSwapchainImageView()
            );

            // 初始化 AppView(R5: LAppView.Initialize(int width, int height))
            View.Initialize(width, height);
            // 保存尺寸
            WindowWidth = width;
            WindowHeight = height;
            VulkanManager.IsSwapchainInvalid = false;
            return true;
        }
        return false;
    }
}
