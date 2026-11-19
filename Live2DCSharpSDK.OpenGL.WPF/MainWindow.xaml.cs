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
        // モデルのパスはハードコードせず、App 層の ModelResourceLocator で解決する
        // （上流 sample は "F:\\live2d\\Resources" 直書きだった。本仓库はパラメータ化）。
        // 見つからない場合も例外にせず、空のシーンで起動を続ける。
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
    /// ログ出力。WinExe（コンソール無し）で起動した場合、Console.WriteLine が
    /// "句柄无效"（ERROR_INVALID_HANDLE）で例外になることがあるため、
    /// ログ出力でプロセスを落とさないように包む。
    /// </summary>
    /// <param name="message">出力する文字列</param>
    private static void Log(string message)
    {
        try
        {
            Console.WriteLine(message);
        }
        catch (Exception e)
        {
            // コンソールハンドルが無効な環境（WinExe をリダイレクト無しで起動した場合など）では
            // IOException / UnauthorizedAccessException の両方が来るため、ここで握りつぶす。
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
    /// ウィンドウ終了時の後片付け（公式 Samples/OpenGL/.../LAppLive2DManager.cpp:77 の解放経路に相当）。
    /// GLWpfControl には deinit イベントが無いため Closed でコンテキストを明示的に current にしてから解放する。
    /// </summary>
    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        try
        {
            var context = GLControl?.Context;
            context?.MakeCurrent();

            lapp?.Dispose();

            // プロセス共有の offscreen manager を解放する（官方 LAppLive2DManager::ReleaseInstance()）。
            CubismOffscreenManager_OpenGLES2.ReleaseInstance();
            Log("[Live2D App]CubismOffscreenManager_OpenGLES2.ReleaseInstance()");

            context?.MakeNoneCurrent();

            // 注: GLControl.Dispose() は呼ばない。OpenTK.GLWpfControl 4.3.1 の
            // GLWpfControlRenderer.Dispose → DxGlContext.Dispose → DXCloseDeviceNV が
            // AccessViolation（実測 0xC0000005, 終了コード 139）を起こすため、D3D 相互運用の後片付けは
            // WPF/OS の破棄経路に任せる（GL リソースは lapp.Dispose + ReleaseInstance で解放済み）。
        }
        catch (Exception ex)
        {
            // 終了経路でコンテキストが既に無効な場合でもプロセス終了を妨げない。
            Log($"[Live2D App]release on close failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}