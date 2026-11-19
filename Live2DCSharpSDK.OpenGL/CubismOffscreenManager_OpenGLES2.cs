using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// オフスクリーン用のレンダーターゲットを管理するクラス
/// （官方 OpenGL/CubismOffscreenManager_OpenGLES2.hpp:19-57 / .cpp:17-67、B4）。
/// 官方同様にシングルトン（OpenGL 側のみ。Vulkan 側は CubismDeviceInfo 経由で非シングルトン）。
/// </summary>
public sealed class CubismOffscreenManager_OpenGLES2 : ICubismOffscreenManager<CubismRenderTarget_OpenGLES2>
{
    private static CubismOffscreenManager_OpenGLES2? s_instance;
    private static readonly object s_lock = new();

    private CubismOffscreenManager_OpenGLES2()
    {
    }

    /// <summary>
    /// クラスのインスタンス（シングルトン）を返す。
    /// インスタンスが生成されていない場合は内部で生成する。
    /// </summary>
    public static CubismOffscreenManager_OpenGLES2 GetInstance()
    {
        if (s_instance == null)
        {
            lock (s_lock)
            {
                s_instance ??= new CubismOffscreenManager_OpenGLES2();
            }
        }

        return s_instance;
    }

    /// <summary>
    /// クラスのインスタンス（シングルトン）を解放する。
    /// </summary>
    public static void ReleaseInstance()
    {
        lock (s_lock)
        {
            s_instance?.Dispose();
            s_instance = null;
        }
    }

    /// <summary>
    /// 使用可能なレンダーターゲットの取得（官方 GetOffscreenRenderTarget）。
    /// </summary>
    /// <param name="width">幅</param>
    /// <param name="height">高さ</param>
    /// <returns>使用可能なレンダーターゲット</returns>
    public CubismRenderTarget_OpenGLES2 GetOffscreenRenderTarget(uint width, uint height)
    {
        // 使用数を更新
        UpdateRenderTargetCount();

        // 使われていないリソースコンテナがあればそれを返す
        CubismRenderTarget_OpenGLES2? offscreenRenderTarget = GetUnusedOffscreenRenderTarget();
        if (offscreenRenderTarget != null)
        {
            // サイズが違う場合は再作成する
            if (offscreenRenderTarget.GetBufferWidth() != width || offscreenRenderTarget.GetBufferHeight() != height)
            {
                // S6: 作成に失敗しても戻り値の契約は非 null なので返すが、失敗を記録する。
                //     呼び出し側（CubismRenderer_OpenGLES2.AddOffscreen）は IsValid() で判定して描画をスキップする。
                if (!offscreenRenderTarget.CreateRenderTarget((int)width, (int)height))
                {
                    WarnInvalidRenderTarget(width, height);
                }
            }
            // 既存の未使用レンダーターゲットを返す
            return offscreenRenderTarget;
        }

        // 新規にレンダーターゲットを作成して登録する
        offscreenRenderTarget = CreateOffscreenRenderTarget();
        if (!offscreenRenderTarget.CreateRenderTarget((int)width, (int)height))
        {
            WarnInvalidRenderTarget(width, height);
        }
        return offscreenRenderTarget;
    }

    /// <summary>
    /// S6: オフスクリーン用 FBO を作成できなかったことを警告する。
    /// 状態コード・サイズなどの詳細は <see cref="CubismRenderTarget_OpenGLES2.CreateRenderTarget"/> 側で出力済み。
    /// </summary>
    private static void WarnInvalidRenderTarget(uint width, uint height)
    {
        CubismLog.Warning(
            "[Live2D OpenGL]GetOffscreenRenderTarget: failed to create a render target {0}x{1}. " +
            "The renderer skips drawing into this offscreen.",
            width, height);
    }
}
