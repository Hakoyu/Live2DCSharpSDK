using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Math;

namespace Live2DCSharpSDK.App;

/// <summary>
/// 绘制类
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
public abstract class LAppView(LAppDelegate lapp)
{
    /// <summary>
    /// 触摸管理器
    /// </summary>
    private readonly TouchManager _touchManager = new();

    /// <summary>
    /// 从设备坐标到屏幕坐标的矩阵
    /// </summary>
    private readonly CubismMatrix44 _deviceToScreen = new();

    /// <summary>
    /// viewMatrix
    /// </summary>
    private readonly CubismViewMatrix _viewMatrix = new();

    public abstract void RenderPre();
    public abstract void RenderPost();

    /// <summary>
    /// 执行初始化.
    /// </summary>
    /// <param name="width">窗口宽度</param>
    /// <param name="height">窗口高度</param>
    /// <remarks>
    /// 参照官方 5-r.5 的 LAppView::Initialize(int width, int height)(LAppView.cpp)改为带参数的实现.
    /// 调用方只有 LAppDelegate(InitApp / Resize), 宿主不会触碰它.
    /// </remarks>
    public void Initialize(int width, int height)
    {
        if (width == 0 || height == 0)
        {
            return;
        }

        // 以纵向尺寸为基准
        float ratio = (float)width / height;
        float left = -ratio;
        float right = ratio;
        float bottom = LAppDefine.ViewLogicalLeft;
        float top = LAppDefine.ViewLogicalRight;

        _viewMatrix.SetScreenRect(left, right, bottom, top); // 与设备对应的画面范围. X 的左端, X 的右端, Y 的下端, Y 的上端
        _viewMatrix.Scale(LAppDefine.ViewScale, LAppDefine.ViewScale);

        _deviceToScreen.LoadIdentity(); // 尺寸变化等情况必须重置
        if (width > height)
        {
            float screenW = MathF.Abs(right - left);
            _deviceToScreen.ScaleRelative(screenW / width, -screenW / width);
        }
        else
        {
            float screenH = MathF.Abs(top - bottom);
            _deviceToScreen.ScaleRelative(screenH / height, -screenH / height);
        }
        _deviceToScreen.TranslateRelative(-width * 0.5f, -height * 0.5f);

        // 设置显示范围
        _viewMatrix.MaxScale = LAppDefine.ViewMaxScale; // 极限放大倍率
        _viewMatrix.MinScale = LAppDefine.ViewMinScale; // 极限缩小倍率

        // 可显示的最大范围
        _viewMatrix.SetMaxScreenRect(
            LAppDefine.ViewLogicalMaxLeft,
            LAppDefine.ViewLogicalMaxRight,
            LAppDefine.ViewLogicalMaxBottom,
            LAppDefine.ViewLogicalMaxTop
        );
    }

    /// <summary>
    /// 执行绘制.
    /// </summary>
    internal void Render()
    {
        RenderPre();

        var manager = lapp.Live2dManager;
        manager.ViewMatrix.SetMatrix(_viewMatrix);

        // Cubism 更新与绘制
        manager.OnUpdate();

        RenderPost();
    }

    /// <summary>
    /// 触摸按下时调用.
    /// </summary>
    /// <param name="pointX">屏幕 X 坐标</param>
    /// <param name="pointY">屏幕 Y 坐标</param>
    public void OnTouchesBegan(float pointX, float pointY)
    {
        _touchManager.TouchesBegan(pointX, pointY);
        CubismLog.Debug($"[Live2D App]touchesBegan x:{pointX:#.##} y:{pointY:#.##}");
    }

    /// <summary>
    /// 触摸过程中指针移动时调用.
    /// </summary>
    /// <param name="pointX">屏幕 X 坐标</param>
    /// <param name="pointY">屏幕 Y 坐标</param>
    public void OnTouchesMoved(float pointX, float pointY)
    {
        float viewX = TransformViewX(_touchManager.GetX());
        float viewY = TransformViewY(_touchManager.GetY());

        _touchManager.TouchesMoved(pointX, pointY);

        lapp.Live2dManager.OnDrag(viewX, viewY);
    }

    /// <summary>
    /// 触摸结束时调用.
    /// </summary>
    public void OnTouchesEnded(float _, float __)
    {
        // 触摸结束
        var live2DManager = lapp.Live2dManager;
        live2DManager.OnDrag(0.0f, 0.0f);
        // 单击
        float x = _deviceToScreen.TransformX(_touchManager.GetX()); // 获取经过逻辑坐标转换的坐标.
        float y = _deviceToScreen.TransformY(_touchManager.GetY()); // 获取经过逻辑坐标转换的坐标.
        CubismLog.Debug($"[Live2D App]touchesEnded x:{x:#.##} y:{y:#.##}");
        live2DManager.OnTap(x, y);
    }

    /// <summary>
    /// 把 X 坐标转换为 View 坐标.
    /// </summary>
    /// <param name="deviceX">设备 X 坐标</param>
    public float TransformViewX(float deviceX)
    {
        float screenX = _deviceToScreen.TransformX(deviceX); // 获取经过逻辑坐标转换的坐标.
        return _viewMatrix.InvertTransformX(screenX); // 放大, 缩小, 平移后的值.
    }

    /// <summary>
    /// 把 Y 坐标转换为 View 坐标.
    /// </summary>
    /// <param name="deviceY">设备 Y 坐标</param>
    public float TransformViewY(float deviceY)
    {
        float screenY = _deviceToScreen.TransformY(deviceY); // 获取经过逻辑坐标转换的坐标.
        return _viewMatrix.InvertTransformY(screenY); // 放大, 缩小, 平移后的值.
    }

    /// <summary>
    /// 把 X 坐标转换为 Screen 坐标.
    /// </summary>
    /// <param name="deviceX">设备 X 坐标</param>
    public float TransformScreenX(float deviceX)
    {
        return _deviceToScreen.TransformX(deviceX);
    }

    /// <summary>
    /// 把 Y 坐标转换为 Screen 坐标.
    /// </summary>
    /// <param name="deviceY">设备 Y 坐标</param>
    public float TransformScreenY(float deviceY)
    {
        return _deviceToScreen.TransformY(deviceY);
    }

    /// <summary>
    /// 在把模型绘制到另一个渲染目标的示例中,
    /// 决定绘制时的 α 值.
    /// </summary>
    public static float GetSpriteAlpha(int assign)
    {
        // 根据 assign 的数值适当决定
        // 官方 5-r.5 的 LAppView.cpp:307 取 0.4f(旧实现为 0.25f). 本移植没有精灵绘制, 只保持取值与官方一致.
        float alpha = 0.4f + assign * 0.5f; // 作为示例给 α 加上适当的差异
        if (alpha > 1.0f)
        {
            alpha = 1.0f;
        }
        if (alpha < 0.1f)
        {
            alpha = 0.1f;
        }

        return alpha;
    }
}
