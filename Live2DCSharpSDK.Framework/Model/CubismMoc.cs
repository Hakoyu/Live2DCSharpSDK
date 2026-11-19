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
    /// 从缓冲区读取 Moc 文件, 并创建 Moc 数据.
    /// </summary>
    /// <param name="mocBytes"> Moc 文件的缓冲区</param>
    /// <param name="shouldCheckMocConsistency">MOC 一致性检查标志(初始值 : false)</param>
    /// <returns></returns>
    public CubismMoc(byte[] mocBytes, bool shouldCheckMocConsistency = false)
    {
        IntPtr alignedBuffer = CubismFramework.AllocateAligned(mocBytes.Length, CsmEnum.csmAlignofMoc);
        Marshal.Copy(mocBytes, 0, alignedBuffer, mocBytes.Length);

        if (shouldCheckMocConsistency)
        {
            // 确认 .moc3 的一致性
            bool consistency = HasMocConsistency(alignedBuffer, mocBytes.Length);
            if (!consistency)
            {
                CubismFramework.DeallocateAligned(alignedBuffer);

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
        var modelMemory = CubismFramework.AllocateAligned(modelSize, CsmEnum.CsmAlignofModel);

        var model = CubismCore.InitializeModelInPlace(_moc, modelMemory, modelSize);

        if (model == IntPtr.Zero)
        {
            throw new Exception("MODEL is null");
        }

        Model = new CubismModel(model);
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
    {
        if (mocBytes == null || mocBytes.Length == 0)
        {
            // 官方会输出 CubismLogError 并返回 0 (C# 的 CubismLog.Error 会抛出异常, 因此用 Warning 代替)
            CubismLog.Warning("Invalid mocBytes or size.");
            return 0;
        }

        IntPtr alignedBuffer = CubismFramework.AllocateAligned(mocBytes.Length, CsmEnum.csmAlignofMoc);
        Marshal.Copy(mocBytes, 0, alignedBuffer, mocBytes.Length);

        var version = CubismCore.GetMocVersion(alignedBuffer, mocBytes.Length);

        CubismFramework.DeallocateAligned(alignedBuffer);

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
    {
        IntPtr alignedBuffer = CubismFramework.AllocateAligned(data.Length, CsmEnum.csmAlignofMoc);
        Marshal.Copy(data, 0, alignedBuffer, data.Length);

        bool consistency = HasMocConsistency(alignedBuffer, data.Length);

        CubismFramework.DeallocateAligned(alignedBuffer);

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
        CubismFramework.DeallocateAligned(_moc);
        GC.SuppressFinalize(this);
    }
}
