using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// オフスクリーン用のレンダーターゲットを管理するクラス
/// （官方 FW5/Vulkan/CubismOffscreenRenderTarget_Vulkan.hpp:15-72）。
/// CRTP は P5 の自参照ジェネリック制約（PLAN D-07）をそのまま使う。
/// </summary>
public class CubismOffscreenRenderTarget_Vulkan
    : ICubismOffscreenRenderTarget<CubismOffscreenRenderTarget_Vulkan, CubismRenderTarget_Vulkan>
{
    /// <summary>
    /// オフスクリーン描画用レンダーターゲットを設定する（官方 SetOffscreenRenderTarget）
    /// </summary>
    public void SetOffscreenRenderTarget(
        Device device, PhysicalDevice physicalDevice,
        CubismOffscreenManager_Vulkan offscreenManager,
        uint displayBufferWidth, uint displayBufferHeight,
        Format surfaceFormat, Format depthFormat)
    {
        if (_renderTarget != null && GetUsingRenderTextureState(offscreenManager))
        {
            if (_renderTarget.GetBufferWidth() != displayBufferWidth ||
                _renderTarget.GetBufferHeight() != displayBufferHeight)
            {
                _renderTarget.DestroyRenderTarget();
                _renderTarget.CreateRenderTarget(device, physicalDevice,
                    displayBufferWidth, displayBufferHeight, surfaceFormat, depthFormat);
            }
            return;
        }

        _renderTarget = offscreenManager.GetOffscreenRenderTarget(
            device, physicalDevice, displayBufferWidth, displayBufferHeight, surfaceFormat, depthFormat);
    }

    /// <summary>
    /// レンダーターゲットの使用状態を取得する（官方 GetUsingRenderTextureState）
    /// </summary>
    public bool GetUsingRenderTextureState(CubismOffscreenManager_Vulkan offscreenManager)
    {
        return _renderTarget != null && offscreenManager.GetUsingRenderTextureState(_renderTarget);
    }

    /// <summary>
    /// オフスクリーン描画用レンダーターゲットの使用を終了する（官方 StopUsingRenderTexture）
    /// </summary>
    public void StopUsingRenderTexture(CubismOffscreenManager_Vulkan offscreenManager)
    {
        if (_renderTarget != null)
        {
            offscreenManager.StopUsingRenderTexture(_renderTarget);
            _renderTarget = null;
        }
    }
}
