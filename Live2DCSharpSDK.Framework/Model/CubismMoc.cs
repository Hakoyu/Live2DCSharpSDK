using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework.Core;

namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 进行 Moc 数据管理的类.
/// </summary>
public class CubismMoc : IDisposable
{
    /// <summary>
    /// Moc 数据 (官方 5-r.5 中由 private 改为 protected)
    /// </summary>
    protected readonly IntPtr _moc;
    /// <summary>
    /// 已读入模型的 .moc3 Version
    /// </summary>
    public uint MocVersion { get; }

    public CubismModel Model { get; }

    /// <summary>
    /// 是否已释放(Dispose 的幂等保护, 即使从 CubismUserModel 的释放路径被重复调用也安全).
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 所属上下文 (提供内存分配器).
    /// </summary>
    private readonly CubismContext _context;

    /// <summary>
    /// 使用默认上下文从缓冲区读取 Moc 文件 (过渡期兼容重载).
    /// </summary>
    /// <param name="mocBytes"> Moc 文件的缓冲区</param>
    /// <param name="shouldCheckMocConsistency">MOC 一致性检查标志(初始值 : false)</param>
    /// <returns></returns>
    /// <remarks>
    /// 仅为兼容既有调用点保留, 内部回落到 <see cref="CubismContext.Default"/>.
    /// 新代码请使用带 context 的重载.
    /// </remarks>
    public CubismMoc(byte[] mocBytes, bool shouldCheckMocConsistency = false)
        : this(CubismContext.Default, mocBytes, shouldCheckMocConsistency)
    {
    }

