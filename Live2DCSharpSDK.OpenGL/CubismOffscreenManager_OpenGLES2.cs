using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// 管理 Offscreen 用渲染目标的类
/// (对应官方 OpenGL/CubismOffscreenManager_OpenGLES2.hpp:19-57 / .cpp:17-67).
/// 与官方相同为单例 (仅 OpenGL 侧. Vulkan 侧通过 CubismDeviceInfo 使用非单例). 
/// </summary>
public sealed class CubismOffscreenManager_OpenGLES2 : ICubismOffscreenManager<CubismRenderTarget_OpenGLES2>
{
    private static CubismOffscreenManager_OpenGLES2? s_instance;
    private static readonly object s_lock = new();

    private CubismOffscreenManager_OpenGLES2()
    {
    }

    /// <summary>
    /// 返回类的实例 (单例). 
    /// 实例尚未生成时在内部生成. 
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
    /// 释放类的实例 (单例). 
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
    /// 获取可用的渲染目标 (官方 GetOffscreenRenderTarget). 
    /// </summary>
    /// <param name="width">幅</param>
    /// <param name="height">高度</param>
    /// <returns>可用的渲染目标</returns>
    public CubismRenderTarget_OpenGLES2 GetOffscreenRenderTarget(uint width, uint height)
    {
        // 更新使用数
        UpdateRenderTargetCount();

        // 若有未被使用的资源容器则返回它
        CubismRenderTarget_OpenGLES2? offscreenRenderTarget = GetUnusedOffscreenRenderTarget();
        if (offscreenRenderTarget != null)
        {
            // 尺寸不同时重新创建
            if (offscreenRenderTarget.GetBufferWidth() != width || offscreenRenderTarget.GetBufferHeight() != height)
            {
                // 创建失败时返回值契约仍为非 null, 因此照常返回, 但会记录失败状态.
                //     调用方 (CubismRenderer_OpenGLES2.AddOffscreen) 用 IsValid() 判定后跳过绘制. 
                if (!offscreenRenderTarget.CreateRenderTarget((int)width, (int)height))
                {
                    WarnInvalidRenderTarget(width, height);
                }
            }
            // 返回已存在的未使用渲染目标
            return offscreenRenderTarget;
        }

        // 新建渲染目标并注册
        offscreenRenderTarget = CreateOffscreenRenderTarget();
        if (!offscreenRenderTarget.CreateRenderTarget((int)width, (int)height))
        {
            WarnInvalidRenderTarget(width, height);
        }
        return offscreenRenderTarget;
    }

    /// <summary>
    /// 警告离屏用帧缓冲创建失败.
    /// 状态码, 尺寸等详细信息已由 <see cref="CubismRenderTarget_OpenGLES2.CreateRenderTarget"/> 侧输出. 
    /// </summary>
    private static void WarnInvalidRenderTarget(uint width, uint height)
    {
        CubismLog.Warning(
            "[Live2D OpenGL]GetOffscreenRenderTarget: failed to create a render target {0}x{1}. " +
            "The renderer skips drawing into this offscreen.",
            width, height);
    }
}
