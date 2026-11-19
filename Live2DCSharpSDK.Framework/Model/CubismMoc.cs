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
            throw new ArgumentNullException(
                nameof(context),
                "CubismMoc requires a context instance."
            );
        }

        if (mocBytes is null)
        {
            throw new ArgumentNullException(nameof(mocBytes), "MOC3 data is required.");
        }

        _context = context;

        // 两块非托管内存先按 "所有权未转移" 处理, 任何一步失败都在下面的 catch 里统一回收.
        IntPtr alignedBuffer = IntPtr.Zero;
        IntPtr modelMemory = IntPtr.Zero;
        bool modelMemoryOwned = false;

        try
        {
            alignedBuffer = context.Allocator.AllocateAligned(
                mocBytes.Length,
                CsmEnum.csmAlignofMoc
            );
            if (alignedBuffer == IntPtr.Zero)
            {
                throw new OutOfMemoryException("Failed to allocate MOC3 memory.");
            }

            Marshal.Copy(mocBytes, 0, alignedBuffer, mocBytes.Length);

            if (
                shouldCheckMocConsistency
                && HasMocConsistency(alignedBuffer, mocBytes.Length) is false
            )
            {
                // 如果无法确认一致性则不处理, 缓冲区交给 catch 释放.
                throw new Exception("Inconsistent MOC3.");
            }

            var moc = CubismCore.ReviveMocInPlace(alignedBuffer, mocBytes.Length);

            if (moc == IntPtr.Zero)
            {
                throw new Exception("MOC3 revive failed.");
            }

            _moc = moc;

            MocVersion = CubismCore.GetMocVersion(alignedBuffer, mocBytes.Length);

            var modelSize = CubismCore.GetSizeofModel(_moc);
            modelMemory = context.Allocator.AllocateAligned(modelSize, CsmEnum.CsmAlignofModel);
            if (modelMemory == IntPtr.Zero)
            {
                throw new OutOfMemoryException("Failed to allocate model memory.");
            }

            var model = CubismCore.InitializeModelInPlace(_moc, modelMemory, modelSize);

            if (model == IntPtr.Zero)
            {
                throw new Exception("Model initialization failed.");
            }

            Model = new CubismModel(context, model);

            // 所有权转移点: 模型对象构造成功后, model 内存由 CubismModel.Dispose 释放,
            // catch 中不得再释放, 否则会与模型自身的释放叠成 double free.
            modelMemoryOwned = true;
        }
        catch
        {
            try
            {
                if (modelMemoryOwned is false && modelMemory != IntPtr.Zero)
                {
                    context.Allocator.DeallocateAligned(modelMemory);
                }
            }
            finally
            {
                if (alignedBuffer != IntPtr.Zero)
                {
                    context.Allocator.DeallocateAligned(alignedBuffer);
                }
            }

            throw;
        }
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
            throw new ArgumentNullException(
                nameof(context),
                "CubismMoc requires a context instance."
            );
        }

        if (mocBytes is null || mocBytes.Length == 0)
        {
            // 官方会输出 CubismLogError 并返回 0 (C# 的 CubismLog.Error 会抛出异常, 因此用 Warning 代替)
            CubismLog.Warning("Invalid mocBytes or size.");
            return 0;
        }

        IntPtr alignedBuffer = context.Allocator.AllocateAligned(
            mocBytes.Length,
            CsmEnum.csmAlignofMoc
        );
        if (alignedBuffer == IntPtr.Zero)
        {
            throw new OutOfMemoryException("Failed to allocate MOC3 memory.");
        }

        try
        {
            Marshal.Copy(mocBytes, 0, alignedBuffer, mocBytes.Length);
            return CubismCore.GetMocVersion(alignedBuffer, mocBytes.Length);
        }
        finally
        {
            context.Allocator.DeallocateAligned(alignedBuffer);
        }
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
            throw new ArgumentNullException(
                nameof(context),
                "CubismMoc requires a context instance."
            );
        }

        IntPtr alignedBuffer = context.Allocator.AllocateAligned(
            data.Length,
            CsmEnum.csmAlignofMoc
        );
        if (alignedBuffer == IntPtr.Zero)
        {
            throw new OutOfMemoryException("Failed to allocate MOC3 memory.");
        }

        try
        {
            Marshal.Copy(data, 0, alignedBuffer, data.Length);
            return HasMocConsistency(alignedBuffer, data.Length);
        }
        finally
        {
            context.Allocator.DeallocateAligned(alignedBuffer);
        }
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
