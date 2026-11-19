using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

public class LAppDelegateVulkan : LAppDelegate
{
    /// <summary>
    /// モデルごとのマスクバッファ枚数の上限（4 枚 = 128 グループまで、OpenGL 側と同一）
    /// </summary>
    private const int MaxMaskBufferCount = 4;

    public readonly VulkanApi _api;
    public readonly Vk _vk;
    public readonly VulkanManager VulkanManager;

    public LAppDelegateVulkan(VulkanApi api)
    {
        _api = api;
        _vk = Vk.GetApi();

        VulkanManager = new VulkanManager(this, _vk, _api);
        VulkanManager.Initialize();

        var swapchainManager = VulkanManager.SwapchainManager;
        // レンダラにvulkanManagerの変数を渡す
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
        // 【官方との差異】官方 sample は maskBufferCount = 1 固定だが、本地はモデルの
        // 実際のマスクグループ数から必要枚数を決める（大肥魚: 49 グループ > 1 枚あたり上限 36 の対策）。
        int maskBufferCount = CalculateMaskBufferCount(model);
        return new CubismRenderer_Vulkan(_vk, VulkanManager, model, maskBufferCount);
    }

    /// <summary>
    /// モデルのマスクグループ数から必要な maskBufferCount を算出する（OpenGL 側と同一規則）。
    /// 1 枚あたり 36 グループ、2 枚以上は 1 枚あたり 32 グループ（官方の上限式）。
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
        // swapchain 再作成後の extent も毎フレーム同期する（古い extent でモデル RT を作ると
        // ウィンドウとの解像度不一致で描画が右下にずれる）。
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

            // AppViewの初期化（R5: LAppView.Initialize(int width, int height)）
            View.Initialize(width, height);
            // サイズを保存しておく
            WindowWidth = width;
            WindowHeight = height;
            VulkanManager.IsSwapchainInvalid = false;
            return true;
        }
        return false;
    }
}
