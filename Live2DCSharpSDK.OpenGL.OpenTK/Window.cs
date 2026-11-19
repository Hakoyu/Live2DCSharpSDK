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
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        lapp.Run((float)UpdateTime);

        var code = GL.GetError();
        if (code != ErrorCode.NoError)
        {
            throw new Exception();
        }

        SwapBuffers();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        lapp.Resize();

        GL.Viewport(0, 0, e.Width, e.Height);
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
