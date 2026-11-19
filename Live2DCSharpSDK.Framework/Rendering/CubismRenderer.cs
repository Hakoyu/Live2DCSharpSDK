using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// 处理模型绘制的渲染器
/// 在子类中描述与环境相关的绘制指令
/// </summary>
public abstract class CubismRenderer
{
    /// <summary>
    /// 纹理各向异性过滤的参数
    /// </summary>
    public float Anisotropy { get; set; }
    /// <summary>
    /// 渲染目标的模型
    /// </summary>
    public CubismModel Model { get; private set; }
    /// <summary>
    /// 如果已预乘 α 则为 true
    /// </summary>
    public bool IsPremultipliedAlpha { get; set; }
    /// <summary>
    /// 如果剔除有效则为 true
    /// </summary>
    public bool IsCulling { get; set; }
    /// <summary>
    /// 为 false 时, 汇总绘制遮罩; 为 true 时, 每次绘制部件都重写遮罩
    /// </summary>
    public bool UseHighPrecisionMask { get; set; }

    /// <summary>
    /// 模型本身的颜色(RGBA)
    /// </summary>
    public CubismTextureColor ModelColor = new();

    /// <summary>
    /// Offscreen 的宽度 (官方 _modelRenderTargetWidth, 由 SetRenderTargetSize 更新)
    /// </summary>
    protected uint _modelRenderTargetWidth;
    /// <summary>
    /// Offscreen 的高度 (官方 _modelRenderTargetHeight, 由 SetRenderTargetSize 更新)
    /// </summary>
    protected uint _modelRenderTargetHeight;

    /// <summary>
    /// Model-View-Projection 行列
    /// </summary>
    private readonly CubismMatrix44 _mvpMatrix4x4 = new();

    /// <summary>
    /// 生成并获取渲染器的实例
    /// </summary>
    public CubismRenderer(CubismModel model)
    {
        _mvpMatrix4x4.LoadIdentity();
        Model = model ?? throw new Exception("model is null");
    }

    /// <summary>
    /// 释放渲染器的实例
    /// </summary>
    public abstract void Dispose();

    /// <summary>
    /// 绘制模型
    /// </summary>
    public void DrawModel()
    {
        /**
         * 请在 DoDrawModel 的绘制前与绘制后调用以下函数.
         * - SaveProfile();
         * - RestoreProfile();
         * 这是通过保存与复原渲染器的绘制设置,
         * 用于恢复到模型绘制直前状态的处理.
         */

        SaveProfile();

        DoDrawModel();

        RestoreProfile();
    }

    /// <summary>
    /// 设置 Model-View-Projection 矩阵
    /// 数组会被复制, 因此原数组可在外部销毁
    /// </summary>
    /// <param name="matrix4x4">Model-View-Projection 行列</param>
    public void SetMvpMatrix(CubismMatrix44 matrix4x4)
    {
        _mvpMatrix4x4.SetMatrix(matrix4x4.Tr);
    }

    /// <summary>
    /// 获取 Model-View-Projection 矩阵
    /// </summary>
    /// <returns>Model-View-Projection 行列</returns>
    public CubismMatrix44 GetMvpMatrix()
    {
        return _mvpMatrix4x4;
    }

    /// <summary>
    /// 计算考虑透明度的模型颜色.
    /// </summary>
    /// <param name="opacity">透明度</param>
    /// <returns>RGBA 的颜色信息</returns>
    public CubismTextureColor GetModelColorWithOpacity(float opacity)
    {
        CubismTextureColor modelColorRGBA = new(ModelColor);
        modelColorRGBA.A *= opacity;
        if (IsPremultipliedAlpha)
        {
            modelColorRGBA.R *= modelColorRGBA.A;
            modelColorRGBA.G *= modelColorRGBA.A;
            modelColorRGBA.B *= modelColorRGBA.A;
        }
        return modelColorRGBA;
    }

    /// <summary>
    /// 设置模型的颜色.
    /// 各颜色在 0.0f 到 1.0f 之间指定(1.0f 为标准状态).
    /// </summary>
    /// <param name="red">红色通道的值</param>
    /// <param name="green">绿色通道的值</param>
    /// <param name="blue">蓝色通道的值</param>
    /// <param name="alpha">α 通道的值</param>
    public void SetModelColor(float red, float green, float blue, float alpha)
    {
        if (red < 0.0f) red = 0.0f;
        else if (red > 1.0f) red = 1.0f;

        if (green < 0.0f) green = 0.0f;
        else if (green > 1.0f) green = 1.0f;

        if (blue < 0.0f) blue = 0.0f;
        else if (blue > 1.0f) blue = 1.0f;

        if (alpha < 0.0f) alpha = 0.0f;
        else if (alpha > 1.0f) alpha = 1.0f;

        ModelColor.R = red;
        ModelColor.G = green;
        ModelColor.B = blue;
        ModelColor.A = alpha;
    }

    /// <summary>
    /// 设置 Offscreen 的尺寸 (官方 CubismRenderer::SetRenderTargetSize, CubismRenderer.cpp:76-80).
    /// 本地在创建模型时由宿主生成渲染器, 因此由宿主的 OnRender
    /// 每帧按实际尺寸调用.
    /// </summary>
    /// <param name="width">绘制模型的缓冲区宽度</param>
    /// <param name="height">绘制模型的缓冲区高度</param>
    public void SetRenderTargetSize(uint width, uint height)
    {
        _modelRenderTargetWidth = width;
        _modelRenderTargetHeight = height;
    }

    /// <summary>
    /// 使用 blend 模式时提高遮罩精度 (官方 CubismRenderer.cpp:42-52).
    /// 官方在 Initialize 内引用 model->IsBlendModeEnabled(), 但本地的
    /// CubismModel.IsBlendModeEnabled() 判定结果改由此处作为参数传入,
    /// 由宿主在初始化时提供.
    /// </summary>
    /// <param name="isBlendModeEnabled">如果模型使用 blend 模式则为 true</param>
    protected void ApplyBlendModeMaskPrecision(bool isBlendModeEnabled)
    {
        if (!isBlendModeEnabled)
        {
            return;
        }

        UseHighPrecisionMask = true;
        CubismLog.Info("[Live2D SDK]This model uses a high-resolution mask because it operates in blend mode.");
    }

    /// <summary>
    /// 模型绘制的实现
    /// </summary>
    protected abstract void DoDrawModel();

    /// <summary>
    /// 保持模型绘制直前渲染器的状态
    /// </summary>
    protected abstract void SaveProfile();

    /// <summary>
    /// 恢复模型绘制直前渲染器的状态
    /// </summary>
    protected abstract void RestoreProfile();

    /// <summary>
    /// 模型绘制直前的 Offscreen 设置 (官方 pure virtual, CubismRenderer.hpp:284).
    /// 在派生类的 DoDrawModel 开头调用 (官方 OpenGL 实现为 CubismRenderer_OpenGLES2.cpp:724).
    /// </summary>
    protected abstract void BeforeDrawModelRenderTarget();

    /// <summary>
    /// 模型绘制后的 Offscreen 设置 (官方 pure virtual, CubismRenderer.hpp:289).
    /// 在派生类的 DoDrawModel 末尾调用 (官方 OpenGL 实现为 CubismRenderer_OpenGLES2.cpp:788).
    /// </summary>
    protected abstract void AfterDrawModelRenderTarget();
}
