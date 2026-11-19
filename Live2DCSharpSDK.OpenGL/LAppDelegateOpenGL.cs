using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

public class LAppDelegateOpenGL : LAppDelegate
{
    /// <summary>
    /// モデルごとのマスクバッファ枚数の上限（4 枚 = 128 グループまで）
    /// </summary>
    private const int MaxMaskBufferCount = 4;

    public OpenGLApi GL { get; }

    public LAppDelegateOpenGL(OpenGLApi gl)
    {
        GL = gl;

        //テクスチャサンプリング設定
        GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, GL.GL_LINEAR);
        GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, GL.GL_LINEAR);

        //透過設定
        //R5: モデルのシェーダ出力は乗算済みアルファ（FragShaderSrc.frag の gl_FragColor = vec4(rgb*a, a)）のため、
        //    グローバルブレンドも ONE / ONE_MINUS_SRC_ALPHA に合わせる（官方 Samples/OpenGL LAppDelegate.cpp:94）。
        //    R1 は SRC_ALPHA / ONE_MINUS_SRC_ALPHA で、半透明エッジが二重に暗くなる（PLAN R2）。
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
        // 画面の初期化
        GL.ClearColor(BGColor.R, BGColor.G, BGColor.B, BGColor.A);
        GL.Clear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);
        GL.ClearDepthf(1.0f);

        return false;
    }

    public override CubismRenderer CreateRenderer(CubismModel model)
    {
        // 【官方との差異】官方 sample は CubismUserModel::CreateRenderer の既定値どおり
        // maskBufferCount = 1 固定で生成するが、本地はモデルの実際のマスクグループ数から
        // 必要枚数を決める（大肥魚: 49 グループ > 1 枚あたり上限 36 の対策）。
        int maskBufferCount = CalculateMaskBufferCount(model);
        return new CubismRenderer_OpenGLES2(GL, this, model, maskBufferCount);
    }

    /// <summary>
    /// モデルのマスクグループ数から必要な maskBufferCount を算出する。
    /// 1 枚あたり 36 グループ、2 枚以上は 1 枚あたり 32 グループ（官方の上限式）。
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

        // モデル名は CubismModel が保持しないため、識別用に drawable / offscreen 数を併記する
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
