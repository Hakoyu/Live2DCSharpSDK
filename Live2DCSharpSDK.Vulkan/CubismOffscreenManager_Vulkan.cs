using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// 管理离屏用渲染目标的类
/// (对应官方 Framework/Rendering/Vulkan/CubismOffscreenManager_Vulkan.hpp:19-56).
/// 实际资源管理由 ICubismOffscreenManager&lt;T&gt; 负责.
/// </summary>
public class CubismOffscreenManager_Vulkan : ICubismOffscreenManager<CubismRenderTarget_Vulkan>
{
    /// <summary>
    /// 获取可用的渲染目标 (对应官方 GetOffscreenRenderTarget)
    /// </summary>
    /// <param name="device">逻辑设备</param>
    /// <param name="physicalDevice">物理设备</param>
    /// <param name="displayBufferWidth">离屏的宽度</param>
    /// <param name="displayBufferHeight">离屏的高度</param>
    /// <param name="surfaceFormat">surface 格式</param>
    /// <param name="depthFormat">深度格式</param>
    /// <returns>可用的渲染目标</returns>
    public CubismRenderTarget_Vulkan GetOffscreenRenderTarget(
        Device device, PhysicalDevice physicalDevice,
        uint displayBufferWidth, uint displayBufferHeight,
        Format surfaceFormat, Format depthFormat)
    {
        // 更新使用数量
        UpdateRenderTargetCount();

        // 若存在未被使用的资源容器则直接返回
        CubismRenderTarget_Vulkan? offscreenRenderTarget = GetUnusedOffscreenRenderTarget();
        if (offscreenRenderTarget != null)
        {
            // 尺寸不同时重新创建
            if (offscreenRenderTarget.GetBufferWidth() != displayBufferWidth ||
                offscreenRenderTarget.GetBufferHeight() != displayBufferHeight)
            {
                offscreenRenderTarget.DestroyRenderTarget();
                if (offscreenRenderTarget.CreateRenderTarget(device, physicalDevice,
                    displayBufferWidth, displayBufferHeight, surfaceFormat, depthFormat) is false)
                {
                    // 降级: 返回无效目标, 由调用方通过 IsValid() 判断并跳过绘制, 下次调用会重新尝试创建.
                    CubismLog.Warning("[Live2D Vulkan]recreate offscreen render target failed ({0}x{1}).",
                        displayBufferWidth, displayBufferHeight);
                }
            }
            // 返回已有的未使用渲染目标
            return offscreenRenderTarget;
        }

        // 新建渲染目标并登记
        offscreenRenderTarget = CreateOffscreenRenderTarget();
        if (offscreenRenderTarget.CreateRenderTarget(device, physicalDevice,
            displayBufferWidth, displayBufferHeight, surfaceFormat, depthFormat) is false)
        {
            // 降级: 返回无效目标, 由调用方通过 IsValid() 判断并跳过绘制.
            CubismLog.Warning("[Live2D Vulkan]create offscreen render target failed ({0}x{1}).",
                displayBufferWidth, displayBufferHeight);
        }

        return offscreenRenderTarget;
    }
}
