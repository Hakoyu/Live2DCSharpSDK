using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// モデル描画を処理するレンダラ
/// サブクラスに環境依存の描画命令を記述する
/// </summary>
public abstract class CubismRenderer
{
    /// <summary>
    /// テクスチャの異方性フィルタリングのパラメータ
    /// </summary>
    public float Anisotropy { get; set; }
    /// <summary>
    /// レンダリング対象のモデル
    /// </summary>
    public CubismModel Model { get; private set; }
    /// <summary>
    /// 乗算済みαならtrue
    /// </summary>
    public bool IsPremultipliedAlpha { get; set; }
    /// <summary>
    /// カリングが有効ならtrue
    /// </summary>
    public bool IsCulling { get; set; }
    /// <summary>
    /// falseの場合、マスクを纏めて描画する trueの場合、マスクはパーツ描画ごとに書き直す
    /// </summary>
    public bool UseHighPrecisionMask { get; set; }

    /// <summary>
    /// モデル自体のカラー(RGBA)
    /// </summary>
    public CubismTextureColor ModelColor = new();

    /// <summary>
    /// オフスクリーンの幅（官方 _modelRenderTargetWidth、SetRenderTargetSize で更新）
    /// </summary>
    protected uint _modelRenderTargetWidth;
    /// <summary>
    /// オフスクリーンの高さ（官方 _modelRenderTargetHeight、SetRenderTargetSize で更新）
    /// </summary>
    protected uint _modelRenderTargetHeight;

    /// <summary>
    /// Model-View-Projection 行列
    /// </summary>
    private readonly CubismMatrix44 _mvpMatrix4x4 = new();

    /// <summary>
    /// レンダラのインスタンスを生成して取得する
    /// </summary>
    public CubismRenderer(CubismModel model)
    {
        _mvpMatrix4x4.LoadIdentity();
        Model = model ?? throw new Exception("model is null");
    }

    /// <summary>
    /// レンダラのインスタンスを解放する
    /// </summary>
    public abstract void Dispose();

    /// <summary>
    /// モデルを描画する
    /// </summary>
    public void DrawModel()
    {
        /**
         * DoDrawModelの描画前と描画後に以下の関数を呼んでください。
         * ・SaveProfile();
         * ・RestoreProfile();
         * これはレンダラの描画設定を保存・復帰させることで、
         * モデル描画直前の状態に戻すための処理です。
         */

        SaveProfile();

        DoDrawModel();

        RestoreProfile();
    }

    /// <summary>
    /// Model-View-Projection 行列をセットする
    /// 配列は複製されるので元の配列は外で破棄して良い
    /// </summary>
    /// <param name="matrix4x4">Model-View-Projection 行列</param>
    public void SetMvpMatrix(CubismMatrix44 matrix4x4)
    {
        _mvpMatrix4x4.SetMatrix(matrix4x4.Tr);
    }

    /// <summary>
    /// Model-View-Projection 行列を取得する
    /// </summary>
    /// <returns>Model-View-Projection 行列</returns>
    public CubismMatrix44 GetMvpMatrix()
    {
        return _mvpMatrix4x4;
    }

    /// <summary>
    /// 透明度を考慮したモデルの色を計算する。
    /// </summary>
    /// <param name="opacity">透明度</param>
    /// <returns>RGBAのカラー情報</returns>
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
    /// モデルの色をセットする。
    /// 各色0.0f～1.0fの間で指定する(1.0fが標準の状態）。
    /// </summary>
    /// <param name="red">赤チャンネルの値</param>
    /// <param name="green">緑チャンネルの値</param>
    /// <param name="blue">青チャンネルの値</param>
    /// <param name="alpha">αチャンネルの値</param>
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
    /// オフスクリーンのサイズを設定する（官方 CubismRenderer::SetRenderTargetSize、CubismRenderer.cpp:76-80）。
    /// 本地はモデル生成時にホストがレンダラを作る方式のため、D-06 に従って
    /// ホストの OnRender から実サイズで毎フレーム呼ぶ。
    /// </summary>
    /// <param name="width">モデルを描画するバッファの幅</param>
    /// <param name="height">モデルを描画するバッファの高さ</param>
    public void SetRenderTargetSize(uint width, uint height)
    {
        _modelRenderTargetWidth = width;
        _modelRenderTargetHeight = height;
    }

    /// <summary>
    /// blend モード使用時のマスク高精度化（官方 CubismRenderer.cpp:42-52）。
    /// 官方は Initialize 内で model->IsBlendModeEnabled() を参照するが、本地の
    /// CubismModel.IsBlendModeEnabled() は P2 で追加されるため、ここでは同じ判定結果を
    /// 引数で受け取る形にしておく（P6 / P8 の初期化時に渡す）。
    /// </summary>
    /// <param name="isBlendModeEnabled">モデルが blend モードを使用するなら true</param>
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
    /// モデル描画の実装
    /// </summary>
    protected abstract void DoDrawModel();

    /// <summary>
    /// モデル描画直前のレンダラのステートを保持する
    /// </summary>
    protected abstract void SaveProfile();

    /// <summary>
    /// モデル描画直前のレンダラのステートを復帰させる
    /// </summary>
    protected abstract void RestoreProfile();

    /// <summary>
    /// モデル描画直前のオフスクリーン設定（官方 pure virtual、CubismRenderer.hpp:284）。
    /// 派生クラスの DoDrawModel 冒頭で呼ぶ（官方 OpenGL 実装は CubismRenderer_OpenGLES2.cpp:724）。
    /// </summary>
    protected abstract void BeforeDrawModelRenderTarget();

    /// <summary>
    /// モデル描画後のオフスクリーン設定（官方 pure virtual、CubismRenderer.hpp:289）。
    /// 派生クラスの DoDrawModel 末尾で呼ぶ（官方 OpenGL 実装は CubismRenderer_OpenGLES2.cpp:788）。
    /// </summary>
    protected abstract void AfterDrawModelRenderTarget();
}
