using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// オフスクリーン用のレンダーターゲットを管理するクラス
/// （官方 OpenGL/CubismOffscreenRenderTarget_OpenGLES2.hpp:19-52 / .cpp:14-46、B3）。
/// 実体はすべて CubismOffscreenManager_OpenGLES2 に委譲する。
/// </summary>
public class CubismOffscreenRenderTarget_OpenGLES2
    : ICubismOffscreenRenderTarget<CubismOffscreenRenderTarget_OpenGLES2, CubismRenderTarget_OpenGLES2>
{
    /// <summary>
    /// オフスクリーン描画用レンダーターゲットを設定する（官方 SetOffscreenRenderTarget）。
    /// </summary>
    /// <param name="width">幅</param>
    /// <param name="height">高さ</param>
    public void SetOffscreenRenderTarget(uint width, uint height)
    {
        if (GetUsingRenderTextureState())
        {
            // 使用中の場合はサイズだけ確認
            if (_renderTarget!.GetBufferWidth() != width || _renderTarget.GetBufferHeight() != height)
            {
                _renderTarget.CreateRenderTarget((int)width, (int)height);
            }
            return;
        }

        _renderTarget = CubismOffscreenManager_OpenGLES2.GetInstance().GetOffscreenRenderTarget(width, height);
    }

    /// <summary>
    /// レンダーターゲットの使用状態を取得する（官方 GetUsingRenderTextureState）。
    /// </summary>
    /// <returns>使用中はtrue、未使用の場合はfalse</returns>
    public bool GetUsingRenderTextureState()
    {
        return _renderTarget != null
            && CubismOffscreenManager_OpenGLES2.GetInstance().GetUsingRenderTextureState(_renderTarget);
    }

    /// <summary>
    /// オフスクリーン描画用レンダーターゲットの使用を終了する（官方 StopUsingRenderTexture）。
    /// </summary>
    public void StopUsingRenderTexture()
    {
        if (_renderTarget == null)
        {
            return;
        }

        CubismOffscreenManager_OpenGLES2.GetInstance().StopUsingRenderTexture(_renderTarget);
        _renderTarget = null;
    }
}
