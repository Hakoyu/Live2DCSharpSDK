using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// 管理 Offscreen 用渲染目标的类
/// (对应官方 OpenGL/CubismOffscreenManager_OpenGLES2.hpp:19-57 / .cpp:17-67).
/// 每个实例持有自己的渲染目标池, 并绑定一个 OpenGLApi.
/// CubismRenderer_OpenGLES2 在构造时创建自用的实例, 因此多个渲染器 (多个 GL 上下文)
/// 之间不再共享池. 旧的静态入口 GetInstance 仅为对外 API 兼容保留.
/// </summary>
public sealed class CubismOffscreenManager_OpenGLES2 : ICubismOffscreenManager<CubismRenderTarget_OpenGLES2>
{
    /// <summary>
    /// 兼容旧单例入口的默认管理器. 
    /// </summary>
    private static CubismOffscreenManager_OpenGLES2? s_instance;
    private static readonly object s_lock = new();

    /// <summary>
    /// 本管理器创建渲染目标时使用的 OpenGL API.
    /// 兼容入口 (GetInstance) 生成的默认管理器在绑定之前为 null.
    /// </summary>
    private OpenGLApi? _gl;

    /// <summary>
    /// 构造实例.
    /// </summary>
    /// <param name="gl">本管理器创建的渲染目标使用的 OpenGL API</param>
    /// <exception cref="ArgumentNullException">gl 为 null 时抛出</exception>
    public CubismOffscreenManager_OpenGLES2(OpenGLApi gl)
    {
        if (gl is null)
        {
            throw new ArgumentNullException(nameof(gl),
                "[Live2D OpenGL]CubismOffscreenManager_OpenGLES2 requires an OpenGLApi instance.");
        }

        _gl = gl;
    }

    /// <summary>
    /// 默认管理器用的构造函数 (未绑定 OpenGLApi). 
    /// </summary>
    private CubismOffscreenManager_OpenGLES2()
    {
    }

    /// <summary>
    /// 返回默认管理器 (兼容旧单例入口). 
    /// 本仓库内的渲染器改为各自持有实例, 不再使用该入口.
    /// 默认管理器未绑定 OpenGLApi 时, GetOffscreenRenderTarget 会抛出 InvalidOperationException,
    /// 此时需先调用 <see cref="BindApi"/> 绑定一个 API.
    /// </summary>
    public static CubismOffscreenManager_OpenGLES2 GetInstance()
    {
        if (s_instance is null)
        {
            lock (s_lock)
            {
                s_instance ??= new CubismOffscreenManager_OpenGLES2();
            }
        }

        return s_instance;
    }

    /// <summary>
    /// 释放默认管理器 (兼容旧单例入口). 
    /// 渲染器自有的管理器由渲染器自己释放, 与这里无关.
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
    /// 当前管理器绑定的 OpenGL API. 未绑定时为 null. 
    /// </summary>
    public OpenGLApi? Api => _gl;

    /// <summary>
    /// 获取可用的渲染目标 (官方 GetOffscreenRenderTarget). 
    /// </summary>
    /// <param name="width">幅</param>
    /// <param name="height">高度</param>
    /// <returns>可用的渲染目标</returns>
    /// <exception cref="InvalidOperationException">管理器未绑定 OpenGLApi 时抛出</exception>
    public CubismRenderTarget_OpenGLES2 GetOffscreenRenderTarget(uint width, uint height)
    {
        OpenGLApi gl = Api ?? throw new InvalidOperationException(
            "[Live2D OpenGL]CubismOffscreenManager_OpenGLES2 has no OpenGLApi. " +
            "Create the manager with new CubismOffscreenManager_OpenGLES2(gl).");

        // 更新使用数
        UpdateRenderTargetCount();

        // 若有未被使用的资源容器则返回它
        CubismRenderTarget_OpenGLES2? offscreenRenderTarget = GetUnusedOffscreenRenderTarget();
        if (offscreenRenderTarget is not null)
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

        // new T() 满足 ICubismOffscreenManager<T> 的 new() 约束, 创建时未绑定 API, 
        // 在这里补齐, 保证渲染目标只使用本管理器的上下文. 
        offscreenRenderTarget.BindApi(gl);

        if (!offscreenRenderTarget.CreateRenderTarget((int)width, (int)height))
        {
            WarnInvalidRenderTarget(width, height);
        }
        return offscreenRenderTarget;
    }

    /// <summary>
    /// 绑定 OpenGLApi. 
    /// 用构造函数创建的管理器已经绑定, 这里只对 GetInstance 返回的默认管理器有意义;
    /// 已绑定其它 API 时抛出异常, 防止两个上下文互相覆盖. 
    /// </summary>
    /// <param name="gl">OpenGL API</param>
    /// <exception cref="ArgumentNullException">gl 为 null 时抛出</exception>
    /// <exception cref="InvalidOperationException">已绑定其它 OpenGLApi 时抛出</exception>
    public void BindApi(OpenGLApi gl)
    {
        if (gl is null)
        {
            throw new ArgumentNullException(nameof(gl),
                "[Live2D OpenGL]CubismOffscreenManager_OpenGLES2 requires an OpenGLApi instance.");
        }

        if (_gl is null)
        {
            _gl = gl;
            return;
        }

        if (ReferenceEquals(_gl, gl) is false)
        {
            throw new InvalidOperationException(
                "[Live2D OpenGL]CubismOffscreenManager_OpenGLES2 is already bound to another OpenGLApi instance.");
        }
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
