using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.App;

/// <summary>
/// 应用程序类.
/// 负责 Cubism SDK 的管理.
/// </summary>
public abstract class LAppDelegate : IDisposable
{
    /// <summary>
    /// 纹理管理器
    /// </summary>
    public LAppTextureManager TextureManager { get; private set; }

    public LAppLive2DManager Live2dManager { get; private set; }

    /// <summary>
    /// View 信息
    /// </summary>
    public LAppView View { get; protected set; }

    public CubismTextureColor BGColor { get; set; } = new(0, 0, 0, 0);

    /// <summary>
    /// 是否正在点击
    /// </summary>
    private bool _captured;
    /// <summary>
    /// 鼠标 X 坐标
    /// </summary>
    private float _mouseX;
    /// <summary>
    /// 鼠标 Y 坐标
    /// </summary>
    private float _mouseY;

    /// <summary>
    /// 由 Initialize 函数设置的窗口宽度
    /// </summary>
    public int WindowWidth { get; protected set; }
    /// <summary>
    /// 由 Initialize 函数设置的窗口高度
    /// </summary>
    public int WindowHeight { get; protected set; }

    public abstract void OnUpdatePre();
    public abstract void GetWindowSize(out int width, out int height);
    public abstract CubismRenderer CreateRenderer(CubismModel model);
    public abstract TextureInfo CreateTexture(LAppModel model, int index, int width, int height, IntPtr data);

    public void InitApp()
    {
        TextureManager = new LAppTextureManager(this);

        // 记录窗口尺寸
        GetWindowSize(out int width, out int height);
        WindowWidth = width;
        WindowHeight = height;
        // AppView 的初始化
        View.Initialize(width, height);

        //load model
        Live2dManager = new LAppLive2DManager(this);

        LAppPal.DeltaTime = 0;
    }

    /// <summary>
    /// 释放资源.
    /// </summary>
    public void Dispose()
    {
        Live2dManager.Dispose();
    }

    public void Resize()
    {
        GetWindowSize(out int width, out int height);
        if ((WindowWidth != width || WindowHeight != height) && width > 0 && height > 0)
        {
            // 先保存尺寸
            WindowWidth = width;
            WindowHeight = height;
            // AppView 的初始化
            View.Initialize(width, height);

            // 将模型的渲染目标尺寸与实际尺寸对齐(官方 LAppDelegate.cpp:148.
            // 示例层未启用 USE_RENDER_TARGET, 但 CubismRenderer.SetRenderTargetSize 会
            // 影响 Blend 模型使用的渲染目标分辨率, 因此与官方一样向下传递).
            Live2dManager?.SetRenderTargetSize(width, height);
        }
    }

    /// <summary>
    /// Need skip
    /// </summary>
    /// <returns></returns>
    public abstract bool RunPre();
    public abstract void RunPost();

    /// <summary>
    /// 执行处理.
    /// </summary>
    public void Run(float tick)
    {
        Resize();

        // 时间更新
        LAppPal.DeltaTime = tick;

        if (RunPre())
        {
            return;
        }

        // 绘制更新
        View.Render();

        RunPost();
    }

    /// <summary>
    /// 供 OpenGL 使用的 glfwSetMouseButtonCallback 回调函数.
    /// </summary>
    /// <param name="button">按钮种类</param>
    /// <param name="action">执行结果</param>
    public void OnMouseCallBack(bool press)
    {
        if (press)
        {
            _captured = true;
            View.OnTouchesBegan(_mouseX, _mouseY);
        }
        else
        {
            if (_captured)
            {
                _captured = false;
                View.OnTouchesEnded(_mouseX, _mouseY);
            }
        }
    }

    /// <summary>
    /// 供 OpenGL 使用的 glfwSetCursorPosCallback 回调函数.
    /// </summary>
    /// <param name="x">x 坐标</param>
    /// <param name="y">y 坐标</param>
    public void OnMouseCallBack(float x, float y)
    {
        // 按下时的点击判定(OnTouchesBegan/Ended)使用上一次的坐标,
        // 因此即使尚未捕获也要始终更新坐标(旧实现只在捕获后更新,
        // 宿主在按下时传入坐标也会被判定为 (0,0)).
        _mouseX = x;
        _mouseY = y;

        if (!_captured)
        {
            return;
        }

        View.OnTouchesMoved(_mouseX, _mouseY);
    }
}
