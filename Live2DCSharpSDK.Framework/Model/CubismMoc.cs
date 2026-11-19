using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework.Core;

namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// Mocデータの管理を行うクラス。
/// </summary>
public class CubismMoc : IDisposable
{
    /// <summary>
    /// Mocデータ（官方 5-r.5 で private → protected に変更）
    /// </summary>
    protected readonly IntPtr _moc;
    /// <summary>
    /// 読み込んだモデルの.moc3 Version
    /// </summary>
    public uint MocVersion { get; }

    public CubismModel Model { get; }

    /// <summary>
    /// 是否已释放(Dispose 的幂等保护, 即使从 CubismUserModel 的释放路径被重复调用也安全).
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// バッファからMocファイルを読み取り、Mocデータを作成する。
    /// </summary>
    /// <param name="mocBytes"> Mocファイルのバッファ</param>
    /// <param name="shouldCheckMocConsistency">MOCの整合性チェックフラグ(初期値 : false)</param>
    /// <returns></returns>
    public CubismMoc(byte[] mocBytes, bool shouldCheckMocConsistency = false)
    {
        IntPtr alignedBuffer = CubismFramework.AllocateAligned(mocBytes.Length, CsmEnum.csmAlignofMoc);
        Marshal.Copy(mocBytes, 0, alignedBuffer, mocBytes.Length);

        if (shouldCheckMocConsistency)
        {
            // .moc3の整合性を確認
            bool consistency = HasMocConsistency(alignedBuffer, mocBytes.Length);
            if (!consistency)
            {
                CubismFramework.DeallocateAligned(alignedBuffer);

                // 整合性が確認できなければ処理しない
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
    /// 最新の.moc3 Versionを取得する。
    /// </summary>
    /// <returns></returns>
    public static uint GetLatestMocVersion()
    {
        return CubismCore.GetLatestMocVersion();
    }

    /// <summary>
    /// 未復元の Moc バッファから .moc3 のバージョンを取得する（官方 5-r.5 の新規 API GetMocVersionFromBuffer）。
    /// 未対応の新しい MOC3 でもバージョンだけは取得できる。
    /// </summary>
    /// <param name="mocBytes">Mocファイルのバッファ</param>
    /// <returns>.moc3 Version。不正な引数の場合は 0</returns>
    public static uint GetMocVersionFromBuffer(byte[] mocBytes)
    {
        if (mocBytes == null || mocBytes.Length == 0)
        {
            // 官方は CubismLogError を出して 0 を返す（C# の CubismLog.Error は例外を投げるため Warning で代替）
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
    /// <param name="mocBytes">Mocファイルのバッファ</param>
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

        // Model.Dispose() 自体も冪等なので、CubismModel を先に破棄していても二重解放にならない。
        Model.Dispose();
        CubismFramework.DeallocateAligned(_moc);
        GC.SuppressFinalize(this);
    }
}
