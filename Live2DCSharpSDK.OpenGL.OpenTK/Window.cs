using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.OpenGL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ErrorCode = OpenTK.Graphics.OpenGL4.ErrorCode;

namespace Live2DCSharpSDK.OpenTK;

public class Window : GameWindow
{
    private LAppDelegate lapp;

    /// <summary>
    /// 已经记录过的 GL 错误码. 同一错误码只记录一次, 避免逐帧刷屏.
    /// </summary>
    private readonly HashSet<ErrorCode> _reportedGlErrors = [];
    public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
    {
        var version = GL.GetString(StringName.Version);

        var cubismAllocator = new LAppAllocator();
        var cubismOption = new CubismOption()
        {
            LogFunction = Console.WriteLine,
            LoggingLevel = LAppDefine.CubismLoggingLevel
        };
        CubismFramework.StartUp(cubismAllocator, cubismOption);
    }

    protected unsafe override void OnLoad()
    {
        base.OnLoad();
        lapp = new LAppDelegateOpenGL(new OpenTKApi(this))
        {
            BGColor = new(0, 1, 0, 1)
        };

        // 模型路径不硬编码, 由 App 层的 ModelResourceLocator 解析
        // (上游 sample 直接写死 "F:\\live2d\\Resources", 本仓库改为参数化).
        // 找不到时也不抛出异常, 继续以空场景启动.
        if (ModelResourceLocator.TryResolve(null, out string modelDir, out string modelName))
        {
            try
            {
                var model = lapp.Live2dManager.LoadModel(modelDir, modelName);
                model.ModelMatrix.TranslateX(0.0f);
                Console.WriteLine($"[Live2D App]model loaded: {Path.Combine(modelDir, modelName)}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Live2D App]model load failed: {e.GetType().Name}: {e.Message}");
            }
        }
        else
        {
            Console.WriteLine($"[Live2D App]未找到模型, 以空场景启动: {modelName}");
        }

        // 高 DPI 或 Retina 下 client 与 framebuffer 尺寸可能不同, 记录一次便于核对口径.
        Console.WriteLine($"[Live2D App]window: client={ClientSize.X}x{ClientSize.Y}, framebuffer={FramebufferSize.X}x{FramebufferSize.Y}");
        ApplyFramebufferViewport();
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        // 每帧同步 viewport: 窗口移动/跨显示器切换 DPI 时也不至于沿用旧尺寸.
        ApplyFramebufferViewport();

        lapp.Run((float)UpdateTime);

        ErrorCode code = GL.GetError();
        if (code is not ErrorCode.NoError && _reportedGlErrors.Add(code))
        {
            // 旧实现此处 throw new Exception(): 既无消息也无错误码, 日志里无从定位.
            // 现在按错误码各记录一次并继续渲染, 避免单帧 GL 错误直接终止宿主.
            CubismLog.Warning($"[Live2D App]OpenGL error detected: {code} (0x{(int)code:X4}).");
        }

        SwapBuffers();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        lapp?.Resize();

        ApplyFramebufferViewport();
    }

    /// <summary>
    /// 把 viewport 设为 framebuffer 的实际像素尺寸.
    /// 高 DPI 屏或 Retina 上 framebuffer 与窗口逻辑尺寸不同, 统一以 framebuffer 为准.
    /// </summary>
    private void ApplyFramebufferViewport()
    {
        var size = FramebufferSize;
        if (size.X > 0 && size.Y > 0)
        {
            GL.Viewport(0, 0, size.X, size.Y);
        }
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        KeyboardState input = KeyboardState;

        if (input.IsKeyDown(Keys.Escape))
        {
            Close();
        }
    }

    /// <summary>
    /// 窗口结束时的清理(相当于官方 Samples/OpenGL/.../LAppLive2DManager.cpp:77 的释放路径).
    /// 在 context 仍为 current 期间, 按模型(renderer) → 进程共享 offscreen manager 的顺序释放.
    /// </summary>
    protected override void OnUnload()
    {
        base.OnUnload();

        try
        {
            lapp?.Dispose();

            // 释放进程共享的 offscreen manager(官方 LAppLive2DManager::ReleaseInstance()).
            // 渲染器侧不再负责全局释放 manager, 因此应用退出时这里是唯一的释放点.
            CubismOffscreenManager_OpenGLES2.ReleaseInstance();
            Console.WriteLine("[Live2D App]CubismOffscreenManager_OpenGLES2.ReleaseInstance()");
        }
        catch (Exception e)
        {
            // 结束路径中即使 context 已失效也不妨碍进程退出(GL 名字由 OS 回收).
            Console.WriteLine($"[Live2D App]release on close failed: {e.GetType().Name}: {e.Message}");
        }
    }
}
