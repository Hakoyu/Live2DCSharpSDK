using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// デバイスに紐づいているデータを管理するクラス
/// （官方 FW5/Vulkan/CubismDeviceInfo_Vulkan.hpp:19-70）。
/// VkDevice.Handle をキーに、デバイスごとのパイプラインとオフスクリーンマネージャを保持する。
/// </summary>
public class CubismDeviceInfo_Vulkan
{
    /// <summary>
    /// デバイスごとのデータ（官方 s_deviceInfoList: csmMap&lt;VkDevice, CubismDeviceInfo_Vulkan&gt;）
    /// </summary>
    private static readonly Dictionary<nint, CubismDeviceInfo_Vulkan> DeviceInfoList = [];

    /// <summary>
    /// 同一デバイスを複数レンダラが共有できるようにするための参照カウント。
    /// 官方はレンダラ破棄時に解放しないが、C# 側は IDisposable に合わせて
    /// 最後の参照が外れた時点で解放する（P8b の裁定、報告 §2.2）。
    /// </summary>
    private int _referenceCount;

    private readonly CubismPipeline_Vulkan _pipeline;

    private readonly CubismOffscreenManager_Vulkan _offscreenManager = new();

    private CubismDeviceInfo_Vulkan(Device device)
    {
        _pipeline = new(Vk.GetApi(), device);
    }

    /// <summary>
    /// デバイスに紐づいているデータを取得する（官方 GetDeviceInfo）
    /// </summary>
    /// <param name="device">使用デバイス</param>
    /// <returns>デバイスに紐づいているデータ</returns>
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
    /// デバイスに紐づいているデータの使用を開放する（官方 ReleaseDeviceInfo）。
    /// 参照カウントが 0 になった時点でパイプライン／オフスクリーンを解放する。
    /// </summary>
    /// <param name="device">使用デバイス</param>
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
    /// デバイスに紐づいているデータの使用を全て開放する（官方 ReleaseAllDeviceInfo）
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
    /// パイプラインを取得する（官方 GetPipeline）
    /// </summary>
    public CubismPipeline_Vulkan Pipeline => _pipeline;

    /// <summary>
    /// オフスクリーンマネージャーを取得する（官方 GetOffscreenManager）
    /// </summary>
    public CubismOffscreenManager_Vulkan OffscreenManager => _offscreenManager;

    /// <summary>
    /// 保持リソースを解放する
    /// </summary>
    private void Release()
    {
        _referenceCount = 0;
        _pipeline.Dispose();
        _offscreenManager.Dispose();
    }
}
