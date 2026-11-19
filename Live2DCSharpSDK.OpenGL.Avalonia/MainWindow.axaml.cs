using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Motion;
using Live2DCSharpSDK.OpenGL;

namespace Live2DCSharpSDK.Avalonia;

public partial class MainWindow : Window
{
    private FpsTimer _renderTimer;

    public MainWindow()
    {
        InitializeComponent();

        Button1.Click += Button1_Click;

        // V6（操作/表情/物理の切替）用のボタン。処理は GL コントロール側に置き、ここは配線だけ。
        MotionButton.Click += MotionButton_Click;
        ExpressionButton.Click += ExpressionButton_Click;
        PhysicsButton.Click += PhysicsButton_Click;

        Closing += MainWindow_Closing;

        _renderTimer = new(GL);

        GLTop.PointerPressed += GLTop_PointerPressed;
        GLTop.PointerMoved += GLTop_PointerMoved;
        GLTop.PointerReleased += GLTop_PointerReleased;

        // Cubism SDK の初期化
        var cubismOption = new CubismOption()
        {
            LogFunction = Console.WriteLine,
            LoggingLevel = LAppDefine.CubismLoggingLevel
        };
        CubismFramework.StartUp(new LAppAllocator(), cubismOption);
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        _renderTimer.Close();
    }

    private void Button1_Click(object? sender, RoutedEventArgs e)
    {
        GL.RequestNextFrameRendering();
    }

    private void MotionButton_Click(object? sender, RoutedEventArgs e)
    {
        GL.StartNextMotion();
    }

    private void ExpressionButton_Click(object? sender, RoutedEventArgs e)
    {
        GL.StartNextExpression();
    }

    private void PhysicsButton_Click(object? sender, RoutedEventArgs e)
    {
        bool enabled = GL.TogglePhysics();
        PhysicsButton.Content = enabled ? "物理 ON" : "物理 OFF";
    }

    private void GLTop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(GLTop).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // ドラッグ中も GLTop でイベントを受け続ける（Look の切替検証用）。
        e.Pointer.Capture(GLTop);

        var (x, y) = GetPointerPosition(e);
        GL.NotifyMousePressed(x, y);
    }

    private void GLTop_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(GLTop).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var (x, y) = GetPointerPosition(e);
        GL.NotifyMouseMoved(x, y);
    }

    private void GLTop_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var (x, y) = GetPointerPosition(e);
        GL.NotifyMouseReleased(x, y);

        e.Pointer.Capture(null);
    }

    /// <summary>
    /// ポインタ座標を取得する。
    /// 座標系は Avalonia の DIP。LAppView のデバイス→スクリーン行列も
    /// AvaloniaApi.GetWindowSize（Bounds = DIP）で作られるため、ここで拡大率を掛けてはいけない。
    /// </summary>
    private (double X, double Y) GetPointerPosition(PointerEventArgs e)
    {
        var point = e.GetPosition(GLTop);
        return (point.X, point.Y);
    }
}

public class OpenGlPageControl : OpenGlControlBase
{
    private LAppDelegate? lapp;

    private string _info = string.Empty;
    private string _status = string.Empty;
    private DateTime time;

    /// <summary>
    /// 現在ロードしているモデル（未ロードなら null）
    /// </summary>
    private LAppModel? _model;

    private int _motionGroupIndex;
    private int _motionIndex;
    private int _expressionIndex;

    public static readonly DirectProperty<OpenGlPageControl, string> InfoProperty =
        AvaloniaProperty.RegisterDirect<OpenGlPageControl, string>("Info", o => o.Info, (o, v) => o.Info = v);

    public static readonly DirectProperty<OpenGlPageControl, string> StatusProperty =
        AvaloniaProperty.RegisterDirect<OpenGlPageControl, string>("Status", o => o.Status, (o, v) => o.Status = v);

    public OpenGlPageControl()
    {

    }

    public string Info
    {
        get => _info;
        private set => SetAndRaise(InfoProperty, ref _info, value);
    }

    /// <summary>
    /// 動作/表情/物理の切替結果を表示するためのテキスト
    /// </summary>
    public string Status
    {
        get => _status;
        private set => SetAndRaise(StatusProperty, ref _status, value);
    }

    private static void CheckError(GlInterface gl)
    {
        int err;
        while ((err = gl.GetError()) != GlConsts.GL_NO_ERROR)
            Console.WriteLine(err);
    }

