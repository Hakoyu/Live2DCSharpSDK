using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// OpenGL 用的渲染目标 (官方 OpenGL/CubismRenderTarget_OpenGLES2.hpp:48-145 / .cpp:28-185). 
/// 由旧的 CubismOffscreenSurface_OpenGLES2 迁移到 R5 的名称与契约.
/// 继承 Framework 侧的 CubismRenderTarget, 并对 ICubismOffscreenManager&lt;T&gt;, 
/// 提供无参构造, 以满足 new() 约束.
/// </summary>
public class CubismRenderTarget_OpenGLES2 : CubismRenderTarget
{
    /// <summary>
    /// 无参构造函数使用的默认 API. 
    /// 官方直接调用全局 GL 函数, 因此作为对等处理, 在创建渲染器时设置. 
    /// </summary>
    private static OpenGLApi? s_defaultApi;

    private readonly OpenGLApi? _gl;

    /// <summary>
    /// 无参构造函数 (为满足 ICubismOffscreenManager&lt;T&gt; 的 new() 约束). 
    /// </summary>
    public CubismRenderTarget_OpenGLES2()
    {
    }

    /// <summary>
    /// 带参数的构造函数
    /// </summary>
    /// <param name="gl">OpenGL API</param>
    public CubismRenderTarget_OpenGLES2(OpenGLApi gl)
    {
        _gl = gl;
    }

    /// <summary>
    /// 设置无参构造函数使用的默认 API. 
    /// </summary>
    internal static void SetDefaultApi(OpenGLApi gl)
    {
        s_defaultApi = gl;
    }

    private OpenGLApi GL => _gl ?? s_defaultApi
        ?? throw new InvalidOperationException("[Live2D OpenGL]CubismRenderTarget_OpenGLES2: OpenGLApi is not set.");

    /// <summary>
    /// 作为渲染目标的地址
    /// </summary>
    public int RenderTexture { get; private set; }
    /// <summary>
    /// 绘制时作为纹理使用的地址
    /// </summary>
    public int ColorBuffer { get; private set; }

    /// <summary>
    /// 旧帧缓冲
    /// </summary>
    private int _oldFBO;
    /// <summary>
    /// 旧视口(对应官方 _oldViewport[4]).
    /// </summary>
    private readonly int[] _oldViewport = new int[4];

    /// <summary>
    /// Create 时指定的宽度 (本地兼容的 int 公开)
    /// </summary>
    public int BufferWidth => _bufferWidth;
    /// <summary>
    /// Create 时指定的高度 (本地兼容的 int 公开)
    /// </summary>
    public int BufferHeight => _bufferHeight;
    /// <summary>
    /// 是否为由参数设置的色彩缓冲? 
    /// </summary>
    private bool _isColorBufferInherited;

    private int _bufferWidth;
    private int _bufferHeight;

    /// <summary>
    /// 复制缓冲的内容 (官方 CopyBuffer, 用于不支持 texture barrier 的环境). 
    /// </summary>
    /// <param name="src">复制源</param>
    /// <param name="dst">复制目标</param>
    public static void CopyBuffer(CubismRenderTarget_OpenGLES2 src, CubismRenderTarget_OpenGLES2 dst)
    {
        // 任一方创建失败时不执行拷贝.
        // 若把 RenderTexture == 0 作为写入目标, 会转送到默认帧缓冲 (画面). 
        if (!src.IsValid() || !dst.IsValid())
        {
            return;
        }

        OpenGLApi gl = src.GL;
        gl.GetIntegerv(gl.GL_FRAMEBUFFER_BINDING, out int framebuffer);

        gl.BindFramebuffer(gl.GL_READ_FRAMEBUFFER, src.RenderTexture);
        gl.BindFramebuffer(gl.GL_DRAW_FRAMEBUFFER, dst.RenderTexture);

        gl.BlitFramebuffer(0, 0, src._bufferWidth, src._bufferHeight,
            0, 0, dst._bufferWidth, dst._bufferHeight, gl.GL_COLOR_BUFFER_BIT, gl.GL_NEAREST);

        gl.BindFramebuffer(gl.GL_FRAMEBUFFER, framebuffer);
    }

    /// <summary>
    /// 朝向指定的绘制目标开始绘制
    /// </summary>
    /// <param name="restoreFBO">大于等于 0 时, 在 EndDraw 中用该值 glBindFramebuffer</param>
    public void BeginDraw(int restoreFBO = -1)
    {
        if (RenderTexture == 0)
        {
            return;
        }

        // 记住后台缓冲的表面
        if (restoreFBO < 0)
        {
            GL.GetIntegerv(GL.GL_FRAMEBUFFER_BINDING, out _oldFBO);
        }
        else
        {
            _oldFBO = restoreFBO;
        }

        // 记住视口 (官方 .cpp:57)
        GL.GetIntegerv(GL.GL_VIEWPORT, _oldViewport);

        // 将用于 Mask 的 RenderTexture 设为 active
        GL.BindFramebuffer(GL.GL_FRAMEBUFFER, RenderTexture);
    }

