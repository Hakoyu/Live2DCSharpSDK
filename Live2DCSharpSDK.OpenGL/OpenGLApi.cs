namespace Live2DCSharpSDK.OpenGL;

public abstract class OpenGLApi
{
    public abstract bool AlwaysClear { get; }
    public abstract bool IsES2 { get; }
    public abstract bool IsPhoneES2 { get; }

    public readonly int GL_FALSE = 0;
    public readonly int GL_ZERO = 0;
    public readonly int GL_ONE = 1;
    public readonly int GL_TRIANGLES = 0x0004;
    public readonly int GL_DEPTH_BUFFER_BIT = 0x0100;
    public readonly int GL_ONE_MINUS_SRC_COLOR = 0x0301;
    public readonly int GL_SRC_ALPHA = 0x0302;
    public readonly int GL_ONE_MINUS_SRC_ALPHA = 0x0303;
    public readonly int GL_DST_COLOR = 0x0306;
    public readonly int GL_CCW = 0x0901;
    public readonly int GL_CULL_FACE = 0x0B44;
    public readonly int GL_FRONT_FACE = 0x0B46;
    public readonly int GL_DEPTH_TEST = 0x0B71;
    public readonly int GL_STENCIL_TEST = 0x0B90;
    public readonly int GL_VIEWPORT = 0x0BA2;
    public readonly int GL_BLEND = 0x0BE2;
    public readonly int GL_SCISSOR_TEST = 0x0C11;
    public readonly int GL_COLOR_WRITEMASK = 0x0C23;
    public readonly int GL_TEXTURE_2D = 0x0DE1;
    public readonly int GL_UNSIGNED_BYTE = 0x1401;
    public readonly int GL_UNSIGNED_SHORT = 0x1403;
    public readonly int GL_FLOAT = 0x1406;
    public readonly int GL_RGBA = 0x1908;
    public readonly int GL_LINEAR = 0x2601;
    public readonly int GL_LINEAR_MIPMAP_LINEAR = 0x2703;
    public readonly int GL_TEXTURE_MAG_FILTER = 0x2800;
    public readonly int GL_TEXTURE_MIN_FILTER = 0x2801;
    public readonly int GL_TEXTURE_WRAP_S = 0x2802;
    public readonly int GL_TEXTURE_WRAP_T = 0x2803;
    public readonly int GL_COLOR_BUFFER_BIT = 0x4000;
    public readonly int GL_TEXTURE_BINDING_2D = 0x8069;
    public readonly int GL_BLEND_DST_RGB = 0x80C8;
    public readonly int GL_BLEND_SRC_RGB = 0x80C9;
    public readonly int GL_BLEND_DST_ALPHA = 0x80CA;
    public readonly int GL_BLEND_SRC_ALPHA = 0x80CB;
    public readonly int GL_CLAMP_TO_EDGE = 0x812F;
    public readonly int GL_TEXTURE0 = 0x84C0;
    public readonly int GL_TEXTURE1 = 0x84C1;

    /// <summary>
    /// 纹理单元 2(blend 用).
    /// </summary>
    public readonly int GL_TEXTURE2 = 0x84C2;

    /// <summary>
    /// 纹理重复方式(对应官方 CubismRenderTarget_OpenGLES2.cpp:102).
    /// </summary>
    public readonly int GL_REPEAT = 0x2901;

