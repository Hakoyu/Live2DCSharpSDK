using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// OpenGL 用のレンダーターゲット（官方 OpenGL/CubismRenderTarget_OpenGLES2.hpp:48-145 / .cpp:28-185）。
/// 旧 CubismOffscreenSurface_OpenGLES2 を R5 の名前・契約へ移行したもの（B1/B2）。
/// Framework 側 CubismRenderTarget を継承し、ICubismOffscreenManager&lt;T&gt; の
/// new() 制約から生成できるよう無引数コンストラクタを持つ（P5 報告 §2.4）。
/// </summary>
public class CubismRenderTarget_OpenGLES2 : CubismRenderTarget
{
    /// <summary>
    /// 無引数コンストラクタ用の既定 API。
    /// 官方はグローバルな GL 関数を直接呼ぶため、同等の扱いとしてレンダラ生成時に設定する。
    /// </summary>
    private static OpenGLApi? s_defaultApi;

    private readonly OpenGLApi? _gl;

    /// <summary>
    /// 無引数コンストラクタ（ICubismOffscreenManager&lt;T&gt; の new() 制約用）。
    /// </summary>
    public CubismRenderTarget_OpenGLES2()
    {
    }

    /// <summary>
    /// 引数付きコンストラクタ
    /// </summary>
    /// <param name="gl">OpenGL API</param>
    public CubismRenderTarget_OpenGLES2(OpenGLApi gl)
    {
        _gl = gl;
    }

    /// <summary>
    /// 無引数コンストラクタ用の既定 API を設定する。
    /// </summary>
    internal static void SetDefaultApi(OpenGLApi gl)
    {
        s_defaultApi = gl;
    }

    private OpenGLApi GL => _gl ?? s_defaultApi
        ?? throw new InvalidOperationException("[Live2D OpenGL]CubismRenderTarget_OpenGLES2: OpenGLApi is not set.");

    /// <summary>
    /// レンダリングターゲットとしてのアドレス
    /// </summary>
    public int RenderTexture { get; private set; }
    /// <summary>
    /// 描画の際使用するテクスチャとしてのアドレス
    /// </summary>
    public int ColorBuffer { get; private set; }

    /// <summary>
    /// 旧フレームバッファ
    /// </summary>
    private int _oldFBO;
    /// <summary>
    /// 旧ビューポート（官方 _oldViewport[4]、B2/B13）
    /// </summary>
    private readonly int[] _oldViewport = new int[4];

    /// <summary>
    /// Create時に指定された幅（ローカル互換の int 公開）
    /// </summary>
    public int BufferWidth => _bufferWidth;
    /// <summary>
    /// Create時に指定された高さ（ローカル互換の int 公開）
    /// </summary>
    public int BufferHeight => _bufferHeight;
    /// <summary>
    /// 引数によって設定されたカラーバッファか？
    /// </summary>
    private bool _isColorBufferInherited;

    private int _bufferWidth;
    private int _bufferHeight;

