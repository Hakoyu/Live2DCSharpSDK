using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

public class SwapchainSupportDetails
{
    /// <summary>
    /// 基本功能
    /// </summary>
    public SurfaceCapabilitiesKHR Capabilities;
    /// <summary>
    /// 可用的格式
    /// </summary>
    public SurfaceFormatKHR[] Formats;
    /// <summary>
    /// 可用的显示模式
    /// </summary>
    public PresentModeKHR[] PresentModes;
}
