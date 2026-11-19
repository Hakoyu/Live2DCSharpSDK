using System.Runtime.InteropServices;
using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// OpenGLES2用の描画命令を実装したクラス
/// （官方 OpenGL/CubismRenderer_OpenGLES2.hpp:179-559 / .cpp:472-1423）。
/// R5 で Drawable と Offscreen を統合した描画順（二重ソート配列）、
/// Drawable/Offscreen 二系統のクリッピングマネージャ、モデル全体の
/// レンダーターゲット（blend モード用）を扱う。
/// </summary>
public class CubismRenderer_OpenGLES2 : CubismRenderer
{
    public const int ColorChannelCount = 4;   // 実験時に1チャンネルの場合は1、RGBだけの場合は3、アルファも含める場合は4
    public const int ClippingMaskMaxCountOnDefault = 36;  // 通常のフレームバッファ1枚あたりのマスク最大数
    public const int ClippingMaskMaxCountOnMultiRenderTexture = 32;   // フレームバッファが2枚以上ある場合のフレームバッファ1枚あたりのマスク最大数

    private readonly OpenGLApi GL;

    /// <summary>
    /// モデルが参照するテクスチャとレンダラでバインドしているテクスチャとのマップ
    /// </summary>
    private readonly Dictionary<int, int> _textures;
    /// <summary>
    /// 描画オブジェクトのインデックスを描画順に並べたリスト（Drawable + Offscreen の通し番号）
    /// </summary>
    private readonly int[] _sortedObjectsIndexList;
    /// <summary>
    /// 描画オブジェクトの種別を描画順に並べたリスト
    /// </summary>
    private readonly CsmEnum.DrawableObjectType[] _sortedObjectsTypeList;
    /// <summary>
    /// OpenGLのステートを保持するオブジェクト
    /// </summary>
    private readonly CubismRendererProfile_OpenGLES2 _rendererProfile;
    /// <summary>
    /// Drawable 用クリッピングマスク管理オブジェクト（B6）
    /// </summary>
    private CubismClippingManager_OpenGLES2? _drawableClippingManager;
    /// <summary>
    /// Offscreen 用クリッピングマスク管理オブジェクト（B6）
    /// </summary>
    private CubismClippingManager_OpenGLES2? _offscreenClippingManager;
    /// <summary>
    /// マスクテクスチャに描画するためのクリッピングコンテキスト
    /// </summary>
    internal CubismClippingContext? ClippingContextBufferForMask { get; set; }
    /// <summary>
    /// Drawable で画面上描画するためのクリッピングコンテキスト
    /// </summary>
    internal CubismClippingContext? ClippingContextBufferForDrawable { get; set; }
    /// <summary>
    /// Offscreen で画面上描画するためのクリッピングコンテキスト
    /// </summary>
    internal CubismClippingContext? ClippingContextBufferForOffscreen { get; set; }

    internal CubismShader_OpenGLES2 Shader;

    /// <summary>
    /// モデル全体を描画する先のフレームバッファ（blend モード用、B7）
    /// </summary>
    private readonly List<CubismRenderTarget_OpenGLES2> _modelRenderTargets = [];
    /// <summary>
    /// Drawable のマスク描画用のフレームバッファ（B6）
    /// </summary>
    private readonly List<CubismRenderTarget_OpenGLES2> _drawableMasks = [];
    /// <summary>
    /// Offscreen 機能マスク描画用のフレームバッファ（B6）
    /// </summary>
    private readonly List<CubismRenderTarget_OpenGLES2> _offscreenMasks = [];
    /// <summary>
    /// モデルのオフスクリーン（B3/B4）
    /// </summary>
    private readonly List<CubismOffscreenRenderTarget_OpenGLES2> _offscreenList = [];

    /// <summary>現在のフレームバッファオブジェクト</summary>
    private int _currentFBO;
    /// <summary>現在のオフスクリーンのフレームバッファ</summary>
    private CubismOffscreenRenderTarget_OpenGLES2? _currentOffscreen;
    /// <summary>モデル描画のルートフレームバッファ</summary>
    private int _modelRootFBO;
    /// <summary>Dispose 済みか（二重解放防止。GL 名の再利用事故を避ける）</summary>
    private bool _disposed;

    // ---- S6: FBO 作成失敗の診断ログを 1 回だけ出すためのフラグ（毎フレームのログ溢れ防止）----
    /// <summary>モデル合成用レンダーターゲットが使えないことを警告済みか（S6）</summary>
    private bool _warnedModelRenderTargetUnavailable;
    /// <summary>オフスクリーン用レンダーターゲットが使えないことを警告済みか（S6）</summary>
    private bool _warnedOffscreenRenderTargetUnavailable;
    /// <summary>コピー先レンダーターゲットが使えないことを警告済みか（S6）</summary>
    private bool _warnedCopyRenderTargetUnavailable;
    /// <summary>Drawable 用マスクバッファが使えないことを警告済みか（N-2）</summary>
    private bool _warnedDrawableMaskBufferUnavailable;
    /// <summary>Offscreen 用マスクバッファが使えないことを警告済みか（N-2）</summary>
    private bool _warnedOffscreenMaskBufferUnavailable;

    internal int VertexArray { get; private set; }
    internal int VertexBuffer { get; private set; }
    internal int IndexBuffer { get; private set; }
    /// <summary>画面全体クワッド用の頂点バッファ（Copy / Offscreen 合成用）</summary>
    private int _quadVertexBuffer;
    /// <summary>画面全体クワッド用のインデックスバッファ</summary>
    private int _quadIndexBuffer;

    /// <summary>
    /// 描画ターゲット合成用のクワッド（位置 + UV のインターリーブ）
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct VBO
    {
        public float ver0;
        public float ver1;
        public float uv0;
        public float uv1;
    }

    /// <summary>画面全体クワッドの頂点（通常 UV）</summary>
    private static readonly VBO[] QuadVertices =
    [
        new() { ver0 = -1.0f, ver1 = -1.0f, uv0 = 0.0f, uv1 = 0.0f },
        new() { ver0 = 1.0f, ver1 = -1.0f, uv0 = 1.0f, uv1 = 0.0f },
        new() { ver0 = -1.0f, ver1 = 1.0f, uv0 = 0.0f, uv1 = 1.0f },
        new() { ver0 = 1.0f, ver1 = 1.0f, uv0 = 1.0f, uv1 = 1.0f },
    ];

    /// <summary>画面全体クワッドの頂点（上下反転 UV、オフスクリーン合成用）</summary>
    private static readonly VBO[] QuadVerticesReverseUv =
    [
        new() { ver0 = -1.0f, ver1 = -1.0f, uv0 = 0.0f, uv1 = 1.0f },
        new() { ver0 = 1.0f, ver1 = -1.0f, uv0 = 1.0f, uv1 = 1.0f },
        new() { ver0 = -1.0f, ver1 = 1.0f, uv0 = 0.0f, uv1 = 0.0f },
        new() { ver0 = 1.0f, ver1 = 1.0f, uv0 = 1.0f, uv1 = 0.0f },
    ];

    /// <summary>画面全体クワッドのインデックス（官方 ModelRenderTargetIndexArray）</summary>
    private static readonly ushort[] QuadIndices = [0, 1, 2, 2, 1, 3];

