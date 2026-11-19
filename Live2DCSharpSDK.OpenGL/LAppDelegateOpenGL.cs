using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

public class LAppDelegateOpenGL : LAppDelegate
{
    /// <summary>
    /// 每个模型的掩码缓冲张数上限 (4 张 = 最多 128 组)
    /// </summary>
    private const int MaxMaskBufferCount = 4;

    public OpenGLApi GL { get; }

    public LAppDelegateOpenGL(OpenGLApi gl)
    {
        GL = gl;

        // 纹理采样设置
        GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, GL.GL_LINEAR);
        GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, GL.GL_LINEAR);

        // 透明设置
        // R5: 模型的 Shader 输出为预乘 Alpha (FragShaderSrc.frag 的 gl_FragColor = vec4(rgb*a, a)), 因此, 
        //    全局混合也调整为 ONE / ONE_MINUS_SRC_ALPHA (官方 Samples/OpenGL LAppDelegate.cpp:94). 
        //    旧实现用 SRC_ALPHA / ONE_MINUS_SRC_ALPHA, 半透明边缘会被重复压暗.
        GL.Enable(GL.GL_BLEND);
        GL.BlendFunc(GL.GL_ONE, GL.GL_ONE_MINUS_SRC_ALPHA);

        View = new LAppViewOpenGL(this);

        InitApp();
    }

    public override void GetWindowSize(out int width, out int height)
    {
        GL.GetWindowSize(out width, out height);
    }

    public override bool RunPre()
    {
        // 画面初始化
        GL.ClearColor(BGColor.R, BGColor.G, BGColor.B, BGColor.A);
        GL.Clear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);
        GL.ClearDepthf(1.0f);

        return false;
    }

    public override CubismRenderer CreateRenderer(CubismModel model)
    {
        // [与官方的差异] 官方 sample 按 CubismUserModel::CreateRenderer 的默认值
        // 固定以 maskBufferCount = 1 生成, 而本地根据模型实际的掩码组数
        // 决定所需张数 (大肥鱼: 49 组 > 每张上限 36 的对策). 
        int maskBufferCount = CalculateMaskBufferCount(model);
        return new CubismRenderer_OpenGLES2(GL, this, model, maskBufferCount);
    }

    /// <summary>
    /// 根据模型的掩码组数计算所需的 maskBufferCount. 
    /// 每张 36 组, 2 张以上时每张 32 组 (官方的上限公式). 
    /// </summary>
    private static int CalculateMaskBufferCount(CubismModel model)
    {
        if (!model.IsUsingMasking())
        {
            return 1;
        }

        int groups = CubismMaskGroupUtility.CountDrawableMaskGroups(model);
        int maskBufferCount = 1;

        if (groups > CubismRenderer_OpenGLES2.ClippingMaskMaxCountOnDefault)
        {
            maskBufferCount = (groups + CubismRenderer_OpenGLES2.ClippingMaskMaxCountOnMultiRenderTexture - 1)
                / CubismRenderer_OpenGLES2.ClippingMaskMaxCountOnMultiRenderTexture;

            if (maskBufferCount > MaxMaskBufferCount)
            {
                CubismLog.Warning("[Live2D OpenGL]mask groups ({0}) exceed the supported limit of {1} render textures; fallback to {1}.",
                    groups, MaxMaskBufferCount);
                maskBufferCount = MaxMaskBufferCount;
            }
        }

        // 模型名不由 CubismModel 持有, 因此为便于识别同时输出 drawable / offscreen 数量
        CubismLog.Info("[Live2D OpenGL]maskBufferCount={0} maskGroups={1} drawables={2} offscreens={3}",
            maskBufferCount, groups, model.GetDrawableCount(), model.GetOffscreenCount());

        return maskBufferCount;
    }

    public override TextureInfo CreateTexture(LAppModel model, int index, int width, int height, nint data)
    {
        int textureId = GL.GenTexture();
        GL.BindTexture(GL.GL_TEXTURE_2D, textureId);
        GL.TexImage2D(GL.GL_TEXTURE_2D, 0, GL.GL_RGBA, width, height, 0, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, data);
        GL.GenerateMipmap(GL.GL_TEXTURE_2D);
        GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, GL.GL_LINEAR_MIPMAP_LINEAR);
        GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, GL.GL_LINEAR);
        GL.BindTexture(GL.GL_TEXTURE_2D, 0);

        (model.Renderer as CubismRenderer_OpenGLES2)?.BindTexture(index, textureId);

        return new TextureInfoOpenGL(GL)
        {
            Id = textureId
        };
    }

    public override void RunPost()
    {

    }

    public override void OnUpdatePre()
    {

    }
}
