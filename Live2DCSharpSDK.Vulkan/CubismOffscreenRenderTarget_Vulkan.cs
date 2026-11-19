using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// 管理离屏用渲染目标的类
/// (对应官方 Framework/Rendering/Vulkan/CubismOffscreenRenderTarget_Vulkan.hpp:15-72).
/// CRTP 直接沿用自引用泛型约束的做法.
/// </summary>
public class CubismOffscreenRenderTarget_Vulkan
    : ICubismOffscreenRenderTarget<CubismOffscreenRenderTarget_Vulkan, CubismRenderTarget_Vulkan>
{
    /// <summary>
    /// 设置离屏绘制用的渲染目标 (对应官方 SetOffscreenRenderTarget)
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
                if (_renderTarget.CreateRenderTarget(device, physicalDevice,
                    displayBufferWidth, displayBufferHeight, surfaceFormat, depthFormat) is false)
                {
                    CubismLog.Warning("[Live2D Vulkan]recreate offscreen render target failed ({0}x{1}).",
                        displayBufferWidth, displayBufferHeight);
                }
            }
            return;
        }

        _renderTarget = offscreenManager.GetOffscreenRenderTarget(
            device, physicalDevice, displayBufferWidth, displayBufferHeight, surfaceFormat, depthFormat);
    }

    /// <summary>
    /// 获取渲染目标的使用状态 (对应官方 GetUsingRenderTextureState)
    /// </summary>
    public bool GetUsingRenderTextureState(CubismOffscreenManager_Vulkan offscreenManager)
    {
        return _renderTarget != null && offscreenManager.GetUsingRenderTextureState(_renderTarget);
    }

    /// <summary>
    /// 结束离屏绘制用渲染目标的使用 (对应官方 StopUsingRenderTexture)
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