    internal VBO[] vbo = new VBO[512];

    private readonly LAppDelegate _lapp;

    /// <summary>
    /// Tegraプロセッサ対応。拡張方式による描画の有効・無効
    /// </summary>
    public void SetExtShaderMode(bool extMode, bool extPAMode = false)
    {
        Shader.SetExtShaderMode(extMode, extPAMode);
        Shader.ReleaseShaderProgram();
    }

    /// <summary>
    /// コンストラクタ（本地は D-06 によりホストがレンダラを生成してモデルへ渡す方式）。
    /// 官方 Initialize(model, maskBufferCount) 相当の初期化をここで行う。
    /// </summary>
    public unsafe CubismRenderer_OpenGLES2(OpenGLApi gl, LAppDelegate lapp, CubismModel model, int maskBufferCount = 1) : base(model)
    {
        GL = gl;
        CubismRenderTarget_OpenGLES2.SetDefaultApi(gl);
        Shader = new(gl);
        _lapp = lapp;
        _rendererProfile = new(gl);
        _textures = new Dictionary<int, int>(32);

        VertexArray = GL.GenVertexArray();
        VertexBuffer = GL.GenBuffer();
        IndexBuffer = GL.GenBuffer();
        _quadVertexBuffer = GL.GenBuffer();
        _quadIndexBuffer = GL.GenBuffer();

        // 1未満は1に補正する
        if (maskBufferCount < 1)
        {
            maskBufferCount = 1;
            CubismLog.Warning("[Live2D OpenGL]The number of render textures must be an integer greater than or equal to 1. Set the number of render textures to 1.");
        }

        //------------ モデル全体のレンダーターゲット（blend モード用、B7）------------
        _modelRenderTargets.Clear();
        if (model.IsBlendModeEnabled())
        {
            // オフスクリーンの作成
            // 本地は texture barrier を使用しないため常に 2 枚作成する
            // （官方は CanUseTextureBarrier() ? 1 : 2、.cpp:573）
            // 添え字 0 は描画先となる
            // 添え字 1 は Copy 用
            for (int i = 0; i < 2; ++i)
            {
                _modelRenderTargets.Add(new CubismRenderTarget_OpenGLES2(GL));
            }
        }

        //------------ Drawable 用クリッピングマスク（B6）------------
        if (model.IsUsingMasking())
        {
            _drawableClippingManager = new CubismClippingManager_OpenGLES2(GL)
            {
                Type = CsmEnum.DrawableObjectType.Drawable
            };
            _drawableClippingManager.Initialize(model, maskBufferCount);

            _drawableMasks.Clear();
            for (int i = 0; i < maskBufferCount; ++i)
            {
                var mask = new CubismRenderTarget_OpenGLES2(GL);
                mask.CreateRenderTarget((int)_drawableClippingManager.ClippingMaskBufferSize.X, (int)_drawableClippingManager.ClippingMaskBufferSize.Y);
                _drawableMasks.Add(mask);
            }
        }

        //------------ Offscreen 用クリッピングマスク（B6）------------
        if (model.IsUsingMaskingForOffscreen())
        {
            _offscreenClippingManager = new CubismClippingManager_OpenGLES2(GL)
            {
                Type = CsmEnum.DrawableObjectType.Offscreen
            };
            _offscreenClippingManager.Initialize(model, maskBufferCount);

            _offscreenMasks.Clear();
            for (int i = 0; i < maskBufferCount; ++i)
            {
                var mask = new CubismRenderTarget_OpenGLES2(GL);
                mask.CreateRenderTarget((int)_offscreenClippingManager.ClippingMaskBufferSize.X, (int)_offscreenClippingManager.ClippingMaskBufferSize.Y);
                _offscreenMasks.Add(mask);
            }
        }

        //------------ 描画順ソート配列（Drawable + Offscreen、B5）------------
        int offscreenCount = model.GetOffscreenCount();
        int drawableCount = model.GetDrawableCount();
        _sortedObjectsIndexList = new int[drawableCount + offscreenCount];
        _sortedObjectsTypeList = new CsmEnum.DrawableObjectType[drawableCount + offscreenCount];

        //------------ オフスクリーンリスト（B3/B4）------------
        if (offscreenCount > 0)
        {
            for (int offscreenIndex = 0; offscreenIndex < offscreenCount; ++offscreenIndex)
            {
                var renderTarget = new CubismOffscreenRenderTarget_OpenGLES2();
                renderTarget.SetOffscreenIndex(offscreenIndex);
                _offscreenList.Add(renderTarget);
            }

            // 全てのオフスクリーンを登録し終わってから行う
            SetupParentOffscreens(model, offscreenCount);
        }

        // blend モード使用時のマスク高精度化（A6 / P5 の ApplyBlendModeMaskPrecision）
        ApplyBlendModeMaskPrecision(model.IsBlendModeEnabled());

        // 【D-09／官方との差異】官方は Initialize 時に 494 個のシェーダを全生成する
        // （CubismRenderer_OpenGLES2.cpp:638-642）が、本地は初回使用時まで生成しない。
    }

    /// <summary>
    /// オフスクリーンの親を探して設定する（官方 SetupParentOffscreens、.cpp:644-678）。
    /// </summary>
    private unsafe void SetupParentOffscreens(CubismModel model, int offscreenCount)
    {
        int* ownerIndices = model.GetOffscreenOwnerIndices();

        for (int offscreenIndex = 0; offscreenIndex < offscreenCount; ++offscreenIndex)
        {
            CubismOffscreenRenderTarget_OpenGLES2? parentOffscreen = null;
            int ownerIndex = ownerIndices[offscreenIndex];
            int parentIndex = model.GetPartParentPartIndex(ownerIndex);

            // 親のオフスクリーンを探す
            while (parentIndex != CubismModel.CubismNoIndex_Parent)
            {
                for (int i = 0; i < offscreenCount; ++i)
                {
                    if (ownerIndices[_offscreenList[i].GetOffscreenIndex()] != parentIndex)
                    {
                        continue; // オフスクリーンのインデックスが親と一致しなければスキップ
                    }

                    parentOffscreen = _offscreenList[i];
                    break;
                }

                if (parentOffscreen != null)
                {
                    break; // 親のオフスクリーンが見つかった場合はループを抜ける
                }

                parentIndex = model.GetPartParentPartIndex(parentIndex);
            }

            // 親のオフスクリーンを設定
            _offscreenList[offscreenIndex].SetParentPartOffscreen(parentOffscreen);
        }
    }

    /// <summary>
    /// OpenGLテクスチャのバインド処理
    /// </summary>
    /// <param name="modelTextureNo">セットするモデルテクスチャの番号</param>
    /// <param name="glTextureNo">OpenGLテクスチャの番号</param>
    public void BindTexture(int modelTextureNo, int glTextureNo)
    {
        _textures[modelTextureNo] = glTextureNo;
    }

    /// <summary>
    /// OpenGLにバインドされたテクスチャのリストを取得する
    /// </summary>
    public Dictionary<int, int> GetBindedTextures()
    {
        return _textures;
    }

