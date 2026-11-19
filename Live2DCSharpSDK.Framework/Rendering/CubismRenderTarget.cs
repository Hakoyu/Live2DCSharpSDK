namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// 离屏绘制用渲染目标的抽象基类.
/// バックエンド固有の生成・描画 API は派生クラス
/// （CubismRenderTarget_OpenGLES2 / CubismRenderTarget_Vulkan）が持ち、
/// ここには <see cref="ICubismOffscreenManager{T}"/> が共通利用する
/// 「破棄」と「サイズ照会」だけを置く。
/// 官方对应: OpenGL/CubismRenderTarget_OpenGLES2.hpp、Vulkan/CubismRenderTarget_Vulkan.hpp
/// </summary>
public abstract class CubismRenderTarget : IDisposable
{
    /// <summary>
    /// 現在有効かどうか（官方 IsValid）
    /// </summary>
    public abstract bool IsValid();

    /// <summary>
    /// バッファ幅を取得する（官方 GetBufferWidth）
    /// </summary>
    public abstract uint GetBufferWidth();

    /// <summary>
    /// バッファ高さを取得する（官方 GetBufferHeight）
    /// </summary>
    public abstract uint GetBufferHeight();

    /// <summary>
    /// レンダーターゲットを削除する（官方 DestroyRenderTarget）
    /// </summary>
    public abstract void DestroyRenderTarget();

    /// <summary>
    /// レンダーターゲットを解放する
    /// </summary>
    public void Dispose()
    {
        DestroyRenderTarget();
        GC.SuppressFinalize(this);
    }
}