    /// <summary>
    /// 描画終了
    /// </summary>
    public void EndDraw()
    {
        if (RenderTexture == 0)
        {
            return;
        }

        // 还原绘制目标
        GL.BindFramebuffer(GL.GL_FRAMEBUFFER, _oldFBO);
        // 视口也一并还原 (官方 .cpp:72, CHANGELOG "viewport 保存, 恢复的改善")
        GL.Viewport(_oldViewport[0], _oldViewport[1], _oldViewport[2], _oldViewport[3]);
    }

    /// <summary>
    /// 渲染目标的清除
    /// 调用时需在 BeginDraw 之后调用
    /// </summary>
    /// <param name="r">赤(0.0~1.0)</param>
    /// <param name="g">緑(0.0~1.0)</param>
    /// <param name="b">青(0.0~1.0)</param>
    /// <param name="a">α(0.0~1.0)</param>
    public void Clear(float r, float g, float b, float a)
    {
        // 创建失败(RenderTexture == 0)时绘制目标不确定(沿用上一个 FBO), 因此不执行任何操作.
        if (RenderTexture == 0)
        {
            return;
        }

        // 清除 Mask
        GL.ClearColor(r, g, b, a);
        GL.Clear(GL.GL_COLOR_BUFFER_BIT);
    }

