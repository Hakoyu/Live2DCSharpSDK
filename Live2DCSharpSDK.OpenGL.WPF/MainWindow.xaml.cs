using System.IO;
using System.Windows;
using System.Windows.Threading;
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

    /// <summary>
    /// 本次运行使用的 Cubism 上下文. 窗口构造时创建, 关闭时由 delegate(或本窗口的清理路径)释放.
    /// </summary>
    private CubismContext? _context;

    /// <summary>
    /// 是否已经输出过窗口/framebuffer 尺寸诊断(只输出一次).
    /// </summary>
    private bool _loggedSize;

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
        // 上下文在模型加载前创建并 StartUp, 生命周期交给 delegate(关闭时统一释放).
        _context = CubismContext.Create(cubismAllocator, cubismOption);
        _context.StartUp();

        var settings = new GLWpfControlSettings
        {
            MajorVersion = 3,
            MinorVersion = 3,
            // 按设备像素渲染: 高 DPI 下 framebuffer 尺寸 = 控件逻辑尺寸 x 缩放率,
            // 与 Avalonia 宿主的 RenderScaling 口径一致, 避免整幅画面被 WPF 放大而发虚.
            UseDeviceDpi = true
        };
        GLControl.Start(settings);
        lapp = new LAppDelegateOpenGL(new OpenTKWPFApi(GLControl))
        {
            BGColor = new(0, 1, 0, 1),
            // 上下文交给 delegate 管理, 关闭时由 delegate 统一释放.
            Context = _context
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
            Log($"[Live2D App]未找到模型, 以空场景启动: {modelName}");
        }
    }

    /// <summary>
    /// 可选的日志文件路径(环境变量 LIVE2D_LOG_FILE). 设置后每条日志同时追加到该文件.
    /// 实测: 模型加载完成后, WinExe 宿主进程的 stdout 句柄会失效(Console.WriteLine 抛 "句柄无效"),
    /// 无控制台场景下后续日志全部丢失; 需要排障时用这个文件兜底.
    /// </summary>
    private static readonly string? LogFilePath = Environment.GetEnvironmentVariable("LIVE2D_LOG_FILE");

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

        if (LogFilePath is null)
        {
            return;
        }

        try
        {
            File.AppendAllText(LogFilePath, message + Environment.NewLine);
        }
        catch (Exception)
        {
            // 日志文件不可写时保持静默, 不影响宿主主流程.
        }
    }

    private void GLControl_Render(TimeSpan obj)
    {
        UpdateViewport();

        if (_loggedSize is false && GLControl.FrameBufferWidth > 0)
        {
            _loggedSize = true;
            Log($"[Live2D App]window: logical={GLControl.ActualWidth}x{GLControl.ActualHeight}, framebuffer={GLControl.FrameBufferWidth}x{GLControl.FrameBufferHeight}");
        }

        GL.ClearColor(Color4.Blue);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        lapp.Run((float)obj.TotalSeconds);
    }

    private void GLControl_Resized(object sender, SizeChangedEventArgs e)
    {
        if (lapp is null)
            return;
        lapp.Resize();
        UpdateViewport();
    }

    /// <summary>
    /// 把 viewport 设为 GLWpfControl 的 framebuffer 实际像素尺寸.
    /// 高 DPI 下它等于控件逻辑尺寸乘以缩放率, 与 LAppDelegate 的窗口尺寸保持同一口径.
    /// </summary>
    private void UpdateViewport()
    {
        int width = GLControl.FrameBufferWidth;
        int height = GLControl.FrameBufferHeight;
        if (width > 0 && height > 0)
        {
            GL.Viewport(0, 0, width, height);
        }
    }

    /// <summary>
    /// 窗口结束时的清理(相当于官方 Samples/OpenGL/.../LAppLive2DManager.cpp:77 的释放路径).
    /// GLWpfControl 没有 deinit 事件, 因此在 Closed 中显式把 context 置为 current 后再释放.
    /// </summary>
    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        try
        {
            var context = GLControl.Context;
            context?.MakeCurrent();

            // 先摘掉事件, 避免释放过程中还有渲染/尺寸回调进来.
            GLWpfControl control = GLControl;
            control.Render -= GLControl_Render;
            control.SizeChanged -= GLControl_Resized;

            // 1) App 侧: 模型(renderer / texture / moc) -> 进程共享 offscreen manager.
            lapp?.Dispose();
            Log($"[Live2D App]models after dispose: {lapp?.Live2dManager?.GetModelNum() ?? 0}");

            // 防御: delegate 未创建时也释放上下文(Dispose 幂等, 已释放时为空操作).
            _context?.Dispose();
            _context = null;

            CubismOffscreenManager_OpenGLES2.ReleaseInstance();
            Log("[Live2D App]CubismOffscreenManager_OpenGLES2.ReleaseInstance()");

            // 2) 控件侧: 把控件移出可视树, 触发 Unloaded -> GLWpfControlRenderer.ReleaseFramebufferResources(),
            //    它自己的 framebuffer / D3D 互操作 surface 才会被释放(lapp.Dispose + ReleaseInstance 不负责这些).
            //    Unloaded 是异步派发的, 这里驱动 UI 队列等它完成, 此时 GL context 仍为 current.
            BorderOpenTK.Child = null;
            for (int i = 0; i < 100 && GLControl.FrameBufferWidth > 0; i++)
            {
                Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Thread.Sleep(5);
            }

            Log($"[Live2D App]GLWpfControl framebuffer released: {GLControl.FrameBufferWidth == 0}");

            context?.MakeNoneCurrent();

            // 注: 不调用 GLControl.Dispose(). GLWpfControlRenderer.Dispose 会继续调用
            // DxGlContext.Dispose -> Wgl.DXCloseDeviceNV, 历史上实测引发 AccessViolation(0xC0000005);
            // 上面的 Unloaded 路径只做 ReleaseFramebufferResources, 不碰 D3D 互操作设备,
            // 因此 GL 资源已释放, D3D 侧交给 WPF/OS 的销毁路径.
        }
        catch (Exception ex)
        {
            // 结束路径中即使 context 已失效也不妨碍进程退出.
            Log($"[Live2D App]release on close failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}