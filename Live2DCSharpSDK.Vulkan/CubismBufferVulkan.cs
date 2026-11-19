using Live2DCSharpSDK.Framework;
using Silk.NET.Vulkan;

using Buffer = Silk.NET.Vulkan.Buffer;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// 处理 buffer 的类
/// </summary>
/// <param name="vk"></param>
public class CubismBufferVulkan(Vk vk)
{
    //VkDeviceSize ulong

    /// <summary>
    /// buffer
    /// </summary>
    public Buffer Buffer => _buffer;

    private Buffer _buffer;

    /// <summary>
    /// 内存
    /// </summary>
    private DeviceMemory _memory;
    /// <summary>
    /// 映射区域的地址
    /// </summary>
    private unsafe void* _mapped;

    /// <summary>
    /// 查找物理设备内存类型的索引
    /// </summary>
    /// <param name="physicalDevice">物理设备</param>
    /// <param name="typeFilter">存在内存类型时被设置的位掩码</param>
    /// <param name="properties">内存访问设备时的类型</param>
    /// <returns></returns>
    public unsafe int FindMemoryType(PhysicalDevice physicalDevice, uint typeFilter, MemoryPropertyFlags properties)
    {
        vk.GetPhysicalDeviceMemoryProperties(physicalDevice, out var memProperties);

        for (int i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << i)) != 0
                && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                return i;
            }
        }
        CubismLog.Error("[Live2D Vulkan]failed to find suitable memory type!");
        return 0;
    }

    /// <summary>
    /// 创建 buffer
    /// </summary>
    /// <param name="device">设备</param>
    /// <param name="physicalDevice">物理设备</param>
    /// <param name="size">buffer 大小</param>
    /// <param name="usage">指定 buffer 用途的位掩码</param>
    /// <param name="properties">内存访问设备时的类型</param>
    public unsafe void CreateBuffer(Device device, PhysicalDevice physicalDevice, ulong size, BufferUsageFlags usage,
                      MemoryPropertyFlags properties)
    {
        var bufferInfo = new BufferCreateInfo()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        if (vk.CreateBuffer(device, ref bufferInfo, null, out _buffer) != Result.Success)
        {
            CubismLog.Error("[Live2D Vulkan]failed to create buffer!");
        }

        vk.GetBufferMemoryRequirements(device, _buffer, out var memRequirements);

        var allocInfo = new MemoryAllocateInfo()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = (uint)FindMemoryType(physicalDevice, memRequirements.MemoryTypeBits, properties)
        };
        if (vk.AllocateMemory(device, ref allocInfo, null, out _memory) != Result.Success)
        {
            CubismLog.Error("[Live2D Vulkan]failed to allocate buffer memory!");
        }
        vk.BindBufferMemory(device, _buffer, _memory, 0);
    }

    /// <summary>
    /// 把内存映射到地址空间, 并取得该地址指针
    /// </summary>
    /// <param name="device">设备</param>
    /// <param name="size">映射的大小</param>
    public unsafe void Map(Device device, ulong size)
    {
        vk.MapMemory(device, _memory, 0, size, 0, ref _mapped);
    }

    /// <summary>
    /// 复制内存块
    /// </summary>
    /// <param name="src">要复制的数据</param>
    /// <param name="size">复制的大小</param>
    public unsafe void MemCpy(void* src, ulong size)
    {
        System.Buffer.MemoryCopy(src, _mapped, size, size);
    }

    /// <summary>
    /// 销毁资源
    /// </summary>
    /// <param name="device">设备</param>
    public void UnMap(Device device)
    {
        vk.UnmapMemory(device, _memory);
    }

    public unsafe void Destroy(Device device)
    {
        vk.DestroyBuffer(device, Buffer, null);
        vk.FreeMemory(device, _memory, null);
    }
}