    /// <summary>
    /// 创建渲染目标(对应官方 CreateRenderTarget).
    /// </summary>
    /// <param name="displayBufferWidth">要创建的缓冲宽度</param>
    /// <param name="displayBufferHeight">要创建的缓冲高度</param>
    /// <param name="colorBuffer">非 0 时, 将 colorBuffer 用作像素存储区域</param>
    /// <returns>创建成功且帧缓冲完整 (GL_FRAMEBUFFER_COMPLETE) 时为 true</returns>
    /// <remarks>
    /// 创建后立即用 glCheckFramebufferStatus 校验完整性.
    /// 不完整时销毁 GL 资源并返回 false (为 false 时
    /// <see cref="IsValid"/> 为 false, <see cref="RenderTexture"/> / <see cref="ColorBuffer"/> 为 0). 
    /// 宽度, 高度保留为请求值, 因此以相同尺寸再次调用时不会重新创建
    /// (避免在创建持续失败的环境中每帧重建导致日志刷屏). 
    /// </remarks>
    public unsafe bool CreateRenderTarget(int displayBufferWidth, int displayBufferHeight, int colorBuffer = 0)
    {
        // 一旦削除
        DestroyRenderTarget();

        // 尺寸小于等于 0 时 glTexImage2D 会返回 GL_INVALID_VALUE,
        // 并留下一个未分配存储却仍被视为成功的"不完整 FBO". 因此在调用 GL 之前就拦截.
        if (displayBufferWidth <= 0 || displayBufferHeight <= 0)
        {
            CubismLog.Warning(
                "[Live2D OpenGL]CreateRenderTarget failed: invalid size {0}x{1}. No framebuffer was created.",
                displayBufferWidth, displayBufferHeight);
            return false;
        }

        // 新生成
        if (colorBuffer == 0)
        {
            ColorBuffer = GL.GenTexture();

            GL.BindTexture(GL.GL_TEXTURE_2D, ColorBuffer);
            GL.TexImage2D(GL.GL_TEXTURE_2D, 0, GL.GL_RGBA, displayBufferWidth, displayBufferHeight, 0, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, 0);
            // 官方 .cpp:98-106 (桌面 GL 为 REPEAT / NEAREST)
            GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_S, GL.GL_REPEAT);
            GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_T, GL.GL_REPEAT);
            GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, GL.GL_NEAREST);
            GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, GL.GL_NEAREST);
            GL.BindTexture(GL.GL_TEXTURE_2D, 0);

            _isColorBufferInherited = false;
        }
        else
        {
            // 使用指定的对象
            ColorBuffer = colorBuffer;

            _isColorBufferInherited = true;
        }

        GL.GetIntegerv(GL.GL_FRAMEBUFFER_BINDING, out int tmpFramebufferObject);

        int ret = GL.GenFramebuffer();
        GL.BindFramebuffer(GL.GL_FRAMEBUFFER, ret);
        GL.FramebufferTexture2D(GL.GL_FRAMEBUFFER, GL.GL_COLOR_ATTACHMENT0, GL.GL_TEXTURE_2D, ColorBuffer, 0);

        // 附加纹理后(FBO 仍绑定中)校验帧缓冲完整性.
        // 向不完整的 FBO 绘制会使绘制结果未定义, 因此在此处按失败处理. 
        int status = GL.CheckFramebufferStatus(GL.GL_FRAMEBUFFER);

        GL.BindFramebuffer(GL.GL_FRAMEBUFFER, tmpFramebufferObject);

        RenderTexture = ret;

        _bufferWidth = displayBufferWidth;
        _bufferHeight = displayBufferHeight;

        if (status != GL.GL_FRAMEBUFFER_COMPLETE)
        {
            // 输出状态码 / 尺寸 / 附件信息, 销毁 GL 资源并返回失败.
            ReportIncompleteFramebuffer(status, displayBufferWidth, displayBufferHeight);

            // 失败后删除 (继承的纹理不会被 DestroyRenderTarget 清除, 因此只解除引用)
            DestroyRenderTarget();
            ColorBuffer = 0;
            _isColorBufferInherited = false;

            return false;
        }

        // 成功
        return true;
    }

    /// <summary>
    /// 帧缓冲不完整时的诊断日志.
    /// 输出状态码 / 请求尺寸 / 附件(绑定到 COLOR_ATTACHMENT0 的纹理)以及 GL_MAX_TEXTURE_SIZE.
    /// </summary>
    /// <remarks>
    /// 既有的 <see cref="CubismLog.Error"/> 在输出后会抛出异常, 因此对以 bool 返回失败的此 API, 
    /// 为不牵连调用方 (绘制循环), 降级为 Warning 输出
    /// (只降级调用点, 不改变 CubismLog.Error 本体).
    /// </remarks>
    private void ReportIncompleteFramebuffer(int status, int width, int height)
    {
        GL.GetIntegerv(GL.GL_MAX_TEXTURE_SIZE, out int maxTextureSize);

        CubismLog.Warning(
            "[Live2D OpenGL]CreateRenderTarget failed: framebuffer is not complete. " +
            "status=0x{0:X4} ({1}), size={2}x{3}, GL_MAX_TEXTURE_SIZE={4}, " +
            "attachment=GL_COLOR_ATTACHMENT0<-GL_TEXTURE_2D(id={5}, inherited={6})",
            status, GetFramebufferStatusName(status), width, height, maxTextureSize,
            ColorBuffer, _isColorBufferInherited);
    }

    /// <summary>
    /// 将 glCheckFramebufferStatus 的返回值转换为可读名称.
    /// </summary>
    private string GetFramebufferStatusName(int status)
    {
        if (status == GL.GL_FRAMEBUFFER_COMPLETE) return "GL_FRAMEBUFFER_COMPLETE";
        if (status == GL.GL_FRAMEBUFFER_INCOMPLETE_ATTACHMENT) return "GL_FRAMEBUFFER_INCOMPLETE_ATTACHMENT";
        if (status == GL.GL_FRAMEBUFFER_INCOMPLETE_MISSING_ATTACHMENT) return "GL_FRAMEBUFFER_INCOMPLETE_MISSING_ATTACHMENT";
        if (status == GL.GL_FRAMEBUFFER_INCOMPLETE_DIMENSIONS) return "GL_FRAMEBUFFER_INCOMPLETE_DIMENSIONS";
        if (status == GL.GL_FRAMEBUFFER_UNSUPPORTED) return "GL_FRAMEBUFFER_UNSUPPORTED";
        if (status == GL.GL_FRAMEBUFFER_UNDEFINED) return "GL_FRAMEBUFFER_UNDEFINED";

        return "UNKNOWN";
    }

    /// <summary>
    /// 删除 CubismRenderTarget (官方 DestroyRenderTarget)
    /// </summary>
    public override void DestroyRenderTarget()
    {
        if (!_isColorBufferInherited && (ColorBuffer != 0))
        {
            GL.DeleteTexture(ColorBuffer);
            ColorBuffer = 0;
        }

        if (RenderTexture != 0)
        {
            GL.DeleteFramebuffer(RenderTexture);
            RenderTexture = 0;
        }
    }

    /// <summary>
    /// 渲染纹理成员的访问器(对应官方 GetRenderTexture).
    /// </summary>
    public int GetRenderTexture()
    {
        return RenderTexture;
    }

    /// <summary>
    /// 颜色缓冲成员的访问器(对应官方 GetColorBuffer).
    /// </summary>
    public int GetColorBuffer()
    {
        return ColorBuffer;
    }

    /// <summary>
    /// 当前是否有效 (官方 IsValid)
    /// </summary>
    public override bool IsValid()
    {
        return RenderTexture != 0;
    }

    /// <summary>
    /// 获取缓冲宽度 (官方 GetBufferWidth)
    /// </summary>
    public override uint GetBufferWidth()
    {
        return (uint)_bufferWidth;
    }

    /// <summary>
    /// 获取缓冲高度 (官方 GetBufferHeight)
    /// </summary>
    public override uint GetBufferHeight()
    {
        return (uint)_bufferHeight;
    }

    /// <summary>
    /// 旧帧缓冲的获取(对应官方 GetOldFBO).
    /// </summary>
    public int GetOldFBO()
    {
        return _oldFBO;
    }
}
