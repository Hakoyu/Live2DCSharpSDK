namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// 在模型绘制前保存并恢复 OpenGLES2 状态的类
/// (官方 OpenGL/CubismRenderer_OpenGLES2.hpp:116-173).
/// FBO / viewport 不由 Profile 保存(官方 R5 已移到 DoDrawModel 的局部变量).
/// blend 模式下额外保存并恢复纹理单元 2(官方只处理 0/1,
/// 本地为防止 blend 纹理泄漏而补强).
/// </summary>
internal class CubismRendererProfile_OpenGLES2(OpenGLApi gl)
{
    /// <summary>
    /// 模型绘制前的顶点缓冲
    /// </summary>
    internal int LastArrayBufferBinding;

    /// <summary>
    /// 模型绘制前的 Element 缓冲
    /// </summary>
    internal int LastElementArrayBufferBinding;

    /// <summary>
    /// 模型绘制前的 Shader 程序缓冲
    /// </summary>
    internal int LastProgram;

    /// <summary>
    /// 模型绘制前的活动纹理
    /// </summary>
    internal int LastActiveTexture;

    /// <summary>
    /// 模型绘制前的纹理单元 0
    /// </summary>
    internal int LastTexture0Binding2D;

    /// <summary>
    /// 模型绘制前的纹理单元 1
    /// </summary>
    internal int LastTexture1Binding2D;

    /// <summary>
    /// 模型绘制前的纹理单元 2(blend 用).
    /// </summary>
    internal int LastTexture2Binding2D;

    /// <summary>
    /// 模型绘制前的 GL_VERTEX_ATTRIB_ARRAY_ENABLED 参数
    /// </summary>
    internal int[] LastVertexAttribArrayEnabled = new int[4];

    /// <summary>
    /// 模型绘制前的 GL_SCISSOR_TEST 参数
    /// </summary>
    internal bool LastScissorTest;

    /// <summary>
    /// 模型绘制前的 GL_BLEND 参数
    /// </summary>
    internal bool LastBlend;

    /// <summary>
    /// 模型绘制前的 GL_STENCIL_TEST 参数
    /// </summary>
    internal bool LastStencilTest;

    /// <summary>
    /// 模型绘制前的 GL_DEPTH_TEST 参数
    /// </summary>
    internal bool LastDepthTest;

    /// <summary>
    /// 模型绘制前的 GL_CULL_FACE 参数
    /// </summary>
    internal bool LastCullFace;

    /// <summary>
    /// 模型绘制前的 GL_FRONT_FACE 参数
    /// </summary>
    internal int LastFrontFace;

    /// <summary>
    /// 模型绘制前的 GL_COLOR_WRITEMASK 参数
    /// </summary>
    internal bool[] LastColorMask = new bool[4];

    /// <summary>
    /// 模型绘制前的颜色混合参数
    /// </summary>
    internal int[] LastBlending = new int[4];

    /// <summary>
    /// 保存 OpenGLES2 的状态
    /// </summary>
    internal void Save()
    {
        //-- push state --
        gl.GetIntegerv(gl.GL_ARRAY_BUFFER_BINDING, out LastArrayBufferBinding);
        gl.GetIntegerv(gl.GL_ELEMENT_ARRAY_BUFFER_BINDING, out LastElementArrayBufferBinding);
        gl.GetIntegerv(gl.GL_CURRENT_PROGRAM, out LastProgram);

        gl.GetIntegerv(gl.GL_ACTIVE_TEXTURE, out LastActiveTexture);
        gl.ActiveTexture(gl.GL_TEXTURE2); // 激活纹理单元 2
        gl.GetIntegerv(gl.GL_TEXTURE_BINDING_2D, out LastTexture2Binding2D);

        gl.ActiveTexture(gl.GL_TEXTURE1); // 激活纹理单元 1 (作为后续设置的对象)
        gl.GetIntegerv(gl.GL_TEXTURE_BINDING_2D, out LastTexture1Binding2D);

        gl.ActiveTexture(gl.GL_TEXTURE0); // 激活纹理单元 0 (作为后续设置的对象)
        gl.GetIntegerv(gl.GL_TEXTURE_BINDING_2D, out LastTexture0Binding2D);

        gl.GetVertexAttribiv(
            0,
            gl.GL_VERTEX_ATTRIB_ARRAY_ENABLED,
            out LastVertexAttribArrayEnabled[0]
        );
        gl.GetVertexAttribiv(
            1,
            gl.GL_VERTEX_ATTRIB_ARRAY_ENABLED,
            out LastVertexAttribArrayEnabled[1]
        );
        gl.GetVertexAttribiv(
            2,
            gl.GL_VERTEX_ATTRIB_ARRAY_ENABLED,
            out LastVertexAttribArrayEnabled[2]
        );
        gl.GetVertexAttribiv(
            3,
            gl.GL_VERTEX_ATTRIB_ARRAY_ENABLED,
            out LastVertexAttribArrayEnabled[3]
        );

        LastScissorTest = gl.IsEnabled(gl.GL_SCISSOR_TEST);
        LastStencilTest = gl.IsEnabled(gl.GL_STENCIL_TEST);
        LastDepthTest = gl.IsEnabled(gl.GL_DEPTH_TEST);
        LastCullFace = gl.IsEnabled(gl.GL_CULL_FACE);
        LastBlend = gl.IsEnabled(gl.GL_BLEND);

        gl.GetIntegerv(gl.GL_FRONT_FACE, out LastFrontFace);

        gl.GetBooleanv(gl.GL_COLOR_WRITEMASK, LastColorMask);

        // backup blending
        gl.GetIntegerv(gl.GL_BLEND_SRC_RGB, out LastBlending[0]);
        gl.GetIntegerv(gl.GL_BLEND_DST_RGB, out LastBlending[1]);
        gl.GetIntegerv(gl.GL_BLEND_SRC_ALPHA, out LastBlending[2]);
        gl.GetIntegerv(gl.GL_BLEND_DST_ALPHA, out LastBlending[3]);
    }