    /// <summary>
    /// バッファーの内容をコピーする（官方 CopyBuffer、texture barrier 非対応環境用）。
    /// </summary>
    /// <param name="src">コピー元</param>
    /// <param name="dst">コピー先</param>
    public static void CopyBuffer(CubismRenderTarget_OpenGLES2 src, CubismRenderTarget_OpenGLES2 dst)
    {
        // S6: どちらかが作成に失敗している場合はコピーしない。
        // RenderTexture == 0 を書き込み先にすると既定のフレームバッファ（画面）へ転送してしまう。
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
    /// 指定の描画ターゲットに向けて描画開始
    /// </summary>
    /// <param name="restoreFBO">0以上の場合、EndDrawでこの値をglBindFramebufferする</param>
    public void BeginDraw(int restoreFBO = -1)
    {
        if (RenderTexture == 0)
        {
            return;
        }

        // バックバッファのサーフェイスを記憶しておく
        if (restoreFBO < 0)
        {
            GL.GetIntegerv(GL.GL_FRAMEBUFFER_BINDING, out _oldFBO);
        }
        else
        {
            _oldFBO = restoreFBO;
        }

        // ビューポートを記憶しておく（官方 .cpp:57）
        GL.GetIntegerv(GL.GL_VIEWPORT, _oldViewport);

        //マスク用RenderTextureをactiveにセット
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

        // 描画対象を戻す
        GL.BindFramebuffer(GL.GL_FRAMEBUFFER, _oldFBO);
        // ビューポートも戻す（官方 .cpp:72、CHANGELOG「viewport 保存・復帰の改善」）
        GL.Viewport(_oldViewport[0], _oldViewport[1], _oldViewport[2], _oldViewport[3]);
    }

    /// <summary>
    /// レンダリングターゲットのクリア
    /// 呼ぶ場合はBeginDrawの後で呼ぶこと
    /// </summary>
    /// <param name="r">赤(0.0~1.0)</param>
    /// <param name="g">緑(0.0~1.0)</param>
    /// <param name="b">青(0.0~1.0)</param>
    /// <param name="a">α(0.0~1.0)</param>
    public void Clear(float r, float g, float b, float a)
    {
        // S6: 作成に失敗した（RenderTexture == 0）場合は描画先が不定（直前の FBO）になるため何もしない。
        if (RenderTexture == 0)
        {
            return;
        }

        // マスクをクリアする
        GL.ClearColor(r, g, b, a);
        GL.Clear(GL.GL_COLOR_BUFFER_BIT);
    }

    /// <summary>
    /// CubismRenderTarget作成（官方 CreateRenderTarget、B1）
    /// </summary>
    /// <param name="displayBufferWidth">作成するバッファ幅</param>
    /// <param name="displayBufferHeight">作成するバッファ高さ</param>
    /// <param name="colorBuffer">0以外の場合、ピクセル格納領域としてcolorBufferを使用する</param>
    /// <returns>作成できて、かつフレームバッファが完全（GL_FRAMEBUFFER_COMPLETE）なら true</returns>
    /// <remarks>
    /// S6: 作成直後に glCheckFramebufferStatus で完全性を検査する。
    /// 不完全だった場合は GL リソースを破棄して false を返す（false のとき
    /// <see cref="IsValid"/> は false、<see cref="RenderTexture"/> / <see cref="ColorBuffer"/> は 0）。
    /// 幅・高さは要求値のまま残すので、同じサイズで呼び直しても再作成は走らない
    /// （作成に失敗し続ける環境で毎フレーム作り直してログが溢れるのを避ける）。
    /// </remarks>
    public unsafe bool CreateRenderTarget(int displayBufferWidth, int displayBufferHeight, int colorBuffer = 0)
    {
        // 一旦削除
        DestroyRenderTarget();

        // S6: 0 以下のサイズは glTexImage2D が GL_INVALID_VALUE になり、
        // ストレージを持たないテクスチャを添付した「不完全な FBO」が成功扱いになる。GL を呼ぶ前に弾く。
        if (displayBufferWidth <= 0 || displayBufferHeight <= 0)
        {
            CubismLog.Warning(
                "[Live2D OpenGL]CreateRenderTarget failed: invalid size {0}x{1}. No framebuffer was created.",
                displayBufferWidth, displayBufferHeight);
            return false;
        }

        // 新しく生成する
        if (colorBuffer == 0)
        {
            ColorBuffer = GL.GenTexture();

            GL.BindTexture(GL.GL_TEXTURE_2D, ColorBuffer);
            GL.TexImage2D(GL.GL_TEXTURE_2D, 0, GL.GL_RGBA, displayBufferWidth, displayBufferHeight, 0, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, 0);
            // 官方 .cpp:98-106（桌面 GL は REPEAT / NEAREST）
            GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_S, GL.GL_REPEAT);
            GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_T, GL.GL_REPEAT);
            GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, GL.GL_NEAREST);
            GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, GL.GL_NEAREST);
            GL.BindTexture(GL.GL_TEXTURE_2D, 0);

            _isColorBufferInherited = false;
        }
        else
        {
            // 指定されたものを使用
            ColorBuffer = colorBuffer;

            _isColorBufferInherited = true;
        }

        GL.GetIntegerv(GL.GL_FRAMEBUFFER_BINDING, out int tmpFramebufferObject);

        int ret = GL.GenFramebuffer();
        GL.BindFramebuffer(GL.GL_FRAMEBUFFER, ret);
        GL.FramebufferTexture2D(GL.GL_FRAMEBUFFER, GL.GL_COLOR_ATTACHMENT0, GL.GL_TEXTURE_2D, ColorBuffer, 0);

        // S6: 添付直後（FBO をバインド中）にフレームバッファの完全性を検査する。
        // 不完全な FBO に描くと描画結果は未定義になるため、ここで失敗として扱う。
        int status = GL.CheckFramebufferStatus(GL.GL_FRAMEBUFFER);

        GL.BindFramebuffer(GL.GL_FRAMEBUFFER, tmpFramebufferObject);

        RenderTexture = ret;

        _bufferWidth = displayBufferWidth;
        _bufferHeight = displayBufferHeight;

        if (status != GL.GL_FRAMEBUFFER_COMPLETE)
        {
            // S6: 状態コード・サイズ・添付情報を出力し、GL リソースを破棄して失敗を返す
            ReportIncompleteFramebuffer(status, displayBufferWidth, displayBufferHeight);

            // 失敗したので削除（継承テクスチャは DestroyRenderTarget が消さないので、参照だけ落とす）
            DestroyRenderTarget();
            ColorBuffer = 0;
            _isColorBufferInherited = false;

            return false;
        }

        // 成功
        return true;
    }

    /// <summary>
    /// S6: フレームバッファが不完全だったときの診断ログ。
    /// 状態コード・要求サイズ・添付（COLOR_ATTACHMENT0 に付けたテクスチャ）・GL_MAX_TEXTURE_SIZE を出す。
    /// </summary>
    /// <remarks>
    /// 既存の <see cref="CubismLog.Error"/> は出力後に例外を投げるため、失敗を bool で返すこの API では
    /// 呼び出し元（描画ループ）を巻き込まないよう Warning へ降格して出力する
    /// （PLAN-03 §2「CubismLog.Error 本体は変えず、呼び出し点でのみ降格」）。
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
    /// S6: glCheckFramebufferStatus の戻り値を読みやすい名前にする。
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
    /// CubismRenderTargetの削除（官方 DestroyRenderTarget）
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
    /// レンダーテクスチャメンバーへのアクセッサ（官方 GetRenderTexture、B8）
    /// </summary>
    public int GetRenderTexture()
    {
        return RenderTexture;
    }

    /// <summary>
    /// カラーバッファメンバーへのアクセッサ（官方 GetColorBuffer、B8）
    /// </summary>
    public int GetColorBuffer()
    {
        return ColorBuffer;
    }

    /// <summary>
    /// 現在有効かどうか（官方 IsValid）
    /// </summary>
    public override bool IsValid()
    {
        return RenderTexture != 0;
    }

    /// <summary>
    /// バッファ幅取得（官方 GetBufferWidth）
    /// </summary>
    public override uint GetBufferWidth()
    {
        return (uint)_bufferWidth;
    }

    /// <summary>
    /// バッファ高さ取得（官方 GetBufferHeight）
    /// </summary>
    public override uint GetBufferHeight()
    {
        return (uint)_bufferHeight;
    }

    /// <summary>
    /// 旧フレームバッファの取得（官方 GetOldFBO、B8）
    /// </summary>
    public int GetOldFBO()
    {
        return _oldFBO;
    }
}
