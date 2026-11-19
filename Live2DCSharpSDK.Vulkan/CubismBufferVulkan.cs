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
    /// 已映射的字节数, 0 表示未映射
    /// </summary>
    private ulong _mappedSize;
    /// <summary>
    /// buffer 的分配容量, 用于 MemCpy 越界检查
    /// </summary>
    private ulong _capacity;

    /// <summary>
    /// buffer 与内存是否都已创建成功
    /// </summary>
    public bool IsValid => _buffer.Handle != 0 && _memory.Handle != 0;

    /// <summary>
    /// buffer 的分配容量
    /// </summary>
    public ulong Capacity => _capacity;

    /// <summary>
    /// 当前是否已映射
    /// </summary>
    public unsafe bool IsMapped => _mapped is not null;

    /// <summary>
    /// 查找物理设备内存类型的索引
    /// </summary>
    /// <param name="physicalDevice">物理设备</param>
    /// <param name="typeFilter">存在内存类型时被设置的位掩码</param>
    /// <param name="properties">内存访问设备时的类型</param>
    /// <returns>内存类型索引, 找不到兼容类型时返回 -1</returns>
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

        // 不能用 CubismLog.Error(会抛未类型化异常), 由调用方决定降级还是抛明确异常.
        CubismLog.Warning("[Live2D Vulkan]failed to find suitable memory type!");
        return -1;
    }

    /// <summary>
    /// 创建 buffer
    /// </summary>
    /// <param name="device">设备</param>
    /// <param name="physicalDevice">物理设备</param>
    /// <param name="size">buffer 大小</param>
    /// <param name="usage">指定 buffer 用途的位掩码</param>
    /// <param name="properties">内存访问设备时的类型</param>
    /// <returns>成功返回 true, 失败时已回收部分资源并返回 false</returns>
    public unsafe bool CreateBuffer(Device device, PhysicalDevice physicalDevice, ulong size, BufferUsageFlags usage,
                      MemoryPropertyFlags properties)
    {
        Destroy(device);

        if (size == 0)
        {
            CubismLog.Warning("[Live2D Vulkan]create buffer with zero size.");
            return false;
        }

        var bufferInfo = new BufferCreateInfo()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        if (vk.CreateBuffer(device, ref bufferInfo, null, out _buffer) != Result.Success)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to create buffer ({0} bytes).", size);
            _buffer = default;
            return false;
        }

        vk.GetBufferMemoryRequirements(device, _buffer, out var memRequirements);

        int memoryTypeIndex = FindMemoryType(physicalDevice, memRequirements.MemoryTypeBits, properties);
        if (memoryTypeIndex < 0)
        {
            // 找不到兼容内存类型: 先回收已创建的 buffer, 避免句柄泄漏.
            ReleaseBuffer(device);
            return false;
        }

        var allocInfo = new MemoryAllocateInfo()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = (uint)memoryTypeIndex
        };

        if (vk.AllocateMemory(device, ref allocInfo, null, out _memory) != Result.Success)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to allocate buffer memory ({0} bytes).", memRequirements.Size);
            _memory = default;
            ReleaseBuffer(device);
            return false;
        }

        if (vk.BindBufferMemory(device, _buffer, _memory, 0) != Result.Success)
        {
            CubismLog.Warning("[Live2D Vulkan]failed to bind buffer memory.");
            Destroy(device);
            return false;
        }

        _capacity = size;
        return true;
    }

    /// <summary>
    /// 把内存映射到地址空间, 并取得该地址指针
    /// </summary>
    /// <param name="device">设备</param>
    /// <param name="size">映射的大小, ulong.MaxValue 表示整段分配</param>
    /// <returns>映射成功(或已处于映射状态)返回 true</returns>
    public unsafe bool Map(Device device, ulong size)
    {
        if (_memory.Handle == 0)
        {
            CubismLog.Warning("[Live2D Vulkan]map buffer without memory.");
            return false;
        }

        if (_mapped is not null)
        {
            return true;
        }

        if (size != ulong.MaxValue && size > _capacity)
        {
            CubismLog.Warning("[Live2D Vulkan]map size {0} exceeds buffer capacity {1}.", size, _capacity);
            return false;
        }

        ulong mappedSize = size == ulong.MaxValue ? _capacity : size;

        if (vk.MapMemory(device, _memory, 0, size, 0, ref _mapped) != Result.Success || _mapped is null)
        {
            // 映射失败时必须让指针保持为空, 否则后续 MemCpy 会写入非法地址.
            _mapped = null;
            CubismLog.Warning("[Live2D Vulkan]failed to map buffer memory ({0} bytes).", mappedSize);
            return false;
        }

        _mappedSize = mappedSize;
        return true;
    }

    /// <summary>
    /// 复制内存块
    /// </summary>
    /// <param name="src">要复制的数据</param>
    /// <param name="size">复制的大小</param>
    /// <returns>复制成功返回 true, 未映射或越界时返回 false</returns>
    public unsafe bool MemCpy(void* src, ulong size)
    {
        if (src is null || _mapped is null)
        {
            CubismLog.Warning("[Live2D Vulkan]memcpy on unmapped or empty buffer.");
            return false;
        }

        if (size > _mappedSize)
        {
            CubismLog.Warning("[Live2D Vulkan]memcpy size {0} exceeds mapped size {1}.", size, _mappedSize);
            return false;
        }

        System.Buffer.MemoryCopy(src, _mapped, _mappedSize, size);
        return true;
    }

    /// <summary>
    /// 解除内存映射
    /// </summary>
    /// <param name="device">设备</param>
    public unsafe void UnMap(Device device)
    {
        if (_mapped is not null && _memory.Handle != 0)
        {
            vk.UnmapMemory(device, _memory);
        }

        _mapped = null;
        _mappedSize = 0;
    }

    /// <summary>
    /// 销毁资源
    /// </summary>
    /// <param name="device">设备</param>
    public unsafe void Destroy(Device device)
    {
        UnMap(device);
        ReleaseBuffer(device);

        if (_memory.Handle != 0)
        {
            vk.FreeMemory(device, _memory, null);
            _memory = default;
        }

        _capacity = 0;
    }

    /// <summary>
    /// 释放已创建的 buffer 句柄, 内存句柄不动
    /// </summary>
    /// <param name="device">设备</param>
    private unsafe void ReleaseBuffer(Device device)
    {
        if (_buffer.Handle != 0)
        {
            vk.DestroyBuffer(device, _buffer, null);
            _buffer = default;
        }
    }
}
