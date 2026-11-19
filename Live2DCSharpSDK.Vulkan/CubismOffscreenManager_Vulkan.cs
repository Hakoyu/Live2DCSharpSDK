using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// オフスクリーン用のレンダーターゲットを管理するクラス
/// （官方 FW5/Vulkan/CubismOffscreenManager_Vulkan.hpp:19-56）。
/// 実際のリソース管理は P5 の ICubismOffscreenManager&lt;T&gt; が持つ。
/// </summary>
public class CubismOffscreenManager_Vulkan : ICubismOffscreenManager<CubismRenderTarget_Vulkan>
{
    /// <summary>
    /// 使用可能なレンダーターゲットの取得（官方 GetOffscreenRenderTarget）
    /// </summary>
    /// <param name="device">論理デバイス</param>
    /// <param name="physicalDevice">物理デバイス</param>
    /// <param name="displayBufferWidth">オフスクリーンの横幅</param>
    /// <param name="displayBufferHeight">オフスクリーンの縦幅</param>
    /// <param name="surfaceFormat">サーフェスフォーマット</param>
    /// <param name="depthFormat">深度フォーマット</param>
    /// <returns>使用可能なレンダーターゲット</returns>
    public CubismRenderTarget_Vulkan GetOffscreenRenderTarget(
        Device device, PhysicalDevice physicalDevice,
        uint displayBufferWidth, uint displayBufferHeight,
        Format surfaceFormat, Format depthFormat)
    {
        // 使用数を更新
        UpdateRenderTargetCount();

        // 使われていないリソースコンテナがあればそれを返す
        CubismRenderTarget_Vulkan? offscreenRenderTarget = GetUnusedOffscreenRenderTarget();
        if (offscreenRenderTarget != null)
        {
            // サイズが違う場合は再作成する
            if (offscreenRenderTarget.GetBufferWidth() != displayBufferWidth ||
                offscreenRenderTarget.GetBufferHeight() != displayBufferHeight)
            {
                offscreenRenderTarget.DestroyRenderTarget();
                offscreenRenderTarget.CreateRenderTarget(device, physicalDevice,
                    displayBufferWidth, displayBufferHeight, surfaceFormat, depthFormat);
            }
            // 既存の未使用レンダーターゲットを返す
            return offscreenRenderTarget;
        }

        // 新規にレンダーターゲットを作成して登録する
        offscreenRenderTarget = CreateOffscreenRenderTarget();
        offscreenRenderTarget.CreateRenderTarget(device, physicalDevice,
            displayBufferWidth, displayBufferHeight, surfaceFormat, depthFormat);

        return offscreenRenderTarget;
    }
}