    protected override unsafe void OnOpenGlInit(GlInterface gl)
    {
        CheckError(gl);

        Info = $"Renderer: {gl.GetString(GlConsts.GL_RENDERER)} Version: {gl.GetString(GlConsts.GL_VERSION)}";

        try
        {
            lapp = new LAppDelegateOpenGL(new AvaloniaApi(this, gl))
            {
                BGColor = new(0, 0, 0, 0)
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Status = $"初期化失敗: {e.Message}";
            return;
        }

        // モデルのパスはハードコードせず、App 層の ModelResourceLocator で解決する
        // （上流 sample は "F:\\live2d\\Resources" 直書きだった。本仓库はパラメータ化）。
        // 見つからない場合も例外にせず、空のシーンで起動を続ける。
        if (ModelResourceLocator.TryResolve(null, out string modelDir, out string modelName))
        {
            try
            {
                _model = lapp.Live2dManager.LoadModel(modelDir, modelName);
                Status = $"モデル: {modelName}";
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Status = $"モデル読込失敗: {e.Message}";
            }
        }
        else
        {
            Status = $"モデル未検出（空のシーン）: {modelName}";
        }

        CheckError(gl);
    }

    protected override void OnOpenGlDeinit(GlInterface GL)
    {
        lapp?.Dispose();
        lapp = null;
        _model = null;

        // アプリ終了時にプロセス共有の offscreen manager を解放する
        // （官方 Samples/OpenGL/Demo/proj.win.cmake/src/LAppLive2DManager.cpp:77
        //   LAppLive2DManager::ReleaseInstance() の CubismOffscreenManager_OpenGLES2::ReleaseInstance() に相当）。
        // ここは GL コンテキストが current な唯一の後片付けポイントなので、必ず lapp.Dispose()（= 各 renderer の解放）の後に呼ぶ。
        CubismOffscreenManager_OpenGLES2.ReleaseInstance();
        Console.WriteLine("[Live2D App]CubismOffscreenManager_OpenGLES2.ReleaseInstance()");
    }

    /// <summary>
    /// マウス押下（LAppDelegate の glfw 相当 API に橋渡しする）
    /// </summary>
    public void NotifyMousePressed(double x, double y)
    {
        if (lapp == null)
        {
            return;
        }

        lapp.OnMouseCallBack((float)x, (float)y);
        lapp.OnMouseCallBack(true);
    }

    /// <summary>
    /// マウス移動（ドラッグ）
    /// </summary>
    public void NotifyMouseMoved(double x, double y)
    {
        lapp?.OnMouseCallBack((float)x, (float)y);
    }

    /// <summary>
    /// マウス解放（タップ判定もここで走る）
    /// </summary>
    public void NotifyMouseReleased(double x, double y)
    {
        if (lapp == null)
        {
            return;
        }

        lapp.OnMouseCallBack((float)x, (float)y);
        lapp.OnMouseCallBack(false);
    }

    /// <summary>
    /// 次のモーションを順に再生する（V6「動作切替」用の App 拡張）。
    /// </summary>
    public void StartNextMotion()
    {
        if (_model == null)
        {
            Status = "モデル未ロード";
            return;
        }

        var groups = _model.MotionGroups;
        if (groups.Count == 0)
        {
            Status = "モーション無し";
            return;
        }

        _motionGroupIndex %= groups.Count;
        string group = groups[_motionGroupIndex];
        int count = _model.GetMotionCount(group);
        if (count == 0)
        {
            Status = $"グループ {group} は空";
            return;
        }

        int index = _motionIndex % count;
        _model.StartMotion(group, index, MotionPriority.PriorityForce);
        Status = $"動作: {group}_{index}";

        _motionIndex++;
        if (_motionIndex >= count)
        {
            _motionIndex = 0;
            _motionGroupIndex = (_motionGroupIndex + 1) % groups.Count;
        }
    }

    /// <summary>
    /// 次の表情を順に適用する（V6「表情切替」用）。
    /// </summary>
    public void StartNextExpression()
    {
        if (_model == null)
        {
            Status = "モデル未ロード";
            return;
        }

        var expressions = _model.Expressions;
        if (expressions.Count == 0)
        {
            Status = "表情無し";
            return;
        }

        int index = _expressionIndex % expressions.Count;
        _model.SetExpression(expressions[index]);
        Status = $"表情: {expressions[index]}";
        _expressionIndex = (index + 1) % expressions.Count;
    }

    /// <summary>
    /// 物理演算の ON/OFF を切り替える（V6「物理切替」用の App 拡張）。切替後の状態を返す。
    /// </summary>
    public bool TogglePhysics()
    {
        if (_model == null)
        {
            Status = "モデル未ロード";
            return false;
        }

        if (!_model.HasPhysics)
        {
            Status = "物理データ無し";
            return false;
        }

        bool enabled = !_model.PhysicsEnabled;
        _model.PhysicsEnabled = enabled;
        Status = enabled ? "物理: ON" : "物理: OFF";
        return enabled;
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (lapp == null)
        {
            return;
        }

        int x = (int)Bounds.Width;
        int y = (int)Bounds.Height;

        if (VisualRoot is TopLevel window)
        {
            var screen = window.RenderScaling;
            x = (int)(Bounds.Width * screen);
            y = (int)(Bounds.Height * screen);
        }
        gl.Viewport(0, 0, x, y);

        // D-06: モデルのレンダーターゲットは「実ピクセル」で設定する（viewport と同じ座標系）。
        // LAppDelegate 側のウィンドウサイズは DIP なので、ここで上書きする。
        if (x > 0 && y > 0)
        {
            lapp.Live2dManager.SetRenderTargetSize(x, y);
        }

        var now = DateTime.Now;
        float span = 0;
        if (time.Ticks == 0)
        {
            time = now;
        }
        else
        {
            span = (float)(now - time).TotalSeconds;
            time = now;
        }
        lapp.Run(span);
        CheckError(gl);
    }
}
