using System.IO;
using System.Windows;
using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.OpenGL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Wpf;

namespace Live2DCSharpSDK.WPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    GLWpfControl GLControl;
    private LAppDelegate lapp;

    public MainWindow()
    {
        InitializeComponent();
        GLControl = new();

        GLControl.SizeChanged += GLControl_Resized;
        GLControl.Render += GLControl_Render;
        Closed += MainWindow_Closed;
        BorderOpenTK.Child = GLControl;
        //CompositionTarget.Rendering += CompositionTarget_Rendering;

        var cubismAllocator = new LAppAllocator();
        var cubismOption = new CubismOption()
        {
            LogFunction = Log,
            LoggingLevel = LAppDefine.CubismLoggingLevel
        };
        CubismFramework.StartUp(cubismAllocator, cubismOption);

        var settings = new GLWpfControlSettings
        {
            MajorVersion = 3,
            MinorVersion = 3
        };
        GLControl.Start(settings);
        lapp = new LAppDelegateOpenGL(new OpenTKWPFApi(GLControl))
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
                lapp.Live2dManager.LoadModel(modelDir, modelName);
                Log($"[Live2D App]model loaded: {Path.Combine(modelDir, modelName)}");
            }
            catch (Exception e)
            {
                Log($"[Live2D App]model load failed: {e}");
            }
        }
        else
        {
            Log($"[Live2D App]モデル未検出のため空のシーンで起動します: {modelName}");
        }
    }

    /// <summary>
    /// 日志输出. 以 WinExe(无控制台)启动时, Console.WriteLine 可能因
    /// "句柄无效"(ERROR_INVALID_HANDLE)而抛出异常, 因此
    /// 把日志输出包起来, 避免导致进程退出.
    /// </summary>
    /// <param name="message">要输出的字符串</param>
    private static void Log(string message)
    {
        try
        {
            Console.WriteLine(message);
        }
        catch (Exception e)
        {
            // 控制台句柄无效的环境中(例如未重定向启动 WinExe),
            // 会同时出现 IOException / UnauthorizedAccessException, 因此在此吞掉.
            System.Diagnostics.Debug.WriteLine($"{message} (console write failed: {e.GetType().Name})");
        }
    }

    private void GLControl_Render(TimeSpan obj)
    {
        GL.ClearColor(Color4.Blue);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        lapp.Run((float)obj.TotalSeconds);
    }

    private void GLControl_Resized(object sender, SizeChangedEventArgs e)
    {
        if (lapp == null)
            return;
        lapp.Resize();
        GL.Viewport(0, 0, (int)GLControl.ActualWidth, (int)GLControl.ActualHeight);
    }

    /// <summary>
    /// 窗口结束时的清理(相当于官方 Samples/OpenGL/.../LAppLive2DManager.cpp:77 的释放路径).
    /// GLWpfControl 没有 deinit 事件, 因此在 Closed 中显式把 context 置为 current 后再释放.
    /// </summary>
    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        try
        {
            var context = GLControl?.Context;
            context?.MakeCurrent();

            lapp?.Dispose();

            // 释放进程共享的 offscreen manager(官方 LAppLive2DManager::ReleaseInstance()).
            CubismOffscreenManager_OpenGLES2.ReleaseInstance();
            Log("[Live2D App]CubismOffscreenManager_OpenGLES2.ReleaseInstance()");

            context?.MakeNoneCurrent();

            // 注: 不调用 GLControl.Dispose(). OpenTK.GLWpfControl 4.3.1 中
            // GLWpfControlRenderer.Dispose → DxGlContext.Dispose → DXCloseDeviceNV 会
            // 引发 AccessViolation(实测 0xC0000005, 退出码 139), 因此 D3D 互操作的清理
            // 交给 WPF/OS 的销毁路径处理(GL 资源已由 lapp.Dispose + ReleaseInstance 释放).
        }
        catch (Exception ex)
        {
            // 结束路径中即使 context 已失效也不妨碍进程退出.
            Log($"[Live2D App]release on close failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}