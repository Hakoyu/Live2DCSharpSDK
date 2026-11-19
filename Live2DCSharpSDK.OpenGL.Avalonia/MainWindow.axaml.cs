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

        // 用于切换动作/表情/物理的按钮, 处理逻辑放在 GL 控件侧, 这里只做接线.
        MotionButton.Click += MotionButton_Click;
        ExpressionButton.Click += ExpressionButton_Click;
        PhysicsButton.Click += PhysicsButton_Click;

        Closing += MainWindow_Closing;

        _renderTimer = new(GL);

        GLTop.PointerPressed += GLTop_PointerPressed;
        GLTop.PointerMoved += GLTop_PointerMoved;
        GLTop.PointerReleased += GLTop_PointerReleased;
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

        // 拖动过程中也由 GLTop 持续接收事件 (用于验证 Look 的切换). 
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
    /// 获取指针坐标. 
    /// 坐标系为 Avalonia 的 DIP. LAppView 的设备坐标→屏幕坐标矩阵也是
    /// 由 AvaloniaApi.GetWindowSize (Bounds = DIP) 构建, 因此此处不能乘以缩放率. 
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

    /// <summary>
    /// 本次运行使用的 Cubism 上下文, 与 GL 控件同生命周期(初始化时创建, 反初始化时释放).
    /// </summary>
    private CubismContext? _context;

    private string _info = string.Empty;
    private string _status = string.Empty;
    private DateTime time;

    /// <summary>
    /// 当前加载的模型 (未加载时为 null)
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
    /// 用于显示动作/表情/物理切换结果的文本
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
            // Cubism 上下文与 GL 控件同生命周期: 先创建并 StartUp, 再构造 delegate;
            // 模型加载发生在 delegate 构造之后, 此时上下文必然已启动.
            var cubismAllocator = new LAppAllocator();
            var cubismOption = new CubismOption()
            {
                LogFunction = Console.WriteLine,
                LoggingLevel = LAppDefine.CubismLoggingLevel
            };
            _context = CubismContext.Create(cubismAllocator, cubismOption);
            _context.StartUp();

            lapp = new LAppDelegateOpenGL(new AvaloniaApi(this, gl))
            {
                BGColor = new(0, 0, 0, 0),
                // 上下文交给 delegate 管理, 反初始化时由 delegate 统一释放.
                Context = _context
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            // delegate 未构造成功时上下文不会再被它释放, 在这里收尾.
            _context?.Dispose();
            _context = null;
            Status = $"初期化失敗: {e.Message}";
            return;
        }

        // 模型路径不硬编码, 由 App 层的 ModelResourceLocator 解析
        // (上游 sample 是 "F:\live2d\Resources" 直写. 本仓库已参数化). 
        // 即使找不到也不抛出异常, 以空场景继续启动. 
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

        // 应用退出时释放进程共享的 offscreen manager
        // (官方 Samples/OpenGL/Demo/proj.win.cmake/src/LAppLive2DManager.cpp:77
        //   相当于 LAppLive2DManager::ReleaseInstance() 中的 CubismOffscreenManager_OpenGLES2::ReleaseInstance()). 
        // 这里是 GL 上下文为 current 的唯一收尾点, 因此必须在 lapp.Dispose() (= 各 renderer 的释放) 之后调用. 
        CubismOffscreenManager_OpenGLES2.ReleaseInstance();
        Console.WriteLine("[Live2D App]CubismOffscreenManager_OpenGLES2.ReleaseInstance()");

        // 防御: delegate 未创建时也释放上下文(Dispose 幂等, 已释放时为空操作).
        _context?.Dispose();
        _context = null;
    }

    /// <summary>
    /// 鼠标按下 (桥接到 LAppDelegate 的 glfw 对应 API)
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
    /// 鼠标移动 (拖动)
    /// </summary>
    public void NotifyMouseMoved(double x, double y)
    {
        lapp?.OnMouseCallBack((float)x, (float)y);
    }

    /// <summary>
    /// 鼠标释放 (点击判定也在这里执行)
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
    /// 依次播放下一个动作(动作切换用的 App 扩展).
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
    /// 依次应用下一个表情(表情切换用的 App 扩展).
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
    /// 切换物理演算的开关(物理切换用的 App 扩展), 返回切换后的状态.
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

        // 模型的渲染目标按实际像素设置(与 viewport 使用同一坐标系).
        // LAppDelegate 侧的窗口尺寸为 DIP, 因此在此处覆盖. 
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
