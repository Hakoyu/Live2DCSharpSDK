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

        // モデルのパスはハードコードせず、App 層の ModelResourceLocator で解決する
        // （上流 sample は "F:\\live2d\\Resources" 直書きだった。本仓库はパラメータ化）。
        // 見つからない場合も例外にせず、空のシーンで起動を続ける。
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
            Console.WriteLine($"[Live2D App]モデル未検出のため空のシーンで起動します: {modelName}");
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
    /// ウィンドウ終了時の後片付け（公式 Samples/OpenGL/.../LAppLive2DManager.cpp:77 の解放経路に相当）。
    /// コンテキストが current なうちにモデル（renderer）→ プロセス共有 offscreen manager の順で解放する。
    /// </summary>
    protected override void OnUnload()
    {
        base.OnUnload();

        try
        {
            lapp?.Dispose();

            // プロセス共有の offscreen manager を解放する（官方 LAppLive2DManager::ReleaseInstance()）。
            // renderer 側は G2-F3 対応で manager のグローバル解放を行わないため、アプリ終了時はここが唯一の解放点。
            CubismOffscreenManager_OpenGLES2.ReleaseInstance();
            Console.WriteLine("[Live2D App]CubismOffscreenManager_OpenGLES2.ReleaseInstance()");
        }
        catch (Exception e)
        {
            // 終了経路でコンテキストが既に無効な場合でもプロセス終了を妨げない（GL 名の解放は OS が回収する）。
            Console.WriteLine($"[Live2D App]release on close failed: {e.GetType().Name}: {e.Message}");
        }
    }
}