    /// <summary>
    /// 最近邻过滤(对应官方 .cpp:105).
    /// </summary>
    public readonly int GL_NEAREST = 0x2600;
    public readonly int GL_ACTIVE_TEXTURE = 0x84E0;
    public readonly int GL_TEXTURE_MAX_ANISOTROPY_EXT = 0x84FE;
    public readonly int GL_VERTEX_ATTRIB_ARRAY_ENABLED = 0x8622;
    public readonly int GL_ARRAY_BUFFER = 0x8892;
    public readonly int GL_ELEMENT_ARRAY_BUFFER = 0x8893;
    public readonly int GL_ARRAY_BUFFER_BINDING = 0x8894;
    public readonly int GL_ELEMENT_ARRAY_BUFFER_BINDING = 0x8895;
    public readonly int GL_FRAGMENT_SHADER = 0x8B30;
    public readonly int GL_VERTEX_SHADER = 0x8B31;
    public readonly int GL_COMPILE_STATUS = 0x8B81;
    public readonly int GL_LINK_STATUS = 0x8B82;
    public readonly int GL_VALIDATE_STATUS = 0x8B83;
    public readonly int GL_INFO_LOG_LENGTH = 0x8B84;
    public readonly int GL_CURRENT_PROGRAM = 0x8B8D;
    public readonly int GL_STATIC_DRAW = 0x88E4;
    public readonly int GL_FRAMEBUFFER_BINDING = 0x8CA6;

    /// <summary>
    /// 读取用帧缓冲(glBlitFramebuffer 用).
    /// </summary>
    public readonly int GL_READ_FRAMEBUFFER = 0x8CA8;

    /// <summary>
    /// 写入用帧缓冲(glBlitFramebuffer 用).
    /// </summary>
    public readonly int GL_DRAW_FRAMEBUFFER = 0x8CA9;
    public readonly int GL_COLOR_ATTACHMENT0 = 0x8CE0;
    public readonly int GL_FRAMEBUFFER = 0x8D40;

    /// <summary>
    /// 帧缓冲完整即可绘制的状态值(官方 glCheckFramebufferStatus).
    /// </summary>
    public readonly int GL_FRAMEBUFFER_COMPLETE = 0x8CD5;

    /// <summary>
    /// 附件不完整(诊断用, ES2 / 桌面 GL 通用).
    /// </summary>
    public readonly int GL_FRAMEBUFFER_INCOMPLETE_ATTACHMENT = 0x8CD6;

    /// <summary>
    /// 没有任何附件(诊断用).
    /// </summary>
    public readonly int GL_FRAMEBUFFER_INCOMPLETE_MISSING_ATTACHMENT = 0x8CD7;

    /// <summary>
    /// 附件尺寸不一致(诊断用, 仅 ES2).
    /// </summary>
    public readonly int GL_FRAMEBUFFER_INCOMPLETE_DIMENSIONS = 0x8CD9;

    /// <summary>
    /// 实现不支持该组合(诊断用).
    /// </summary>
    public readonly int GL_FRAMEBUFFER_UNSUPPORTED = 0x8CDD;

    /// <summary>
    /// 未定义默认帧缓冲(诊断用, 桌面 GL).
    /// </summary>
    public readonly int GL_FRAMEBUFFER_UNDEFINED = 0x8219;

    /// <summary>
    /// 纹理最大尺寸(诊断用, 用于判断是否超限).
    /// </summary>
    public readonly int GL_MAX_TEXTURE_SIZE = 0x0D33;