    /// <summary>
    /// 恢复已保存的 OpenGLES2 状态
    /// </summary>
    internal void Restore()
    {
        gl.UseProgram(LastProgram);

        SetGlEnableVertexAttribArray(0, LastVertexAttribArrayEnabled[0] > 0);
        SetGlEnableVertexAttribArray(1, LastVertexAttribArrayEnabled[1] > 0);
        SetGlEnableVertexAttribArray(2, LastVertexAttribArrayEnabled[2] > 0);
        SetGlEnableVertexAttribArray(3, LastVertexAttribArrayEnabled[3] > 0);

        SetGlEnable(gl.GL_SCISSOR_TEST, LastScissorTest);
        SetGlEnable(gl.GL_STENCIL_TEST, LastStencilTest);
        SetGlEnable(gl.GL_DEPTH_TEST, LastDepthTest);
        SetGlEnable(gl.GL_CULL_FACE, LastCullFace);
        SetGlEnable(gl.GL_BLEND, LastBlend);

        gl.FrontFace(LastFrontFace);

        gl.ColorMask(LastColorMask[0], LastColorMask[1], LastColorMask[2], LastColorMask[3]);

        gl.BindBuffer(gl.GL_ARRAY_BUFFER, LastArrayBufferBinding); // 之前若有缓冲被绑定, 则需要丢弃
        gl.BindBuffer(gl.GL_ELEMENT_ARRAY_BUFFER, LastElementArrayBufferBinding);

        gl.ActiveTexture(gl.GL_TEXTURE2); // 恢复纹理单元 2
        gl.BindTexture(gl.GL_TEXTURE_2D, LastTexture2Binding2D);

        gl.ActiveTexture(gl.GL_TEXTURE1); // 恢复纹理单元 1
        gl.BindTexture(gl.GL_TEXTURE_2D, LastTexture1Binding2D);

        gl.ActiveTexture(gl.GL_TEXTURE0); // 恢复纹理单元 0
        gl.BindTexture(gl.GL_TEXTURE_2D, LastTexture0Binding2D);

        gl.ActiveTexture(LastActiveTexture);

        // restore blending
        gl.BlendFuncSeparate(LastBlending[0], LastBlending[1], LastBlending[2], LastBlending[3]);
    }

    /// <summary>
    /// 设置 OpenGLES2 功能的启用/禁用
    /// </summary>
    /// <param name="index">要启用/禁用的功能</param>
    /// <param name="enabled">为 true 时启用</param>
    internal void SetGlEnable(int index, bool enabled)
    {
        if (enabled == true)
            gl.Enable(index);
        else
            gl.Disable(index);
    }

    /// <summary>
    /// 设置 OpenGLES2 的 Vertex Attribute Array 功能的启用/禁用
    /// </summary>
    /// <param name="index">要启用/禁用的功能</param>
    /// <param name="enabled">为 true 时启用</param>
    internal void SetGlEnableVertexAttribArray(int index, bool enabled)
    {
        if (enabled)
            gl.EnableVertexAttribArray(index);
        else
            gl.DisableVertexAttribArray(index);
    }
}