    /// <summary>
    /// 描画オブジェクトのクリッピングマスクバッファのサイズを設定する
    /// （官方 SetDrawableClippingMaskBufferSize、B6）
    /// </summary>
    public void SetDrawableClippingMaskBufferSize(float width, float height)
    {
        if (_drawableClippingManager == null)
        {
            return;
        }

        // インスタンス破棄前にレンダーテクスチャの数を保存
        int renderTextureCount = _drawableClippingManager.RenderTextureCount;

        // RenderTargetのサイズを変更するためにインスタンスを破棄・再作成する
        _drawableClippingManager.Dispose();
        _drawableClippingManager = new CubismClippingManager_OpenGLES2(GL)
        {
            Type = CsmEnum.DrawableObjectType.Drawable
        };

        _drawableClippingManager.SetClippingMaskBufferSize(width, height);

        _drawableClippingManager.Initialize(Model, renderTextureCount);
    }

    /// <summary>
    /// オフスクリーンのクリッピングマスクバッファのサイズを設定する
    /// （官方 SetOffscreenClippingMaskBufferSize、B6）
    /// </summary>
    public void SetOffscreenClippingMaskBufferSize(float width, float height)
    {
        if (_offscreenClippingManager == null)
        {
            return;
        }

        int renderTextureCount = _offscreenClippingManager.RenderTextureCount;

        _offscreenClippingManager.Dispose();
        _offscreenClippingManager = new CubismClippingManager_OpenGLES2(GL)
        {
            Type = CsmEnum.DrawableObjectType.Offscreen
        };

        _offscreenClippingManager.SetClippingMaskBufferSize(width, height);

        _offscreenClippingManager.Initialize(Model, renderTextureCount);
    }

    /// <summary>
    /// クリッピングマスクバッファのサイズを設定する（旧 API 互換。Drawable 用）
    /// </summary>
    public void SetClippingMaskBufferSize(float width, float height)
    {
        SetDrawableClippingMaskBufferSize(width, height);
    }

    /// <summary>
    /// 描画オブジェクトのレンダーテクスチャの枚数を取得する（B8）
    /// </summary>
    public int GetDrawableRenderTextureCount()
    {
        return _drawableClippingManager?.RenderTextureCount ?? 0;
    }

    /// <summary>
    /// オフスクリーンのレンダーテクスチャの枚数を取得する（B8）
    /// </summary>
    public int GetOffscreenRenderTextureCount()
    {
        return _offscreenClippingManager?.RenderTextureCount ?? 0;
    }

    /// <summary>
    /// 描画オブジェクトのクリッピングマスクバッファのサイズを取得する（B8）
    /// </summary>
    public System.Numerics.Vector2 GetDrawableClippingMaskBufferSize()
    {
        return _drawableClippingManager?.ClippingMaskBufferSize ?? default;
    }

    /// <summary>
    /// オフスクリーンのクリッピングマスクバッファのサイズを取得する（B8）
    /// </summary>
    public System.Numerics.Vector2 GetOffscreenClippingMaskBufferSize()
    {
        return _offscreenClippingManager?.ClippingMaskBufferSize ?? default;
    }

    /// <summary>
    /// 描画オブジェクトのクリッピングマスクのバッファを取得する（官方 GetDrawableMaskBuffer、B8）
    /// </summary>
    public CubismRenderTarget_OpenGLES2 GetDrawableMaskBuffer(int index)
    {
        return _drawableMasks[index];
    }

    /// <summary>
    /// オフスクリーンのクリッピングマスクのバッファを取得する（官方 GetOffscreenMaskBuffer、B8）
    /// </summary>
    public CubismRenderTarget_OpenGLES2 GetOffscreenMaskBuffer(int index)
    {
        return _offscreenMasks[index];
    }

    /// <summary>
    /// クリッピングマスクのバッファを取得する（旧 API 互換。Drawable 用）
    /// </summary>
    public CubismRenderTarget_OpenGLES2 GetMaskBuffer(int index)
    {
        return GetDrawableMaskBuffer(index);
    }

    /// <summary>
    /// 遮罩用レンダーターゲットが描画に使えるかを判定する（N-2）。
    /// 作成に失敗した RT は RenderTexture == 0 のため IsValid() が false になる。
    /// この判定が false のバッファには Viewport / Clear / マスク描画を行ってはならない
    /// （実行すると直前にバインドされている FBO＝画面や親 FBO を破壊する）。
    /// </summary>
    /// <param name="type">Drawable 用 / Offscreen 用の別</param>
    /// <param name="index">マスクバッファのインデックス</param>
    /// <returns>使用可能なら true</returns>
    internal bool IsMaskBufferUsable(CsmEnum.DrawableObjectType type, int index)
    {
        List<CubismRenderTarget_OpenGLES2> masks = type == CsmEnum.DrawableObjectType.Offscreen
            ? _offscreenMasks
            : _drawableMasks;

        return index >= 0 && index < masks.Count && masks[index].IsValid();
    }

    /// <summary>
    /// 遮罩用レンダーターゲットが使えないことを 1 度だけ警告する（N-2）。
    /// 実際の Viewport / Clear / マスク描画のスキップは
    /// <see cref="CubismClippingManager_OpenGLES2.SetupClippingContext"/> と
    /// 高精度マスク経路（DrawDrawable / DrawOffscreen）側で行う。
    /// </summary>
    /// <param name="type">Drawable 用 / Offscreen 用の別</param>
    internal void WarnMaskBufferUnavailable(CsmEnum.DrawableObjectType type)
    {
        if (type == CsmEnum.DrawableObjectType.Offscreen)
        {
            if (_warnedOffscreenMaskBufferUnavailable)
            {
                return;
            }

            _warnedOffscreenMaskBufferUnavailable = true;
            CubismLog.Warning(
                "[Live2D OpenGL]Offscreen clipping mask render target is not available ({0}x{1}). " +
                "Skip generating clipping masks for offscreens.",
                (int)(_offscreenClippingManager?.ClippingMaskBufferSize.X ?? 0.0f),
                (int)(_offscreenClippingManager?.ClippingMaskBufferSize.Y ?? 0.0f));
            return;
        }

        if (_warnedDrawableMaskBufferUnavailable)
        {
            return;
        }

        _warnedDrawableMaskBufferUnavailable = true;
        CubismLog.Warning(
            "[Live2D OpenGL]Drawable clipping mask render target is not available ({0}x{1}). " +
            "Skip generating clipping masks for drawables.",
            (int)(_drawableClippingManager?.ClippingMaskBufferSize.X ?? 0.0f),
            (int)(_drawableClippingManager?.ClippingMaskBufferSize.Y ?? 0.0f));
    }

    /// <summary>
    /// 遮罩用レンダーターゲットの作成に失敗していないか確認し、失敗していれば 1 度だけ警告する（N-2）。
    /// </summary>
    /// <param name="type">Drawable 用 / Offscreen 用の別</param>
    private void ValidateMaskBuffers(CsmEnum.DrawableObjectType type)
    {
        List<CubismRenderTarget_OpenGLES2> masks = type == CsmEnum.DrawableObjectType.Offscreen
            ? _offscreenMasks
            : _drawableMasks;

        for (int i = 0; i < masks.Count; ++i)
        {
            if (!masks[i].IsValid())
            {
                WarnMaskBufferUnavailable(type);
                return;
            }
        }
    }

