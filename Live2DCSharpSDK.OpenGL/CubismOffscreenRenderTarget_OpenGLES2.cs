using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// 管理 Offscreen 用渲染目标的类
/// (对应官方 OpenGL/CubismOffscreenRenderTarget_OpenGLES2.hpp:19-52 / .cpp:14-46).
/// 所有实现都委托给 CubismOffscreenManager_OpenGLES2. 
/// </summary>
public class CubismOffscreenRenderTarget_OpenGLES2
    : ICubismOffscreenRenderTarget<CubismOffscreenRenderTarget_OpenGLES2, CubismRenderTarget_OpenGLES2>
{
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
            if (_renderTarget!.GetBufferWidth() != width || _renderTarget.GetBufferHeight() != height)
            {
                _renderTarget.CreateRenderTarget((int)width, (int)height);
            }
            return;
        }

        _renderTarget = CubismOffscreenManager_OpenGLES2.GetInstance().GetOffscreenRenderTarget(width, height);
    }

    /// <summary>
    /// 获取渲染目标的使用状态 (官方 GetUsingRenderTextureState). 
    /// </summary>
    /// <returns>使用中为 true, 未使用时为 false</returns>
    public bool GetUsingRenderTextureState()
    {
        return _renderTarget != null
            && CubismOffscreenManager_OpenGLES2.GetInstance().GetUsingRenderTextureState(_renderTarget);
    }

    /// <summary>
    /// 结束 Offscreen 渲染用的渲染目标的使用 (官方 StopUsingRenderTexture). 
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