    public abstract void Viewport(int x, int y, int w, int h);
    public abstract void ClearColor(float r, float g, float b, float a);
    public abstract void Clear(int bit);
    public abstract void Enable(int bit);
    public abstract void Disable(int bit);
    public abstract void EnableVertexAttribArray(int index);
    public abstract void DisableVertexAttribArray(int index);
    public abstract void GetIntegerv(int bit, out int data);
    public abstract void GetIntegerv(int bit, int[] data);
    public abstract void ActiveTexture(int bit);
    public abstract void GetVertexAttribiv(int index, int bit, out int data);
    public abstract bool IsEnabled(int bit);
    public abstract void GetBooleanv(int bit, bool[] data);
    public abstract void UseProgram(int index);
    public abstract void FrontFace(int data);
    public abstract void ColorMask(bool a, bool b, bool c, bool d);
    public abstract void BindBuffer(int bit, int index);
    public abstract void BindTexture(int bit, int index);
    public abstract void BlendFuncSeparate(int a, int b, int c, int d);
    public abstract void DeleteProgram(int index);
    public abstract int GetAttribLocation(int index, string attr);
    public abstract int GetUniformLocation(int index, string uni);
    public abstract void Uniform1i(int index, int data);
    public abstract unsafe void VertexAttribPointer(
        int index,
        int length,
        int type,
        bool b,
        int size,
        nint arr
    );
    public abstract void Uniform4f(int index, float a, float b, float c, float d);
    public abstract void UniformMatrix4fv(int index, int length, bool b, float[] data);
    public abstract int CreateProgram();
    public abstract void AttachShader(int a, int b);
    public abstract void DeleteShader(int index);
    public abstract void DetachShader(int index, int data);
    public abstract int CreateShader(int type);
    public abstract void ShaderSource(int a, string source);
    public abstract void CompileShader(int index);
    public abstract unsafe void GetShaderiv(int index, int type, int* length);
    public abstract void GetShaderInfoLog(int index, out string log);
    public abstract void LinkProgram(int index);
    public abstract unsafe void GetProgramiv(int index, int type, int* length);
    public abstract unsafe void GetProgramInfoLog(int index, out string log);
    public abstract void ValidateProgram(int index);
    public abstract unsafe void DrawElements(int type, int count, int type1, nint arry);
    public abstract void BindVertexArrayOES(int data);
    public abstract void TexParameterf(int type, int type1, float value);
    public abstract void BindFramebuffer(int type, int data);
    public abstract int GenTexture();
    public abstract void TexImage2D(
        int type,
        int a,
        int type1,
        int w,
        int h,
        int size,
        int type2,
        int type3,
        IntPtr data
    );
    public abstract void TexParameteri(int a, int b, int c);
    public abstract int GenFramebuffer();
    public abstract void FramebufferTexture2D(int a, int b, int c, int buff, int data);

    /// <summary>
    /// 检查当前绑定的帧缓冲是否完整(官方 glCheckFramebufferStatus).
    /// 返回值不为 <see cref="GL_FRAMEBUFFER_COMPLETE"/> 时不能用作绘制目标.
    /// 检查对象是当前绑定到 <paramref name="target"/> 的帧缓冲, 因此
    /// 调用方需要在绑定目标 FBO 的状态下调用.
    /// </summary>
    /// <param name="target">GL_FRAMEBUFFER / GL_READ_FRAMEBUFFER / GL_DRAW_FRAMEBUFFER</param>
    public abstract int CheckFramebufferStatus(int target);
    public abstract void DeleteTexture(int data);
    public abstract void DeleteFramebuffer(int fb);

    /// <summary>
    /// 在帧缓冲之间做位块传输(对应官方 glBlitFramebuffer).
    /// </summary>
    public abstract void BlitFramebuffer(
        int srcX0,
        int srcY0,
        int srcX1,
        int srcY1,
        int dstX0,
        int dstY0,
        int dstX1,
        int dstY1,
        int mask,
        int filter
    );

    /// <summary>
    /// 删除缓冲对象 (§2.F 漏点①, 官方 glDeleteBuffers)
    /// </summary>
    public abstract void DeleteBuffer(int index);

    /// <summary>
    /// 删除顶点数组对象 (§2.F 漏点①, 官方 glDeleteVertexArrays)
    /// </summary>
    public abstract void DeleteVertexArray(int index);
    public abstract void BlendFunc(int a, int b);
    public abstract void GetWindowSize(out int w, out int h);
    public abstract void GenerateMipmap(int a);
    public abstract void ClearDepthf(float data);
    public abstract int GetError();
    public abstract int GenBuffer();
    public abstract void BufferData(int type, int v1, nint v2, int type1);
    public abstract int GenVertexArray();
    public abstract void BindVertexArray(int vertexArray);
}
