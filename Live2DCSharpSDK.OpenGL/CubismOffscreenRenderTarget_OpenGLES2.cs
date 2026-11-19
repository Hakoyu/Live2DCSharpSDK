using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// 管理 Offscreen 用渲染目标的类
/// (对应官方 OpenGL/CubismOffscreenRenderTarget_OpenGLES2.hpp:19-52 / .cpp:14-46).
/// 所有实现都委托给所属的 CubismOffscreenManager_OpenGLES2 实例
/// (每个渲染器持有自己的管理器, 不再使用进程级单例).
/// </summary>
public class CubismOffscreenRenderTarget_OpenGLES2
    : ICubismOffscreenRenderTarget<
        CubismOffscreenRenderTarget_OpenGLES2,
        CubismRenderTarget_OpenGLES2
    >
{
    /// <summary>
    /// 本对象借出渲染目标的管理器.
    /// </summary>
    private readonly CubismOffscreenManager_OpenGLES2 _offscreenManager;

    /// <summary>
    /// 构造实例.
    /// </summary>
    /// <param name="offscreenManager">本对象借出渲染目标的管理器 (渲染器自有的实例)</param>
    /// <exception cref="ArgumentNullException">offscreenManager 为 null 时抛出</exception>
    public CubismOffscreenRenderTarget_OpenGLES2(CubismOffscreenManager_OpenGLES2 offscreenManager)
    {
        if (offscreenManager is null)
        {
            throw new ArgumentNullException(
                nameof(offscreenManager),
                "[Live2D OpenGL]CubismOffscreenRenderTarget_OpenGLES2 requires an offscreen manager instance."
            );
        }

        _offscreenManager = offscreenManager;
    }

    /// <summary>
    /// 设置 Offscreen 渲染用的渲染目标 (官方 SetOffscreenRenderTarget).
    /// </summary>
    /// <param name="width">幅</param>
    /// <param name="height">高度</param>
    public void SetOffscreenRenderTarget(uint width, uint height)
    {
        if (GetUsingRenderTextureState())
        {
            // 使用中时只检查尺寸
            if (
                _renderTarget!.GetBufferWidth() != width
                || _renderTarget.GetBufferHeight() != height
            )
            {
                _renderTarget.CreateRenderTarget((int)width, (int)height);
            }
            return;
        }

        _renderTarget = _offscreenManager.GetOffscreenRenderTarget(width, height);
    }

    /// <summary>
    /// 获取渲染目标的使用状态 (官方 GetUsingRenderTextureState).
    /// </summary>
    /// <returns>使用中为 true, 未使用时为 false</returns>
    public bool GetUsingRenderTextureState()
    {
        return _renderTarget is not null
            && _offscreenManager.GetUsingRenderTextureState(_renderTarget);
    }

    /// <summary>
    /// 结束 Offscreen 渲染用的渲染目标的使用 (官方 StopUsingRenderTexture).
    /// </summary>
    public void StopUsingRenderTexture()
    {
        if (_renderTarget is null)
        {
            return;
        }

        _offscreenManager.StopUsingRenderTexture(_renderTarget);
        _renderTarget = null;
    }
}
