using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// 管理绑定到设备的数据的类
/// (对应官方 Framework/Rendering/Vulkan/CubismDeviceInfo_Vulkan.hpp:19-70).
/// 以 VkDevice.Handle 为键, 保持每个设备的 pipeline 与离屏管理器.
/// </summary>
public class CubismDeviceInfo_Vulkan
{
    /// <summary>
    /// 每个设备的数据(官方 s_deviceInfoList: csmMap&lt;VkDevice, CubismDeviceInfo_Vulkan&gt;)
    /// </summary>
    private static readonly Dictionary<nint, CubismDeviceInfo_Vulkan> DeviceInfoList = [];

    /// <summary>
    /// 为了让多个渲染器共享同一设备而引入的引用计数.
    /// 官方在渲染器销毁时不释放, 而 C# 侧配合 IDisposable
    /// 最后一次引用释放时销毁.
    /// </summary>
    private int _referenceCount;

    private readonly CubismPipeline_Vulkan _pipeline;

    private readonly CubismOffscreenManager_Vulkan _offscreenManager = new();

    private CubismDeviceInfo_Vulkan(Device device)
    {
        _pipeline = new(Vk.GetApi(), device);
    }

    /// <summary>
    /// 获取绑定到设备的数据(对应官方 GetDeviceInfo)
    /// </summary>
    /// <param name="device">使用的设备</param>
    /// <returns>绑定到设备的数据</returns>
    public static CubismDeviceInfo_Vulkan GetDeviceInfo(Device device)
    {
        nint key = device.Handle;

        if (!DeviceInfoList.TryGetValue(key, out CubismDeviceInfo_Vulkan? info))
        {
            info = new CubismDeviceInfo_Vulkan(device);
            DeviceInfoList[key] = info;
        }

        ++info._referenceCount;
        return info;
    }

    /// <summary>
    /// 释放绑定到设备的数据的使用(对应官方 ReleaseDeviceInfo).
    /// 引用计数变为 0 时释放 pipeline 与离屏资源.
    /// </summary>
    /// <param name="device">使用的设备</param>
    public static void ReleaseDeviceInfo(Device device)
    {
        nint key = device.Handle;

        if (!DeviceInfoList.TryGetValue(key, out CubismDeviceInfo_Vulkan? info))
        {
            return;
        }

        if (info._referenceCount > 0)
        {
            --info._referenceCount;
        }

        if (info._referenceCount > 0)
        {
            return;
        }

        DeviceInfoList.Remove(key);
        info.Release();
    }

    /// <summary>
    /// 释放所有绑定到设备的数据的使用(对应官方 ReleaseAllDeviceInfo)
    /// </summary>
    public static void ReleaseAllDeviceInfo()
    {
        foreach (CubismDeviceInfo_Vulkan info in DeviceInfoList.Values)
        {
            info.Release();
        }

        DeviceInfoList.Clear();
    }

    /// <summary>
    /// 获取 pipeline(对应官方 GetPipeline)
    /// </summary>
    public CubismPipeline_Vulkan Pipeline => _pipeline;

    /// <summary>
    /// 获取离屏管理器(对应官方 GetOffscreenManager)
    /// </summary>
    public CubismOffscreenManager_Vulkan OffscreenManager => _offscreenManager;

    /// <summary>
    /// 释放持有的资源
    /// </summary>
    private void Release()
    {
        _referenceCount = 0;
        _pipeline.Dispose();
        _offscreenManager.Dispose();
    }
}