    /// <summary>
    /// 使用指定上下文从缓冲区读取 Moc 文件, 并创建 Moc 数据.
    /// </summary>
    /// <param name="context">所属上下文 (提供内存分配器)</param>
    /// <param name="mocBytes"> Moc 文件的缓冲区</param>
    /// <param name="shouldCheckMocConsistency">MOC 一致性检查标志(初始值 : false)</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">context 为 null 时抛出</exception>
    public CubismMoc(CubismContext context, byte[] mocBytes, bool shouldCheckMocConsistency = false)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context), "CubismMoc requires a context instance.");
        }

        _context = context;

        IntPtr alignedBuffer = context.Allocator.AllocateAligned(mocBytes.Length, CsmEnum.csmAlignofMoc);
        Marshal.Copy(mocBytes, 0, alignedBuffer, mocBytes.Length);

        if (shouldCheckMocConsistency)
        {
            // 确认 .moc3 的一致性
            bool consistency = HasMocConsistency(alignedBuffer, mocBytes.Length);
            if (!consistency)
            {
                context.Allocator.DeallocateAligned(alignedBuffer);

                // 如果无法确认一致性则不处理
                throw new Exception("Inconsistent MOC3.");
            }
        }

        var moc = CubismCore.ReviveMocInPlace(alignedBuffer, mocBytes.Length);

        if (moc == IntPtr.Zero)
        {
            throw new Exception("MOC3 is null");
        }

        _moc = moc;

        MocVersion = CubismCore.GetMocVersion(alignedBuffer, mocBytes.Length);

        var modelSize = CubismCore.GetSizeofModel(_moc);
        var modelMemory = context.Allocator.AllocateAligned(modelSize, CsmEnum.CsmAlignofModel);

        var model = CubismCore.InitializeModelInPlace(_moc, modelMemory, modelSize);

        if (model == IntPtr.Zero)
        {
            throw new Exception("MODEL is null");
        }

        Model = new CubismModel(context, model);
    }

    /// <summary>
    /// 获取最新的 .moc3 Version.
    /// </summary>
    /// <returns></returns>
    public static uint GetLatestMocVersion()
    {
        return CubismCore.GetLatestMocVersion();
    }

    /// <summary>
    /// 从未还原的 Moc 缓冲区获取 .moc3 的版本 (官方 5-r.5 的新增 API GetMocVersionFromBuffer).
    /// 即使是尚未支持的新 MOC3, 也能只取得版本.
    /// </summary>
    /// <param name="mocBytes">Moc 文件的缓冲区</param>
    /// <returns>.moc3 Version. 参数非法时为 0</returns>
    public static uint GetMocVersionFromBuffer(byte[] mocBytes)
        => GetMocVersionFromBuffer(CubismContext.Default, mocBytes);

    /// <summary>
    /// 使用指定上下文从未还原的 Moc 缓冲区获取 .moc3 的版本 (官方 5-r.5 的新增 API GetMocVersionFromBuffer).
    /// 即使是尚未支持的新 MOC3, 也能只取得版本.
    /// </summary>
    /// <param name="context">所属上下文 (提供内存分配器)</param>
    /// <param name="mocBytes">Moc 文件的缓冲区</param>
    /// <returns>.moc3 Version. 参数非法时为 0</returns>
    /// <exception cref="ArgumentNullException">context 为 null 时抛出</exception>
    public static uint GetMocVersionFromBuffer(CubismContext context, byte[] mocBytes)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context), "CubismMoc requires a context instance.");
        }

        if (mocBytes is null || mocBytes.Length == 0)
        {
            // 官方会输出 CubismLogError 并返回 0 (C# 的 CubismLog.Error 会抛出异常, 因此用 Warning 代替)
            CubismLog.Warning("Invalid mocBytes or size.");
            return 0;
        }

        IntPtr alignedBuffer = context.Allocator.AllocateAligned(mocBytes.Length, CsmEnum.csmAlignofMoc);
        Marshal.Copy(mocBytes, 0, alignedBuffer, mocBytes.Length);

        var version = CubismCore.GetMocVersion(alignedBuffer, mocBytes.Length);

        context.Allocator.DeallocateAligned(alignedBuffer);

        return version;
    }

    /// <summary>
    /// Checks consistency of a moc.
    /// </summary>
    /// <param name="address">Address of unrevived moc. The address must be aligned to 'csmAlignofMoc'.</param>
    /// <param name="size">Size of moc (in bytes).</param>
    /// <returns>'1' if Moc is valid; '0' otherwise.</returns>
    public static bool HasMocConsistency(IntPtr address, int size)
    {
        return CubismCore.HasMocConsistency(address, size);
    }

    /// <summary>
    /// Checks consistency of a moc.
    /// </summary>
    /// <param name="mocBytes">Moc 文件的缓冲区</param>
    /// <returns>'true' if Moc is valid; 'false' otherwise.</returns>
    public static bool HasMocConsistencyFromUnrevivedMoc(byte[] data)
        => HasMocConsistencyFromUnrevivedMoc(CubismContext.Default, data);

    /// <summary>
    /// 使用指定上下文检查 Moc 的一致性.
    /// </summary>
    /// <param name="context">所属上下文 (提供内存分配器)</param>
    /// <param name="data">Moc 文件的缓冲区</param>
    /// <returns>'true' if Moc is valid; 'false' otherwise.</returns>
    /// <exception cref="ArgumentNullException">context 为 null 时抛出</exception>
    public static bool HasMocConsistencyFromUnrevivedMoc(CubismContext context, byte[] data)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context), "CubismMoc requires a context instance.");
        }

        IntPtr alignedBuffer = context.Allocator.AllocateAligned(data.Length, CsmEnum.csmAlignofMoc);
        Marshal.Copy(data, 0, alignedBuffer, data.Length);

        bool consistency = HasMocConsistency(alignedBuffer, data.Length);

        context.Allocator.DeallocateAligned(alignedBuffer);

        return consistency;
    }

    /// <summary>
    /// 终结器(幂等, 第二次及以后不做任何事).
    /// </summary>
    public virtual void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Model.Dispose() 本身也是幂等的, 因此即使先销毁 CubismModel 也不会重复释放.
        Model.Dispose();
        _context.Allocator.DeallocateAligned(_moc);
        GC.SuppressFinalize(this);
    }
}
