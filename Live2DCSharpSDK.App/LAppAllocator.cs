using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework;

namespace Live2DCSharpSDK.App;

/// <summary>
/// メモリ確保・解放処理のインターフェースの実装。
/// フレームワークから呼び出される。
/// </summary>
/// <remarks>
/// S3（P0）: アラインメント付きの確保／解放は .NET 6+ の <see cref="NativeMemory"/> に委譲する。
/// 旧実装は <see cref="IntPtr"/>（符号付き nint）でアドレス演算と剰余を行っていたため、
/// アドレスの最上位ビットが立つ環境では `alignedAddress % alignment` が負になり、
/// preamble を確保領域の外へ書いてヒープを破壊し得た。また `(int)(size + offset)` の
/// 32bit 切り捨てと、確保失敗（null）時の preamble 書き込みも未処理だった。
/// <see cref="NativeMemory.AlignedAlloc"/> / <see cref="NativeMemory.AlignedFree"/> は
/// 確保と解放が同じ API で対になるため、元アドレスを保存する preamble そのものが不要になる。
/// </remarks>
public class LAppAllocator : ICubismAllocator
{
    /// <summary>
    /// 不正なアラインメントを渡されたときに使うフォールバック値。
    /// （Framework が実際に要求するのは Moc = 64 / Model = 16。それ以外は呼び出し側の不備。）
    /// </summary>
    private const int FallbackAlignment = 16;

    /// <summary>
    /// メモリ領域を割り当てる。
    /// </summary>
    /// <param name="size">割り当てたいサイズ。</param>
    /// <returns>指定したメモリ領域。失敗した場合は <see cref="IntPtr.Zero"/>。</returns>
    public IntPtr Allocate(int size)
    {
        // ICubismAllocator 契約: 失敗時は 0 を返す（負数はサイズとして不正）。
        if (size < 0)
        {
            LogErrorNoThrow($"Allocate() called with a negative size: {size}.");
            return IntPtr.Zero;
        }

        return Marshal.AllocHGlobal(size);
    }

    /// <summary>
    /// メモリ領域を解放する
    /// </summary>
    /// <param name="memory">解放するメモリ。</param>
    public void Deallocate(IntPtr memory)
    {
        // 0 は「確保失敗」を表すため解放要求は無視する（Marshal.FreeHGlobal(0) も no-op）。
        if (memory == IntPtr.Zero)
        {
            return;
        }

        Marshal.FreeHGlobal(memory);
    }

    /// <summary>
    /// アラインメント制約付きでメモリ領域を割り当てる。
    /// </summary>
    /// <param name="size">割り当てたいサイズ。</param>
    /// <param name="alignment">メモリーブロックのアラインメント幅（2 の冪）。</param>
    /// <returns>指定したアラインメントを満たすメモリ領域。失敗した場合は <see cref="IntPtr.Zero"/>。</returns>
    public unsafe IntPtr AllocateAligned(int size, int alignment)
    {
        if (size < 0)
        {
            LogErrorNoThrow($"AllocateAligned() called with a negative size: {size}.");
            return IntPtr.Zero;
        }

        int effectiveAlignment = NormalizeAlignment(alignment);

        try
        {
            // NativeMemory.AlignedAlloc は 2 の冪のアラインメントのみ受け付ける（上の正規化で保証済み）。
            // 失敗時は OutOfMemoryException を投げる実装なので、契約（0 を返す）に合わせて捕捉する。
            void* memory = NativeMemory.AlignedAlloc((nuint)size, (nuint)effectiveAlignment);

            if (memory == null)
            {
                LogErrorNoThrow($"AllocateAligned() failed. size={size}, alignment={effectiveAlignment}.");
                return IntPtr.Zero;
            }

            return (IntPtr)memory;
        }
        catch (OutOfMemoryException)
        {
            LogErrorNoThrow($"AllocateAligned() failed. size={size}, alignment={effectiveAlignment}.");
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// アラインメント制約付きのメモリ領域を解放する。
    /// </summary>
    /// <param name="alignedMemory">解放するメモリ</param>
    /// <remarks>
    /// 引数は <see cref="AllocateAligned"/> が返したアドレスでなければならない（確保／解放の対称性）。
    /// 現行の呼び出し元は Framework の CubismMoc / CubismModel のみで、いずれも AllocateAligned の
    /// 戻り値をそのまま渡している（Core は in-place API で同じアドレスを返すことを probe で確認済み）。
    /// 0 は解放要求として無視する（旧実装はここで preamble[-1] を読みに行き AV していた）。
    /// </remarks>
    public unsafe void DeallocateAligned(IntPtr alignedMemory)
    {
        if (alignedMemory == IntPtr.Zero)
        {
            return;
        }

        NativeMemory.AlignedFree((void*)alignedMemory);
    }

    /// <summary>
    /// アラインメントを検証し、不正なら安全な既定値に丸める。
    /// </summary>
    private static int NormalizeAlignment(int alignment)
    {
        // 1 は 2 の 0 乗なので有効。0 以下・非 2 の冪は無効。
        if (alignment > 0 && (alignment & (alignment - 1)) == 0)
        {
            return alignment;
        }

        CubismLog.Warning($"LAppAllocator: invalid alignment {alignment}. Fallback to {FallbackAlignment}.");
        return FallbackAlignment;
    }

    /// <summary>
    /// Error レベルでログを出すが例外は投げない。
    /// </summary>
    /// <remarks>
    /// <see cref="CubismLog.Error"/> は出力後に必ず例外を投げる実装（analyse H-6、在本批不改其本体）であり、
    /// 「確保失敗時は 0 を返す」という ICubismAllocator 契約と両立しない。
    /// そのため出力条件（ログレベル）は CubismLog と同じにしたまま、throw だけを省く。
    /// </remarks>
    private static void LogErrorNoThrow(string message)
    {
        // 出力条件は CubismLog と同じ（N-1: 旧実装は不等号が逆で、既定の LoggingLevel では
        // エラーログが一切出なかった。LogLevel は重大度が大きいほど値が大きい）。
        if (LogLevel.Error < CubismFramework.GetLoggingLevel())
        {
            return;
        }

        CubismFramework.CoreLogFunction($"[CSM] [E] {message}");
    }
}