    /// <summary>
    /// 現在のオフスクリーンのフレームバッファを取得する（B8）
    /// </summary>
    public CubismOffscreenRenderTarget_OpenGLES2? GetCurrentOffscreen()
    {
        return _currentOffscreen;
    }

    public bool IsGeneratingMask()
    {
        return ClippingContextBufferForMask != null;
    }

    /// <summary>
    /// オフスクリーンのバッファをコピーする（官方 CopyOffscreenRenderTarget、B7/B8）
    /// </summary>
    public CubismRenderTarget_OpenGLES2? CopyOffscreenRenderTarget()
    {
        return _modelRenderTargets.Count == 0 ? null : CopyRenderTarget(_modelRenderTargets[0]);
    }

    /// <summary>
    /// 任意のバッファをコピーする（官方 CopyRenderTarget、B2/B7）。
    /// 本地は texture barrier を使用しないため、常に glBlitFramebuffer でコピーする
    /// （官方の CanUseTextureBarrier()==false 経路に相当）。
    /// </summary>
    public CubismRenderTarget_OpenGLES2? CopyRenderTarget(CubismRenderTarget_OpenGLES2 srcBuffer)
    {
        if (_modelRenderTargets.Count < 2)
        {
            return srcBuffer;
        }

        // S6: コピー元が作成に失敗している場合はコピーしない（呼び出し側は null を ?? 0 で扱う）
        if (!srcBuffer.IsValid())
        {
            return null;
        }

        CubismRenderTarget_OpenGLES2 dst = _modelRenderTargets[1];
        if (dst.BufferWidth != srcBuffer.BufferWidth || dst.BufferHeight != srcBuffer.BufferHeight)
        {
            if (!dst.CreateRenderTarget(srcBuffer.BufferWidth, srcBuffer.BufferHeight))
            {
                WarnCopyRenderTargetUnavailable(srcBuffer.BufferWidth, srcBuffer.BufferHeight);
                return null;
            }
        }
        else if (!dst.IsValid())
        {
            WarnCopyRenderTargetUnavailable(srcBuffer.BufferWidth, srcBuffer.BufferHeight);
            return null;
        }

        CubismRenderTarget_OpenGLES2.CopyBuffer(srcBuffer, dst);

        return dst;
    }

    /// <summary>
    /// S6: コピー先レンダーターゲットを作成できなかったことを 1 回だけ警告する。
    /// </summary>
    private void WarnCopyRenderTargetUnavailable(int width, int height)
    {
        if (_warnedCopyRenderTargetUnavailable)
        {
            return;
        }

        _warnedCopyRenderTargetUnavailable = true;
        CubismLog.Warning(
            "[Live2D OpenGL]CopyRenderTarget: destination render target is not available ({0}x{1}). " +
            "Blend-mode texture copy is skipped.",
            width, height);
    }

    /// <summary>
    /// [オーバーライド]
    /// 描画オブジェクト（アートメッシュ）を描画する。
    /// </summary>
    /// <param name="model">描画対象のモデル</param>
    /// <param name="index">描画対象のメッシュのインデックス</param>
    internal unsafe void DrawMeshOpenGL(CubismModel model, int index)
    {
        if (_textures[model.GetDrawableTextureIndex(index)] == 0) return;    // モデルが参照するテクスチャがバインドされていない場合は描画をスキップする

        // 裏面描画の有効・無効
        if (IsCulling)
        {
            GL.Enable(GL.GL_CULL_FACE);
        }
        else
        {
            GL.Disable(GL.GL_CULL_FACE);
        }

        GL.FrontFace(GL.GL_CCW);    // Cubism SDK OpenGLはマスク・アートメッシュ共にCCWが表面

        var vertexCount = model.GetDrawableVertexCount(index);
        var vertexArray = model.GetDrawableVertices(index);
        var uvArray = (float*)model.GetDrawableVertexUvs(index);

        GL.BindVertexArray(VertexArray);

        if (vbo == null || vbo.Length < vertexCount)
        {
            vbo = new VBO[vertexCount];
        }

        for (int a = 0; a < vertexCount; a++)
        {
            vbo[a].ver0 = vertexArray[a * 2];
            vbo[a].ver1 = vertexArray[a * 2 + 1];
            vbo[a].uv0 = uvArray[a * 2];
            vbo[a].uv1 = uvArray[a * 2 + 1];
        }

        int indexCount = model.GetDrawableVertexIndexCount(index);
        ushort* indexArray = model.GetDrawableVertexIndices(index);

        GL.BindBuffer(GL.GL_ARRAY_BUFFER, VertexBuffer);
        fixed (void* p = vbo)
        {
            GL.BufferData(GL.GL_ARRAY_BUFFER, vertexCount * sizeof(VBO), new IntPtr(p), GL.GL_STATIC_DRAW);
        }

        GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, IndexBuffer);
        GL.BufferData(GL.GL_ELEMENT_ARRAY_BUFFER, indexCount * sizeof(ushort), new IntPtr(indexArray), GL.GL_STATIC_DRAW);

        if (IsGeneratingMask())  // マスク生成時
        {
            Shader.SetupShaderProgramForMask(this, model, index);
        }
        else
        {
            Shader.SetupShaderProgramForDrawable(this, model, index);
        }

        // ポリゴンメッシュを描画する
        GL.DrawElements(GL.GL_TRIANGLES, indexCount, GL.GL_UNSIGNED_SHORT, 0);

