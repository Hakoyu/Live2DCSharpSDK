using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// 保存 descriptor set 与 UBO 的结构体. 在使用命令缓冲区统一绘制时, 不得在绑定 descriptor set 之后更新它.
/// <para></para>
/// 只有被遮罩的绘制对象需要把 framebuffer 设置到 descriptor set 中, 此时为避免更新已绑定的 descriptor set, 需另外准备一份供被遮罩对象使用的 descriptor set.
/// </summary>
public class Descriptor
{
    /// <summary>
    /// uniform buffer
    /// </summary>
    public CubismBufferVulkan UniformBuffer;
    /// <summary>
    /// descriptor set
    /// </summary>
    public DescriptorSet DescriptorSet;
    /// <summary>
    /// descriptor set 是否已更新
    /// </summary>
    public bool IsDescriptorSetUpdated;
    /// <summary>
    /// 供被遮罩绘制对象使用的 descriptor set
    /// </summary>
    public DescriptorSet DescriptorSetMasked;
    /// <summary>
    /// 供被遮罩绘制对象使用的 descriptor set 是否已更新
    /// </summary>
    public bool IsDescriptorSetMaskedUpdated;
}
