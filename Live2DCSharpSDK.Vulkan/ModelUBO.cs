using System.Runtime.InteropServices;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// 保持模型用 uniform buffer 对象内容的结构体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ModelUBO
{
    /// <summary>
    /// 传递给 shader program 的数据(ProjectionMatrix)
    /// </summary>
    public fixed float ProjectionMatrix[16];

    /// <summary>
    /// 传递给 shader program 的数据(ClipMatrix)
    /// </summary>
    public fixed float ClipMatrix[16];

    /// <summary>
    /// 传递给 shader program 的数据(BaseColor)
    /// </summary>
    public fixed float BaseColor[4];

    /// <summary>
    /// 传递给 shader program 的数据(MultiplyColor)
    /// </summary>
    public fixed float MultiplyColor[4];

    /// <summary>
    /// 传递给 shader program 的数据(ScreenColor)
    /// </summary>
    public fixed float ScreenColor[4];

    /// <summary>
    /// 传递给 shader program 的数据(ChannelFlag)
    /// </summary>
    public fixed float ChannelFlag[4];
}