        // 後処理
        GL.UseProgram(0);
        GL.BindBuffer(GL.GL_ARRAY_BUFFER, 0);
        GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, 0);
        GL.BindVertexArray(0);
        ClippingContextBufferForDrawable = null;
        ClippingContextBufferForMask = null;
    }

    /// <summary>
    /// 画面全体クワッドの頂点属性を設定する（Copy / Offscreen 合成用）。
    /// </summary>
    /// <param name="shaderSet">使用するシェーダセット</param>
    /// <param name="reverseUv">UV を上下反転するか（オフスクリーン合成時 true）</param>
    internal unsafe void SetRenderTargetVertexAttributes(CubismShaderSet shaderSet, bool reverseUv)
    {
        GL.BindVertexArray(VertexArray);

        GL.BindBuffer(GL.GL_ARRAY_BUFFER, _quadVertexBuffer);
        fixed (void* p = reverseUv ? QuadVerticesReverseUv : QuadVertices)
        {
            GL.BufferData(GL.GL_ARRAY_BUFFER, QuadVertices.Length * sizeof(VBO), new IntPtr(p), GL.GL_STATIC_DRAW);
        }

        GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, _quadIndexBuffer);
        fixed (void* p = QuadIndices)
        {
            GL.BufferData(GL.GL_ELEMENT_ARRAY_BUFFER, QuadIndices.Length * sizeof(ushort), new IntPtr(p), GL.GL_STATIC_DRAW);
        }

        GL.EnableVertexAttribArray(shaderSet.AttributePositionLocation);
        GL.VertexAttribPointer(shaderSet.AttributePositionLocation, 2, GL.GL_FLOAT, false, 4 * sizeof(float), 0);

        GL.EnableVertexAttribArray(shaderSet.AttributeTexCoordLocation);
        GL.VertexAttribPointer(shaderSet.AttributeTexCoordLocation, 2, GL.GL_FLOAT, false, 4 * sizeof(float), 2 * sizeof(float));
    }

    /// <summary>
    /// 画面全体クワッドを描画する。
    /// </summary>
    internal void DrawRenderTargetQuad()
    {
        GL.DrawElements(GL.GL_TRIANGLES, QuadIndices.Length, GL.GL_UNSIGNED_SHORT, 0);
    }

    /// <summary>
    /// 描画開始時の追加処理。
    /// モデルを描画する前にクリッピングマスクに必要な処理を実装している。
    /// </summary>
    internal void PreDraw()
    {
        GL.Disable(GL.GL_SCISSOR_TEST);
        GL.Disable(GL.GL_STENCIL_TEST);
        GL.Disable(GL.GL_DEPTH_TEST);

        GL.Enable(GL.GL_BLEND);
        GL.ColorMask(true, true, true, true);

        if (GL.IsPhoneES2)
        {
            GL.BindVertexArrayOES(0);
        }

        GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, 0);
        GL.BindBuffer(GL.GL_ARRAY_BUFFER, 0); //前にバッファがバインドされていたら破棄する必要がある

        //異方性フィルタリング。プラットフォームのOpenGLによっては未対応の場合があるので、未設定のときは設定しない
        if (Anisotropy > 0.0f)
        {
            for (int i = 0; i < _textures.Count; i++)
            {
                GL.BindTexture(GL.GL_TEXTURE_2D, _textures[i]);
                GL.TexParameterf(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAX_ANISOTROPY_EXT, Anisotropy);
            }
        }
    }

    /// <summary>
    /// モデル描画直前のOpenGLES2のステートを保持する
    /// </summary>
    protected override void SaveProfile()
    {
        _rendererProfile.Save();
    }

    /// <summary>
    /// モデル描画直前のOpenGLES2のステートを復帰させる
    /// </summary>
    protected override void RestoreProfile()
    {
        _rendererProfile.Restore();
    }

    /// <summary>
    /// モデル描画直前のオフスクリーン設定（官方 BeforeDrawModelRenderTarget、B7）。
    /// </summary>
    protected override void BeforeDrawModelRenderTarget()
    {
        if (_modelRenderTargets.Count == 0)
        {
            return;
        }

        // D-06: ホストが SetRenderTargetSize を呼ぶまでは 0x0 のため作成しない。
        // ただし宿題（G2 冒煙・P7 前のホスト）では未設定のまま来るため、
        // 現在のビューポートサイズで初期化するフォールバックを置く（官方との差異、P7 で SetRenderTargetSize 接続後も無害）。
        if (_modelRenderTargetWidth == 0 || _modelRenderTargetHeight == 0)
        {
            int[] viewport = new int[4];
            GL.GetIntegerv(GL.GL_VIEWPORT, viewport);
            if (viewport[2] <= 0 || viewport[3] <= 0)
            {
                return;
            }

            SetRenderTargetSize((uint)viewport[2], (uint)viewport[3]);
        }

        // オフスクリーンのバッファのサイズが違う場合は作り直し
        for (int i = 0; i < _modelRenderTargets.Count; ++i)
        {
            if (_modelRenderTargets[i].GetBufferWidth() != _modelRenderTargetWidth ||
                _modelRenderTargets[i].GetBufferHeight() != _modelRenderTargetHeight)
            {
                _modelRenderTargets[i].CreateRenderTarget((int)_modelRenderTargetWidth, (int)_modelRenderTargetHeight, 0);
            }
        }

        // S6: FBO を作成できなかった場合はオフスクリーン描画を諦め、現在のフレームバッファへ直接描画する
        //     （RenderTexture == 0 のまま描くと直前の FBO へ描いてしまい、描画結果が不定になる）
        if (!_modelRenderTargets[0].IsValid())
        {
            if (!_warnedModelRenderTargetUnavailable)
            {
                _warnedModelRenderTargetUnavailable = true;
                CubismLog.Warning(
                    "[Live2D OpenGL]BeforeDrawModelRenderTarget: render target is not available ({0}x{1}). " +
                    "Fall back to drawing the model into the current framebuffer.",
                    _modelRenderTargetWidth, _modelRenderTargetHeight);
            }

            return;
        }

        // 別バッファに描画を開始
        _modelRenderTargets[0].BeginDraw();
        GL.Viewport(0, 0, (int)_modelRenderTargetWidth, (int)_modelRenderTargetHeight);
        _modelRenderTargets[0].Clear(0.0f, 0.0f, 0.0f, 0.0f);
    }

    /// <summary>
    /// モデル描画後のオフスクリーン設定（官方 AfterDrawModelRenderTarget、B7）。
    /// </summary>
    protected override void AfterDrawModelRenderTarget()
    {
        if (_modelRenderTargets.Count == 0 || !_modelRenderTargets[0].IsValid())
        {
            return;
        }

        // 元のバッファに描画する
        _modelRenderTargets[0].EndDraw();

        Shader.SetupShaderProgramForOffscreenRenderTarget(this);
        DrawRenderTargetQuad();

        GL.UseProgram(0);
    }

    /// <summary>
    /// モデル描画の実装（官方 DoDrawModel、.cpp:719-789）。
    /// </summary>
    protected override unsafe void DoDrawModel()
    {
        BeforeDrawModelRenderTarget();

        // モデル描画直前のFBOとビューポートを保存（B13: Profile ではなくローカル変数）
        GL.GetIntegerv(GL.GL_FRAMEBUFFER_BINDING, out int lastFBO);
        int[] lastViewport = new int[4];
        GL.GetIntegerv(GL.GL_VIEWPORT, lastViewport);

        //------------ Drawable 用クリッピングマスク・バッファ前処理方式の場合 ------------
        if (_drawableClippingManager != null)
        {
            PreDraw();

            // サイズが違う場合はここで作成しなおし
            for (int i = 0; i < _drawableClippingManager.RenderTextureCount; ++i)
            {
                if (_drawableMasks[i].BufferWidth != (int)_drawableClippingManager.ClippingMaskBufferSize.X ||
                    _drawableMasks[i].BufferHeight != (int)_drawableClippingManager.ClippingMaskBufferSize.Y)
                {
                    _drawableMasks[i].CreateRenderTarget(
                        (int)_drawableClippingManager.ClippingMaskBufferSize.X, (int)_drawableClippingManager.ClippingMaskBufferSize.Y);
                }
            }

            // N-2: マスク用 FBO の作成に失敗した場合は 1 度だけ警告する。
            //      実際の Clear / Viewport / マスク描画のスキップは各描画経路側で行う。
            ValidateMaskBuffers(CsmEnum.DrawableObjectType.Drawable);

            if (UseHighPrecisionMask)
            {
                _drawableClippingManager.SetupMatrixForHighPrecision(Model, false, CsmEnum.DrawableObjectType.Drawable);
            }
            else
            {
                _drawableClippingManager.SetupClippingContext(Model, this, lastFBO, lastViewport);
            }
        }

        //------------ Offscreen 用クリッピングマスク・バッファ前処理方式の場合 ------------
        if (_offscreenClippingManager != null)
        {
            PreDraw();

            for (int i = 0; i < _offscreenClippingManager.RenderTextureCount; ++i)
            {
                if (_offscreenMasks[i].BufferWidth != (int)_offscreenClippingManager.ClippingMaskBufferSize.X ||
                    _offscreenMasks[i].BufferHeight != (int)_offscreenClippingManager.ClippingMaskBufferSize.Y)
                {
                    _offscreenMasks[i].CreateRenderTarget(
                        (int)_offscreenClippingManager.ClippingMaskBufferSize.X, (int)_offscreenClippingManager.ClippingMaskBufferSize.Y);
                }
            }

            // N-2: マスク用 FBO の作成に失敗した場合は 1 度だけ警告する。
            ValidateMaskBuffers(CsmEnum.DrawableObjectType.Offscreen);

            if (UseHighPrecisionMask)
            {
                _offscreenClippingManager.SetupMatrixForHighPrecision(Model, false, CsmEnum.DrawableObjectType.Offscreen, GetMvpMatrix());
            }
            else
            {
                _offscreenClippingManager.SetupClippingContext(Model, this, lastFBO, lastViewport);
            }
        }

        // 上記クリッピング処理内でも一度PreDrawを呼ぶので注意!!
        PreDraw();

        if (GL.AlwaysClear)
        {
            var color = _lapp.BGColor;
            GL.ClearColor(color.R, color.G, color.B, color.A);
            GL.Clear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);
        }

        // モデルの描画順に従って描画する
        DrawObjectLoop(lastFBO, lastViewport);

        AfterDrawModelRenderTarget();
    }

    /// <summary>
    /// 描画オブジェクト（アートメッシュ、オフスクリーン）を描画するループ処理
    /// （官方 DrawObjectLoop、.cpp:791-833）。
    /// G1 门禁结论: renderOrder の配列長は drawableCount + offscreenCount（Ren で 198+24=222）。
    /// </summary>
    private unsafe void DrawObjectLoop(int lastFBO, int[] lastViewport)
    {
        int drawableCount = Model.GetDrawableCount();
        int offscreenCount = Model.GetOffscreenCount();
        int totalCount = drawableCount + offscreenCount;
        int* renderOrder = Model.GetRenderOrders();

        _currentOffscreen = null;
        _currentFBO = lastFBO;
        _modelRootFBO = lastFBO;

        // インデックスを描画順でソート（Drawable と Offscreen を統合）
        for (int i = 0; i < totalCount; ++i)
        {
            int order = renderOrder[i];

            if (i < drawableCount)
            {
                _sortedObjectsIndexList[order] = i;
                _sortedObjectsTypeList[order] = CsmEnum.DrawableObjectType.Drawable;
            }
            else
            {
                _sortedObjectsIndexList[order] = i - drawableCount;
                _sortedObjectsTypeList[order] = CsmEnum.DrawableObjectType.Offscreen;
            }
        }

        // 描画
        for (int i = 0; i < totalCount; ++i)
        {
            int objectIndex = _sortedObjectsIndexList[i];
            CsmEnum.DrawableObjectType objectType = _sortedObjectsTypeList[i];

            RenderObject(objectIndex, objectType);
        }

        while (_currentOffscreen != null)
        {
            // オフスクリーンが残っている場合は親オフスクリーンへの伝搬を行う
            SubmitDrawToParentOffscreen(_currentOffscreen.GetOffscreenIndex(), CsmEnum.DrawableObjectType.Offscreen);
        }
    }

    /// <summary>
    /// 各オブジェクトの描画処理を呼ぶ（官方 RenderObject、.cpp:835-852）。
    /// </summary>
    private void RenderObject(int objectIndex, CsmEnum.DrawableObjectType objectType)
    {
        switch (objectType)
        {
            case CsmEnum.DrawableObjectType.Drawable:
                DrawDrawable(objectIndex);
                break;
            case CsmEnum.DrawableObjectType.Offscreen:
                AddOffscreen(objectIndex);
                break;
            default:
                CubismLog.Error($"[Live2D OpenGL]Unknown drawable type: {objectType}");
                break;
        }
    }

    /// <summary>
    /// 描画オブジェクト（アートメッシュ）を描画する（官方 DrawDrawable、.cpp:854-930）。
    /// </summary>
    private unsafe void DrawDrawable(int drawableIndex)
    {
        // Drawableが表示状態でなければ処理をパスする
        if (!Model.GetDrawableDynamicFlagIsVisible(drawableIndex))
        {
            return;
        }

        SubmitDrawToParentOffscreen(drawableIndex, CsmEnum.DrawableObjectType.Drawable);

        // クリッピングマスク
        CubismClippingContext? clipContext = _drawableClippingManager != null
            ? _drawableClippingManager.ClippingContextListForDraw[drawableIndex]
            : null;

        // N-2: マスク用レンダーターゲットが作成できていない場合は、Viewport / Clear / マスク描画を行わない。
        //      RenderTexture == 0 のまま実行すると、バインド中の FBO（画面や親 FBO）を破壊してしまう。
        if (clipContext != null && UseHighPrecisionMask
            && IsMaskBufferUsable(CsmEnum.DrawableObjectType.Drawable, clipContext.BufferIndex)) // マスクを書く必要がある
        {
            int[] preHighPrecisionMaskViewport = new int[4];
            GL.GetIntegerv(GL.GL_VIEWPORT, preHighPrecisionMaskViewport);

            if (clipContext.IsUsing) // 書くことになっていた
            {
                // 生成したRenderTargetと同じサイズでビューポートを設定
                GL.Viewport(0, 0, (int)_drawableClippingManager!.ClippingMaskBufferSize.X, (int)_drawableClippingManager.ClippingMaskBufferSize.Y);

                PreDraw(); // バッファをクリアする

                // ---------- マスク描画処理 ----------
                // マスク用RenderTextureをactiveにセット
                GetDrawableMaskBuffer(clipContext.BufferIndex).BeginDraw(_currentFBO);

                // マスクをクリアする
                // 1が無効（描かれない）領域、0が有効（描かれる）領域。
                GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
                GL.Clear(GL.GL_COLOR_BUFFER_BIT);
            }

            {
                int clipDrawCount = clipContext.ClippingIdCount;
                for (int index = 0; index < clipDrawCount; index++)
                {
                    int clipDrawIndex = clipContext.ClippingIdList[index];

                    // 頂点情報が更新されておらず、信頼性がない場合は描画をパスする
                    if (!Model.GetDrawableDynamicFlagVertexPositionsDidChange(clipDrawIndex))
                    {
                        continue;
                    }

                    IsCulling = Model.GetDrawableCulling(clipDrawIndex) != 0;

                    // 今回専用の変換を適用して描く
                    // チャンネルも切り替える必要がある(A,R,G,B)
                    ClippingContextBufferForMask = clipContext;

                    DrawMeshOpenGL(Model, clipDrawIndex);
                }
            }

            {
                // --- 後処理 ---
                GetDrawableMaskBuffer(clipContext.BufferIndex).EndDraw();
                ClippingContextBufferForMask = null;
                GL.Viewport(preHighPrecisionMaskViewport[0], preHighPrecisionMaskViewport[1],
                    preHighPrecisionMaskViewport[2], preHighPrecisionMaskViewport[3]);

                PreDraw(); // バッファをクリアする
            }
        }
        else if (clipContext != null && UseHighPrecisionMask)
        {
            // N-2: マスク用 RT が使えないのでマスク生成をスキップする（Clear を画面 / 親 FBO に落とさない）
            WarnMaskBufferUnavailable(CsmEnum.DrawableObjectType.Drawable);
        }

        // クリッピングマスクをセットする
        ClippingContextBufferForDrawable = clipContext;

        IsCulling = Model.GetDrawableCulling(drawableIndex) != 0;

        DrawMeshOpenGL(Model, drawableIndex);
    }

    /// <summary>
    /// 親オフスクリーンへオフスクリーンの描画結果の伝搬を試みる
    /// （官方 SubmitDrawToParentOffscreen、.cpp:932-984）。
    /// </summary>
    private unsafe void SubmitDrawToParentOffscreen(int objectIndex, CsmEnum.DrawableObjectType objectType)
    {
        if (_currentOffscreen == null || objectIndex == CubismModel.CubismNoIndex_Offscreen)
        {
            return;
        }

        int* ownerIndices = Model.GetOffscreenOwnerIndices();
        int currentOwnerIndex = ownerIndices[_currentOffscreen.GetOffscreenIndex()];

        // オーナーが不明な場合は処理を終了
        if (currentOwnerIndex == CubismModel.CubismNoIndex_Offscreen)
        {
            return;
        }

        int targetParentIndex;
        // 描画オブジェクトのタイプ別に親パーツのインデックスを取得
        switch (objectType)
        {
            case CsmEnum.DrawableObjectType.Drawable:
                targetParentIndex = Model.GetDrawableParentPartIndex(objectIndex);
                break;
            case CsmEnum.DrawableObjectType.Offscreen:
                targetParentIndex = Model.GetPartParentPartIndex(ownerIndices[objectIndex]);
                break;
            default:
                return;
        }

        // 階層を辿って現在のオフスクリーンのオーナーのパーツがいたら処理を終了する。
        while (targetParentIndex != CubismModel.CubismNoIndex_Parent)
        {
            // オブジェクトの親が現在のオーナーと同じ場合は処理を終了
            if (targetParentIndex == currentOwnerIndex)
            {
                return;
            }

            targetParentIndex = Model.GetPartParentPartIndex(targetParentIndex);
        }

        // 呼び出し元の描画オブジェクトは現オフスクリーンの描画対象でない。
        // つまり描画順グループの仕様により、現オフスクリーンの描画対象は全て描画完了しているので
        // 現オフスクリーンを描画する。
        DrawOffscreen(_currentOffscreen);

        // さらに親のオフスクリーンに伝搬可能なら伝搬する。
        SubmitDrawToParentOffscreen(objectIndex, objectType);
    }

    /// <summary>
    /// 描画オブジェクト（オフスクリーン）を追加する（官方 AddOffscreen、.cpp:986-1041）。
    /// </summary>
    private unsafe void AddOffscreen(int offscreenIndex)
    {
        int* ownerIndices = Model.GetOffscreenOwnerIndices();

        // 以前のオフスクリーンレンダリングターゲットを親に伝搬する処理を追加する
        if (_currentOffscreen != null && _currentOffscreen.GetOffscreenIndex() != offscreenIndex)
        {
            bool isParent = false;
            int ownerIndex = ownerIndices[offscreenIndex];
            int parentIndex = Model.GetPartParentPartIndex(ownerIndex);

            int currentOffscreenIndex = _currentOffscreen.GetOffscreenIndex();
            int currentOffscreenOwnerIndex = ownerIndices[currentOffscreenIndex];
            while (parentIndex != CubismModel.CubismNoIndex_Parent)
            {
                if (parentIndex == currentOffscreenOwnerIndex)
                {
                    isParent = true;
                    break;
                }
                parentIndex = Model.GetPartParentPartIndex(parentIndex);
            }

            if (!isParent)
            {
                // 現在のオフスクリーンレンダリングターゲットがあるなら、親に伝搬する
                SubmitDrawToParentOffscreen(offscreenIndex, CsmEnum.DrawableObjectType.Offscreen);
            }
        }

        CubismOffscreenRenderTarget_OpenGLES2 offscreen = _offscreenList[offscreenIndex];
        offscreen.SetOffscreenRenderTarget(_modelRenderTargetWidth, _modelRenderTargetHeight);

        // S6: FBO を作成できなかった場合はこのオフスクリーンへの描画をスキップする。
        // RenderTexture == 0 のまま BeginDraw / Clear / Draw を続けると、直前の FBO（画面を含む）に
        // 対して描いてしまい、描画結果が不定になる。
        CubismRenderTarget_OpenGLES2? offscreenTarget = offscreen.GetRenderTarget();
        if (offscreenTarget == null || !offscreenTarget.IsValid())
        {
            // N-2: SetOffscreenRenderTarget で借りたプールスロットを必ず返却する。
            //      返却しないと slot が IsUsed のまま残り、以降このスロットを二度と借りられない
            //      （= スロット永久占用。使用数カウントも戻らない）。
            offscreen.StopUsingRenderTexture();

            if (!_warnedOffscreenRenderTargetUnavailable)
            {
                _warnedOffscreenRenderTargetUnavailable = true;
                CubismLog.Warning(
                    "[Live2D OpenGL]AddOffscreen: render target for offscreen {0} is not available ({1}x{2}). " +
                    "Skip drawing into this offscreen.",
                    offscreenIndex, _modelRenderTargetWidth, _modelRenderTargetHeight);
            }

            return;
        }

        // 以前のオフスクリーンレンダリングターゲットを取得
        CubismOffscreenRenderTarget_OpenGLES2? oldOffscreen = offscreen.GetParentPartOffscreen();

        offscreen.SetOldOffscreen(oldOffscreen);

        int oldFBO = 0;
        if (oldOffscreen != null)
        {
            oldFBO = oldOffscreen.GetRenderTarget()?.RenderTexture ?? 0;
        }

        if (oldFBO == 0)
        {
            oldFBO = _modelRootFBO; // ルートのFBOを使用する
        }

        // 別バッファに描画を開始
        CubismRenderTarget_OpenGLES2 renderTarget = offscreenTarget;
        renderTarget.BeginDraw(oldFBO);
        GL.Viewport(0, 0, (int)_modelRenderTargetWidth, (int)_modelRenderTargetHeight);
        renderTarget.Clear(0.0f, 0.0f, 0.0f, 0.0f);

        // 現在のオフスクリーンレンダリングターゲットを設定
        _currentOffscreen = offscreen;
        _currentFBO = renderTarget.RenderTexture;
    }

    /// <summary>
    /// 描画オブジェクト（オフスクリーン）を描画する（官方 DrawOffscreen、.cpp:1043-1112）。
    /// </summary>
    private unsafe void DrawOffscreen(CubismOffscreenRenderTarget_OpenGLES2 currentOffscreen)
    {
        int offscreenIndex = currentOffscreen.GetOffscreenIndex();

        // N-2: 描画先（FBO）を作成できなかったオフスクリーンは合成しない。
        //      AddOffscreen の失敗時点では FBO を切り替えていないため現在の描画先はそのまま維持し、
        //      状態だけ 1 つ親へ戻す（GetRenderTarget() が null の場合の例外も防ぐ）。
        if (currentOffscreen.GetRenderTarget() is not { } offscreenTarget || offscreenTarget.IsValid() is false)
        {
            _currentOffscreen = _currentOffscreen?.GetOldOffscreen();
            ClippingContextBufferForOffscreen = null;
            ClippingContextBufferForMask = null;
            return;
        }

        // クリッピングマスク
        CubismClippingContext? clipContext = _offscreenClippingManager != null
            ? _offscreenClippingManager.ClippingContextListForOffscreen[offscreenIndex]
            : null;

        // N-2: マスク用レンダーターゲットが作成できていない場合は、Viewport / Clear / マスク描画を行わない。
        //      RenderTexture == 0 のまま実行すると、バインド中の FBO（親オフスクリーン）を破壊してしまう。
        if (clipContext != null && UseHighPrecisionMask
            && IsMaskBufferUsable(CsmEnum.DrawableObjectType.Offscreen, clipContext.BufferIndex)) // マスクを書く必要がある
        {
            int[] preHighPrecisionMaskViewport = new int[4];
            GL.GetIntegerv(GL.GL_VIEWPORT, preHighPrecisionMaskViewport);

            if (clipContext.IsUsing) // 書くことになっていた
            {
                // 生成したRenderTargetと同じサイズでビューポートを設定
                GL.Viewport(0, 0, (int)_offscreenClippingManager!.ClippingMaskBufferSize.X, (int)_offscreenClippingManager.ClippingMaskBufferSize.Y);

                PreDraw(); // バッファをクリアする

                // ---------- マスク描画処理 ----------
                // マスク用RenderTextureをactiveにセット
                GetOffscreenMaskBuffer(clipContext.BufferIndex).BeginDraw(_currentFBO);

                // マスクをクリアする
                GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
                GL.Clear(GL.GL_COLOR_BUFFER_BIT);
            }

            {
                int clipDrawCount = clipContext.ClippingIdCount;
                for (int index = 0; index < clipDrawCount; index++)
                {
                    int clipDrawIndex = clipContext.ClippingIdList[index];

                    // 頂点情報が更新されておらず、信頼性がない場合は描画をパスする
                    if (!Model.GetDrawableDynamicFlagVertexPositionsDidChange(clipDrawIndex))
                    {
                        continue;
                    }

                    IsCulling = Model.GetDrawableCulling(clipDrawIndex) != 0;

                    // 今回専用の変換を適用して描く
                    // チャンネルも切り替える必要がある(A,R,G,B)
                    ClippingContextBufferForMask = clipContext;

                    DrawMeshOpenGL(Model, clipDrawIndex);
                }
            }

            {
                // --- 後処理 ---
                GetOffscreenMaskBuffer(clipContext.BufferIndex).EndDraw();
                ClippingContextBufferForMask = null;
                GL.Viewport(preHighPrecisionMaskViewport[0], preHighPrecisionMaskViewport[1],
                    preHighPrecisionMaskViewport[2], preHighPrecisionMaskViewport[3]);

                PreDraw(); // バッファをクリアする
            }
        }
        else if (clipContext != null && UseHighPrecisionMask)
        {
            // N-2: マスク用 RT が使えないのでマスク生成をスキップする（Clear を親 FBO に落とさない）
            WarnMaskBufferUnavailable(CsmEnum.DrawableObjectType.Offscreen);
        }

        // クリッピングマスクをセットする
        ClippingContextBufferForOffscreen = clipContext;

        IsCulling = Model.GetOffscreenCulling(offscreenIndex) != 0;

        DrawOffscreenOpenGL(Model, currentOffscreen);
    }

    /// <summary>
    /// オフスクリーンを画面上に描画する（官方 DrawOffscreenOpenGL、.cpp:1166-1202）。
    /// </summary>
    private void DrawOffscreenOpenGL(CubismModel model, CubismOffscreenRenderTarget_OpenGLES2 offscreen)
    {
        // 裏面描画の有効・無効
        if (IsCulling)
        {
            GL.Enable(GL.GL_CULL_FACE);
        }
        else
        {
            GL.Disable(GL.GL_CULL_FACE);
        }

        GL.FrontFace(GL.GL_CCW);    // Cubism SDK OpenGLはマスク・アートメッシュ共にCCWが表面

        var renderTarget = offscreen.GetRenderTarget()!;
        renderTarget.EndDraw();
        _currentOffscreen = _currentOffscreen?.GetOldOffscreen();
        _currentFBO = renderTarget.GetOldFBO();

        Shader.SetupShaderProgramForOffscreen(this, model, offscreen);

        // ポリゴンメッシュを描画する
        DrawRenderTargetQuad();

        // 後処理
        offscreen.StopUsingRenderTexture();
        GL.UseProgram(0);
        ClippingContextBufferForOffscreen = null;
        ClippingContextBufferForMask = null;
    }

    public int GetBindedTextureId(int textureId)
    {
        return (_textures[textureId] != 0) ? _textures[textureId] : -1;
    }

    /// <summary>
    /// レンダラが保持するリソースを解放する（§2.F 漏点①の修正を含む）。
    /// </summary>
    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        // シェーダプログラム
        Shader.ReleaseShaderProgram();

        // クリッピングマネージャ（マスクバッファ含む）
        _drawableClippingManager?.Dispose();
        _drawableClippingManager = null;
        _offscreenClippingManager?.Dispose();
        _offscreenClippingManager = null;

        // モデル全体のレンダーターゲット
        foreach (var target in _modelRenderTargets)
        {
            target.DestroyRenderTarget();
        }
        _modelRenderTargets.Clear();

        // Drawable / Offscreen マスク
        foreach (var mask in _drawableMasks)
        {
            mask.DestroyRenderTarget();
        }
        _drawableMasks.Clear();

        foreach (var mask in _offscreenMasks)
        {
            mask.DestroyRenderTarget();
        }
        _offscreenMasks.Clear();

        // オフスクリーン（G2-F3 修正）
        // 公式デストラクタ（FW5/OpenGL/CubismRenderer_OpenGLES2.cpp:494-525）は
        // 自分が持つ _modelRenderTargets / _drawableMasks / _offscreenMasks だけを破棄し、
        // 共有シングルトン CubismOffscreenManager_OpenGLES2 のレンダーターゲットには触れない。
        // manager の解放はアプリ層（公式 Samples/OpenGL/.../LAppLive2DManager.cpp:77 の ReleaseInstance）が行う。
        // ここでは本 renderer が借りている target を使用終了状態に戻すだけに留める
        // （他 renderer が現在使用中の target は StopUsingRenderTexture の対象外なので誤傷しない）。
        foreach (var offscreen in _offscreenList)
        {
            offscreen.StopUsingRenderTexture();
        }
        _offscreenList.Clear();

        // GL オブジェクト（§2.F 漏点①: 従来は未解放だった）
        GL.DeleteBuffer(VertexBuffer);
        GL.DeleteBuffer(IndexBuffer);
        GL.DeleteBuffer(_quadVertexBuffer);
        GL.DeleteBuffer(_quadIndexBuffer);
        GL.DeleteVertexArray(VertexArray);
    }
}
