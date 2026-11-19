using System.Numerics;
using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.Framework.Model;

public class CubismModel : IDisposable
{
    /// <summary>
    /// 親が存在しない場合のインデックス値
    /// </summary>
    public const int CubismNoIndex_Parent = -1;
    /// <summary>
    /// 参照する Offscreen が存在しない場合のインデックス値
    /// </summary>
    public const int CubismNoIndex_Offscreen = -1;

    /// <summary>
    /// テクスチャのカリング設定を管理するための構造体（官方 CubismModel::CullingData）
    /// </summary>
    public struct CullingData
    {
        /// <summary>
        /// SDK 側の設定で上書きするか
        /// </summary>
        public bool IsOverridden;
        /// <summary>
        /// カリング情報（0: 無効 / 1: 有効）
        /// </summary>
        public int IsCulling;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public CullingData(bool isOverridden, int isCulling)
        {
            IsOverridden = isOverridden;
            IsCulling = isCulling;
        }
    }

    /// <summary>
    /// パラメータのリピート設定の上書きを管理するための構造体（官方 CubismModel::ParameterRepeatData）
    /// </summary>
    public struct ParameterRepeatData
    {
        /// <summary>
        /// SDK 側の設定で上書きするか
        /// </summary>
        public bool IsOverridden;
        /// <summary>
        /// リピートするか
        /// </summary>
        public bool IsParameterRepeated;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public ParameterRepeatData(bool isOverridden, bool isParameterRepeated)
        {
            IsOverridden = isOverridden;
            IsParameterRepeated = isParameterRepeated;
        }
    }

    /// <summary>
    /// モデル
    /// </summary>
    public IntPtr Model { get; }

    public readonly List<string> ParameterIds = [];
    public readonly List<string> PartIds = [];
    public readonly List<string> DrawableIds = [];

    /// <summary>
    /// 存在していないパーツの不透明度のリスト
    /// </summary>
    protected readonly Dictionary<int, float> _notExistPartOpacities = [];
    /// <summary>
    /// 存在していないパーツIDのリスト
    /// </summary>
    protected readonly Dictionary<string, int> _notExistPartId = [];
    /// <summary>
    /// 存在していないパラメータの値のリスト
    /// </summary>
    protected readonly Dictionary<int, float> _notExistParameterValues = [];
    /// <summary>
    /// 存在していないパラメータIDのリスト
    /// </summary>
    protected readonly Dictionary<string, int> _notExistParameterId = [];
    /// <summary>
    /// 保存されたパラメータ
    /// </summary>
    protected readonly List<float> _savedParameters = [];
    /// <summary>
    /// パラメータの値のリスト
    /// </summary>
    protected unsafe float* _parameterValues;
    /// <summary>
    /// パラメータの最大値のリスト
    /// </summary>
    protected unsafe float* _parameterMaximumValues;
    /// <summary>
    /// パラメータの最小値のリスト
    /// </summary>
    protected unsafe float* _parameterMinimumValues;
    /// <summary>
    /// パーツの不透明度のリスト
    /// </summary>
    protected unsafe float* _partOpacities;
    /// <summary>
    /// モデルの不透明度
    /// </summary>
    protected float _modelOpacity;

    /// <summary>
    /// パーツ階層のリスト（5-r.5 で _partChildDrawables から置き換え。再代入禁止：乗算色クラスが参照を保持する）
    /// </summary>
    protected readonly List<CubismModelPartInfo> _partsHierarchy = [];
    /// <summary>
    /// パラメータのリピート設定のリスト（構造体を in-place 更新するため配列）
    /// </summary>
    protected ParameterRepeatData[] _userParameterRepeatDataList = [];
    /// <summary>
    /// Drawable のカリング設定のリスト（構造体を in-place 更新するため配列）
    /// </summary>
    protected CullingData[] _userDrawableCullings = [];
    /// <summary>
    /// Offscreen のカリング設定のリスト（構造体を in-place 更新するため配列）
    /// </summary>
    protected CullingData[] _userOffscreenCullings = [];
    /// <summary>
    /// 乗算色・スクリーン色（5-r.5 で独立クラスへ分離）
    /// </summary>
    protected readonly CubismModelMultiplyAndScreenColor _overrideMultiplyAndScreenColors;
    /// <summary>
    /// パラメータのリピート設定を SDK 側で上書きするか（官方 5-r.5 の初期値は true）
    /// </summary>
    protected bool _isOverriddenParameterRepeat;
    /// <summary>
    /// モデルのカリング設定をすべて上書きするか？
    /// </summary>
    protected bool _isOverriddenCullings;
    /// <summary>
    /// ブレンドモードを使用するか
    /// </summary>
    protected bool _isBlendModeEnabled;

    /// <summary>
    /// コンストラクタ（官方は protected、CubismMoc からのみ生成できる。C# は同一アセンブリの CubismMoc が生成する）
    /// </summary>
    protected internal unsafe CubismModel(IntPtr model)
    {
        Model = model;
        _modelOpacity = 1.0f;

        // 官方 U04: コンストラクタ初期化子リストで this と _partsHierarchy を渡すのと同じ。
        // この時点で _partsHierarchy は空だが、参照は Initialize() 以降で埋まるので問題ない。
        _overrideMultiplyAndScreenColors = new CubismModelMultiplyAndScreenColor(this, _partsHierarchy);

        _isOverriddenParameterRepeat = true;
        _isOverriddenCullings = false;
        _isBlendModeEnabled = false;

        Initialize();
    }

    public virtual void Dispose()
    {
        CubismFramework.DeallocateAligned(Model);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// モデルの初期化（官方 CubismModel::Initialize）
    /// </summary>
    protected unsafe void Initialize()
    {
        _parameterValues = CubismCore.GetParameterValues(Model);
        _partOpacities = CubismCore.GetPartOpacities(Model);
        _parameterMaximumValues = CubismCore.GetParameterMaximumValues(Model);
        _parameterMinimumValues = CubismCore.GetParameterMinimumValues(Model);

        {
            var parameterIds = CubismCore.GetParameterIds(Model);
            var parameterCount = CubismCore.GetParameterCount(Model);

            _userParameterRepeatDataList = new ParameterRepeatData[parameterCount];

            for (int i = 0; i < parameterCount; ++i)
            {
                var str = new string(parameterIds[i]);
                ParameterIds.Add(CubismFramework.CubismIdManager.GetId(str));

                _userParameterRepeatDataList[i] = new ParameterRepeatData(false, false);
            }
        }

        var partCount = CubismCore.GetPartCount(Model);
        var drawableCount = CubismCore.GetDrawableCount(Model);
        var offscreenCount = CubismCore.GetOffscreenCount(Model);

        // Part
        {
            var partIds = CubismCore.GetPartIds(Model);

            for (int i = 0; i < partCount; ++i)
            {
                var str = new string(partIds[i]);
                PartIds.Add(CubismFramework.CubismIdManager.GetId(str));
            }
        }

        // Drawable
        {
            var drawableIds = CubismCore.GetDrawableIds(Model);

            _userDrawableCullings = new CullingData[drawableCount];

            for (int i = 0; i < drawableCount; ++i)
            {
                var str = new string(drawableIds[i]);
                DrawableIds.Add(CubismFramework.CubismIdManager.GetId(str));
                _userDrawableCullings[i] = new CullingData(false, 0);
            }
        }

        // Offscreen
        _userOffscreenCullings = new CullingData[offscreenCount];
        for (int i = 0; i < offscreenCount; ++i)
        {
            _userOffscreenCullings[i] = new CullingData(false, 0);
        }

        // Multiply and Screen
        _overrideMultiplyAndScreenColors.Initialize(partCount, drawableCount, offscreenCount);

        // BlendMode
        InitializeBlendMode();

        // PartsHierarchy
        SetupPartsHierarchy();
    }

    /// <summary>
    /// ブレンドモードを使用するか判定する（官方 CubismModel::InitializeBlendMode）
    /// </summary>
    protected unsafe void InitializeBlendMode()
    {
        var drawableCount = CubismCore.GetDrawableCount(Model);

        // オフスクリーンが存在するか、Drawable のブレンドモードで ColorBlend、AlphaBlend を使用するのであればブレンドモードを有効にする。
        if (CubismCore.GetOffscreenCount(Model) > 0)
        {
            _isBlendModeEnabled = true;
        }
        else
        {
            var blendMode = new CsmBlendMode();
            var blendModes = CubismCore.GetDrawableBlendModes(Model);
            for (int i = 0; i < drawableCount; ++i)
            {
                blendMode.SetBlendMode(blendModes[i]);
                var colorBlendType = blendMode.GetColorBlendType();
                var alphaBlendType = blendMode.GetAlphaBlendType();

                // NormalOver、AddCompatible、MultiplyCompatible 以外であればブレンドモードを有効にする。
                if (!(colorBlendType == CsmEnum.CsmColorBlendType_Normal && alphaBlendType == CsmEnum.CsmAlphaBlendType_Over) &&
                    colorBlendType != CsmEnum.CsmColorBlendType_AddCompatible &&
                    colorBlendType != CsmEnum.CsmColorBlendType_MultiplyCompatible)
                {
                    _isBlendModeEnabled = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// パーツ階層を構築する（官方 CubismModel::SetupPartsHierarchy）
    /// </summary>
    protected unsafe void SetupPartsHierarchy()
    {
        _partsHierarchy.Clear();

        // すべてのパーツのパーツ情報管理構造体を作成
        var partCount = CubismCore.GetPartCount(Model);
        for (int i = 0; i < partCount; ++i)
        {
            _partsHierarchy.Add(new CubismModelPartInfo());
        }

        // Part ごとに親パーツを取得し、親パーツの子 object リストに追加する
        for (int i = 0; i < partCount; ++i)
        {
            var parentPartIndex = GetPartParentPartIndex(i);

            if (parentPartIndex == CubismNoIndex_Parent)
            {
                continue;
            }

            for (int partIndex = 0; partIndex < _partsHierarchy.Count; ++partIndex)
            {
                if (partIndex == parentPartIndex)
                {
                    _partsHierarchy[partIndex].Objects.Add(
                        new CubismModelObjectInfo(i, CubismModelObjectType.ObjectType_Parts));
                    break;
                }
            }
        }

        // Drawable ごとに親パーツを取得し、親パーツの子 object リストに追加する
        var drawableCount = CubismCore.GetDrawableCount(Model);
        for (int i = 0; i < drawableCount; ++i)
        {
            var parentPartIndex = GetDrawableParentPartIndex(i);

            if (parentPartIndex == CubismNoIndex_Parent)
            {
                continue;
            }

            for (int partIndex = 0; partIndex < _partsHierarchy.Count; ++partIndex)
            {
                if (partIndex == parentPartIndex)
                {
                    _partsHierarchy[partIndex].Objects.Add(
                        new CubismModelObjectInfo(i, CubismModelObjectType.ObjectType_Drawable));
                    break;
                }
            }
        }

        // ここまででモデルのパーツ構造が完成

        // パーツ子描画オブジェクト情報構造体を作成
        for (int i = 0; i < _partsHierarchy.Count; ++i)
        {
            GetPartChildDrawObjects(i);
        }
    }

    /// <summary>
    /// パーツの子描画オブジェクト情報を再帰的に構築する（官方 CubismModel::GetPartChildDrawObjects）。
    /// Offscreen を持つパーツは、その Offscreen インデックスも子情報に含める。
    /// </summary>
    /// <param name="partInfoIndex">パーツ情報のインデックス</param>
    public unsafe void GetPartChildDrawObjects(int partInfoIndex)
    {
        if (_partsHierarchy[partInfoIndex].GetChildObjectCount() < 1)
        {
            return;
        }

        var childDrawObjects = _partsHierarchy[partInfoIndex].ChildDrawObjects;

        // 既に childDrawObjects が処理されている場合はスキップ
        if (childDrawObjects.DrawableIndices.Count != 0 ||
            childDrawObjects.OffscreenIndices.Count != 0)
        {
            return;
        }

        var objects = _partsHierarchy[partInfoIndex].Objects;

        for (int i = 0; i < objects.Count; ++i)
        {
            if (objects[i].ObjectType == CubismModelObjectType.ObjectType_Parts)
            {
                // 子のパーツの場合、再帰的に子 objects を取得
                GetPartChildDrawObjects(objects[i].ObjectIndex);

                // 子パーツの子 Drawable、Offscreen を取得
                var childToChildDrawObjects = _partsHierarchy[objects[i].ObjectIndex].ChildDrawObjects;

                for (int j = 0; j < childToChildDrawObjects.DrawableIndices.Count; ++j)
                {
                    // 孫 Drawable をパーツの子 Drawable に追加
                    childDrawObjects.DrawableIndices.Add(childToChildDrawObjects.DrawableIndices[j]);
                }
                for (int j = 0; j < childToChildDrawObjects.OffscreenIndices.Count; ++j)
                {
                    // 孫 Offscreen をパーツの子 Offscreen に追加
                    childDrawObjects.OffscreenIndices.Add(childToChildDrawObjects.OffscreenIndices[j]);
                }

                // Offscreen の確認
                var offscreenIndex = CubismCore.GetPartOffscreenIndices(Model)[objects[i].ObjectIndex];
                if (offscreenIndex != CubismNoIndex_Offscreen)
                {
                    // Offscreen が存在する場合、パーツの子 Offscreen に追加
                    childDrawObjects.OffscreenIndices.Add(offscreenIndex);
                }
            }
            else if (objects[i].ObjectType == CubismModelObjectType.ObjectType_Drawable)
            {
                // Drawable の場合、パーツの子 Drawable に追加
                childDrawObjects.DrawableIndices.Add(objects[i].ObjectIndex);
            }
        }
    }

    /// <summary>
    /// パーツ階層を取得する（官方 CubismModel::GetPartsHierarchy）
    /// </summary>
    public IReadOnlyList<CubismModelPartInfo> GetPartsHierarchy()
    {
        return _partsHierarchy;
    }

    /// <summary>
    /// モデルのパラメータを更新する。
    /// </summary>
    public void Update()
    {
        // Update model.
        CubismCore.UpdateModel(Model);
        // Reset dynamic drawable flags.
        CubismCore.ResetDrawableDynamicFlags(Model);
    }

    /// <summary>
    /// Pixel単位でキャンバスの幅の取得
    /// </summary>
    /// <returns>キャンバスの幅(pixel)</returns>
    public float GetCanvasWidthPixel()
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        CubismCore.ReadCanvasInfo(Model, out var tmpSizeInPixels, out _, out _);

        return tmpSizeInPixels.X;
    }

    /// <summary>
    /// Pixel単位でキャンバスの高さの取得
    /// </summary>
    /// <returns>キャンバスの高さ(pixel)</returns>
    public float GetCanvasHeightPixel()
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        CubismCore.ReadCanvasInfo(Model, out var tmpSizeInPixels, out _, out _);

        return tmpSizeInPixels.Y;
    }

    /// <summary>
    /// PixelsPerUnitを取得する。
    /// </summary>
    /// <returns>PixelsPerUnit</returns>
    public float GetPixelsPerUnit()
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        CubismCore.ReadCanvasInfo(Model, out _, out _, out var tmpPixelsPerUnit);

        return tmpPixelsPerUnit;
    }

    /// <summary>
    /// Unit単位でキャンバスの幅の取得
    /// </summary>
    /// <returns>キャンバスの幅(Unit)</returns>
    public float GetCanvasWidth()
    {
        CubismCore.ReadCanvasInfo(Model, out var tmpSizeInPixels, out _, out var tmpPixelsPerUnit);

        return tmpSizeInPixels.X / tmpPixelsPerUnit;
    }

    /// <summary>
    /// Unit単位でキャンバスの高さの取得
    /// </summary>
    /// <returns>キャンバスの高さ(Unit)</returns>
    public float GetCanvasHeight()
    {
        CubismCore.ReadCanvasInfo(Model, out var tmpSizeInPixels, out _, out var tmpPixelsPerUnit);

        return tmpSizeInPixels.Y / tmpPixelsPerUnit;
    }

    /// <summary>
    /// パーツのインデックスを取得する。
    /// </summary>
    /// <param name="partId">パーツのID</param>
    /// <returns>パーツのインデックス</returns>
    public int GetPartIndex(string partId)
    {
        int partIndex = PartIds.IndexOf(partId);
        if (partIndex != -1)
        {
            return partIndex;
        }

        int partCount = CubismCore.GetPartCount(Model);

        // モデルに存在していない場合、非存在パーツIDリスト内にあるかを検索し、そのインデックスを返す
        if (_notExistPartId.TryGetValue(partId, out var item))
        {
            return item;
        }

        // 非存在パーツIDリストにない場合、新しく要素を追加する
        partIndex = partCount + _notExistPartId.Count;

        _notExistPartId.TryAdd(partId, partIndex);
        _notExistPartOpacities.Add(partIndex, 0);

        return partIndex;
    }

    /// <summary>
    /// パーツのIDを取得する。
    /// </summary>
    /// <param name="partIndex">パーツのIndex</param>
    /// <returns>パーツのID</returns>
    public string GetPartId(int partIndex)
    {
        if (0 <= partIndex && partIndex < PartIds.Count)
        {
            throw new IndexOutOfRangeException("Out of PartIds size");
        }
        return PartIds[partIndex];
    }

    /// <summary>
    /// パーツの個数を取得する。
    /// </summary>
    /// <returns>パーツの個数</returns>
    public int GetPartCount()
    {
        return CubismCore.GetPartCount(Model);
    }

    /// <summary>
    /// パーツの親パーツのインデックスリストを取得する。
    /// </summary>
    /// <returns>パーツの親パーツのインデックスリスト</returns>
    public unsafe int* GetPartParentPartIndices()
    {
        return CubismCore.GetPartParentPartIndices(Model);
    }

    /// <summary>
    /// パーツの Offscreen インデックスリストを取得する。
    /// </summary>
    /// <returns>パーツの Offscreen インデックスリスト</returns>
    public unsafe int* GetPartOffscreenIndices()
    {
        return CubismCore.GetPartOffscreenIndices(Model);
    }

    /// <summary>
    /// パーツの親パーツのインデックスを取得する。
    /// </summary>
    /// <param name="partIndex">パーツのインデックス</param>
    /// <returns>親パーツのインデックス。存在しない場合は CubismNoIndex_Parent</returns>
    public unsafe int GetPartParentPartIndex(int partIndex)
    {
        return CubismCore.GetPartParentPartIndices(Model)[partIndex];
    }

    /// <summary>
    /// パーツの不透明度を設定する。
    /// </summary>
    /// <param name="partId">パーツのID</param>
    /// <param name="opacity">不透明度</param>
    public void SetPartOpacity(string partId, float opacity)
    {
        // 高速化のためにPartIndexを取得できる機構になっているが、外部からの設定の時は呼び出し頻度が低いため不要
        int index = GetPartIndex(partId);

        if (index < 0)
        {
            return; // パーツが無いのでスキップ
        }

        SetPartOpacity(index, opacity);
    }

    /// <summary>
    /// パーツの不透明度を設定する。
    /// </summary>
    /// <param name="partIndex">パーツのインデックス</param>
    /// <param name="opacity">パーツの不透明度</param>
    public unsafe void SetPartOpacity(int partIndex, float opacity)
    {
        if (_notExistPartOpacities.ContainsKey(partIndex))
        {
            _notExistPartOpacities[partIndex] = opacity;
            return;
        }

        //インデックスの範囲内検知
        if (0 > partIndex || partIndex >= GetPartCount())
        {
            throw new ArgumentException($"partIndex out of range");
        }

        _partOpacities[partIndex] = opacity;
    }

    /// <summary>
    /// パーツの不透明度を取得する。
    /// </summary>
    /// <param name="partId">パーツのID</param>
    /// <returns>パーツの不透明度</returns>
    public float GetPartOpacity(string partId)
    {
        // 高速化のためにPartIndexを取得できる機構になっているが、外部からの設定の時は呼び出し頻度が低いため不要
        int index = GetPartIndex(partId);

        if (index < 0)
        {
            return 0; //パーツが無いのでスキップ
        }

        return GetPartOpacity(index);
    }

    /// <summary>
    /// パーツの不透明度を取得する。
    /// </summary>
    /// <param name="partIndex">パーツのインデックス</param>
    /// <returns>パーツの不透明度</returns>
    public unsafe float GetPartOpacity(int partIndex)
    {
        if (_notExistPartOpacities.TryGetValue(partIndex, out float value))
        {
            // モデルに存在しないパーツIDの場合、非存在パーツリストから不透明度を返す
            return value;
        }

        //インデックスの範囲内検知
        if (0 > partIndex || partIndex >= GetPartCount())
        {
            throw new ArgumentException($"partIndex out of range");
        }

        return _partOpacities[partIndex];
    }

    /// <summary>
    /// パラメータのインデックスを取得する。
    /// </summary>
    /// <param name="parameterId">パラメータID</param>
    /// <returns>パラメータのインデックス</returns>
    public int GetParameterIndex(string parameterId)
    {
        int parameterIndex = ParameterIds.IndexOf(parameterId);
        if (parameterIndex != -1)
        {
            return parameterIndex;
        }

        // モデルに存在していない場合、非存在パラメータIDリスト内を検索し、そのインデックスを返す
        if (_notExistParameterId.TryGetValue(parameterId, out var data))
        {
            return data;
        }

        // 非存在パラメータIDリストにない場合、新しく要素を追加する
        parameterIndex = CubismCore.GetParameterCount(Model) + _notExistParameterId.Count;

        _notExistParameterId.TryAdd(parameterId, parameterIndex);
        _notExistParameterValues.Add(parameterIndex, 0);

        return parameterIndex;
    }

    /// <summary>
    /// パラメータの個数を取得する。
    /// </summary>
    /// <returns>パラメータの個数</returns>
    public int GetParameterCount()
    {
        return CubismCore.GetParameterCount(Model);
    }

    /// <summary>
    /// パラメータの種類を取得する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <returns>csmParameterType_Normal -> 通常のパラメータ
    /// csmParameterType_BlendShape -> ブレンドシェイプパラメータ</returns>
    public unsafe int GetParameterType(int parameterIndex)
    {
        return CubismCore.GetParameterTypes(Model)[parameterIndex];
    }

    /// <summary>
    /// パラメータの最大値を取得する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <returns>パラメータの最大値</returns>
    public unsafe float GetParameterMaximumValue(int parameterIndex)
    {
        return CubismCore.GetParameterMaximumValues(Model)[parameterIndex];
    }

    /// <summary>
    /// パラメータの最小値を取得する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <returns>パラメータの最小値</returns>
    public unsafe float GetParameterMinimumValue(int parameterIndex)
    {
        return CubismCore.GetParameterMinimumValues(Model)[parameterIndex];
    }

    /// <summary>
    /// パラメータのデフォルト値を取得する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <returns> パラメータのデフォルト値</returns>
    public unsafe float GetParameterDefaultValue(int parameterIndex)
    {
        return CubismCore.GetParameterDefaultValues(Model)[parameterIndex];
    }

    /// <summary>
    /// パラメータの値を取得する。
    /// </summary>
    /// <param name="parameterId">パラメータID</param>
    /// <returns>パラメータの値</returns>
    public float GetParameterValue(string parameterId)
    {
        // 高速化のためにParameterIndexを取得できる機構になっているが、外部からの設定の時は呼び出し頻度が低いため不要
        int parameterIndex = GetParameterIndex(parameterId);
        return GetParameterValue(parameterIndex);
    }

    /// <summary>
    /// パラメータの値を取得する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <returns>パラメータの値</returns>
    public unsafe float GetParameterValue(int parameterIndex)
    {
        if (_notExistParameterValues.TryGetValue(parameterIndex, out var item))
        {
            return item;
        }

        //インデックスの範囲内検知
        if (0 > parameterIndex || parameterIndex >= GetParameterCount())
        {
            throw new ArgumentException($"parameterIndex out of range");
        }

        return _parameterValues[parameterIndex];
    }

    /// <summary>
    /// パラメータの値を設定する。
    /// </summary>
    /// <param name="parameterId">パラメータID</param>
    /// <param name="value">パラメータの値</param>
    /// <param name="weight">重み</param>
    public void SetParameterValue(string parameterId, float value, float weight = 1.0f)
    {
        int index = GetParameterIndex(parameterId);
        SetParameterValue(index, value, weight);
    }

    /// <summary>
    /// パラメータの値を設定する。
    /// 5-r.5 からリピート設定がある場合は範囲内に収めず循環させる（IsRepeat / GetParameterRepeatValue）。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <param name="value">パラメータの値</param>
    /// <param name="weight">重み</param>
    public unsafe void SetParameterValue(int parameterIndex, float value, float weight = 1.0f)
    {
        if (_notExistParameterValues.TryGetValue(parameterIndex, out float value1))
        {
            _notExistParameterValues[parameterIndex] = weight == 1
                ? value : (value1 * (1 - weight)) + (value * weight);
            return;
        }

        //インデックスの範囲内検知
        if (0 > parameterIndex || parameterIndex >= GetParameterCount())
        {
            throw new ArgumentException($"parameterIndex out of range");
        }

        if (IsRepeat(parameterIndex))
        {
            value = GetParameterRepeatValue(parameterIndex, value);
        }
        else
        {
            value = GetParameterClampValue(parameterIndex, value);
        }

        _parameterValues[parameterIndex] = (weight == 1)
                                          ? value
                                          : (_parameterValues[parameterIndex] * (1 - weight)) + (value * weight);
    }

    /// <summary>
    /// パラメータにリピート設定があるか取得する。
    /// モデル全体の上書きフラグ、またはパラメータ個別の上書きフラグが立っている場合は
    /// SDK 側の設定を、そうでなければ Editor（Core）の設定を使用する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <returns>リピートするなら true</returns>
    public unsafe bool IsRepeat(int parameterIndex)
    {
        if (_notExistParameterValues.ContainsKey(parameterIndex))
        {
            return false;
        }

        //インデックスの範囲内検知
        if (0 > parameterIndex || parameterIndex >= GetParameterCount())
        {
            throw new ArgumentException($"parameterIndex out of range");
        }

        bool isRepeat;

        // パラメータリピート処理を行うか判定
        if (_isOverriddenParameterRepeat || _userParameterRepeatDataList[parameterIndex].IsOverridden)
        {
            // SDK 側で設定されたリピート情報を使用する
            isRepeat = _userParameterRepeatDataList[parameterIndex].IsParameterRepeated;
        }
        else
        {
            // Editor で設定されたリピート情報を使用する
            isRepeat = CubismCore.GetParameterRepeats(Model)[parameterIndex] != 0;
        }

        return isRepeat;
    }

    /// <summary>
    /// パラメータの値を範囲内に収まるよう循環させた結果を取得する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <param name="value">パラメータの値</param>
    /// <returns>循環させた値。パラメータが存在しない場合はそのまま返す</returns>
    public unsafe float GetParameterRepeatValue(int parameterIndex, float value)
    {
        if (_notExistParameterValues.ContainsKey(parameterIndex))
        {
            return value;
        }

        //インデックスの範囲内検知
        if (0 > parameterIndex || parameterIndex >= GetParameterCount())
        {
            throw new ArgumentException($"parameterIndex out of range");
        }

        var maxValue = CubismCore.GetParameterMaximumValues(Model)[parameterIndex];
        var minValue = CubismCore.GetParameterMinimumValues(Model)[parameterIndex];
        var valueSize = maxValue - minValue;

        if (maxValue < value)
        {
            var overValue = CubismMath.ModF(value - maxValue, valueSize);
            value = !float.IsNaN(overValue) ? minValue + overValue : maxValue;
        }
        if (value < minValue)
        {
            var overValue = CubismMath.ModF(minValue - value, valueSize);
            value = !float.IsNaN(overValue) ? maxValue - overValue : minValue;
        }

        return value;
    }

    /// <summary>
    /// パラメータの値を範囲内にクランプした結果を取得する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <param name="value">パラメータの値</param>
    /// <returns>クランプした値。パラメータが存在しない場合はそのまま返す</returns>
    public unsafe float GetParameterClampValue(int parameterIndex, float value)
    {
        if (_notExistParameterValues.ContainsKey(parameterIndex))
        {
            return value;
        }

        //インデックスの範囲内検知
        if (0 > parameterIndex || parameterIndex >= GetParameterCount())
        {
            throw new ArgumentException($"parameterIndex out of range");
        }

        var maxValue = CubismCore.GetParameterMaximumValues(Model)[parameterIndex];
        var minValue = CubismCore.GetParameterMinimumValues(Model)[parameterIndex];

        return CubismMath.ClampF(value, minValue, maxValue);
    }

    /// <summary>
    /// パラメータのリピート設定（Core の生データ）を取得する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <returns>リピートするなら true</returns>
    public unsafe bool GetParameterRepeats(int parameterIndex)
    {
        return CubismCore.GetParameterRepeats(Model)[parameterIndex] != 0;
    }

    /// <summary>
    /// パラメータの値を加算する。
    /// </summary>
    /// <param name="parameterId">パラメータID</param>
    /// <param name="value">加算する値</param>
    /// <param name="weight">重み</param>
    public void AddParameterValue(string parameterId, float value, float weight = 1.0f)
    {
        int index = GetParameterIndex(parameterId);
        AddParameterValue(index, value, weight);
    }

    /// <summary>
    /// パラメータの値を加算する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <param name="value">加算する値</param>
    /// <param name="weight">重み</param>
    public void AddParameterValue(int parameterIndex, float value, float weight = 1.0f)
    {
        if (parameterIndex == -1)
            return;
        SetParameterValue(parameterIndex, (GetParameterValue(parameterIndex) + (value * weight)));
    }

    /// <summary>
    /// パラメータの値を乗算する。
    /// </summary>
    /// <param name="parameterId">パラメータID</param>
    /// <param name="value">乗算する値</param>
    /// <param name="weight">重み</param>
    public void MultiplyParameterValue(string parameterId, float value, float weight = 1.0f)
    {
        int index = GetParameterIndex(parameterId);
        MultiplyParameterValue(index, value, weight);
    }

    /// <summary>
    /// パラメータの値を乗算する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <param name="value">乗算する値</param>
    /// <param name="weight">重み</param>
    public void MultiplyParameterValue(int parameterIndex, float value, float weight = 1.0f)
    {
        if (parameterIndex == -1)
            return;
        SetParameterValue(parameterIndex, GetParameterValue(parameterIndex) * (1.0f + (value - 1.0f) * weight));
    }

    /// <summary>
    /// Drawableのインデックスを取得する。
    /// </summary>
    /// <param name="drawableId">DrawableのID</param>
    /// <returns>Drawableのインデックス</returns>
    public int GetDrawableIndex(string drawableId)
    {
        return DrawableIds.IndexOf(drawableId);
    }

    /// <summary>
    /// Drawableの個数を取得する。
    /// </summary>
    /// <returns>Drawableの個数</returns>
    public int GetDrawableCount()
    {
        return CubismCore.GetDrawableCount(Model);
    }

    /// <summary>
    /// DrawableのIDを取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>DrawableのID</returns>
    public string GetDrawableId(int drawableIndex)
    {
        if (0 <= drawableIndex && drawableIndex < DrawableIds.Count)
        {
            throw new IndexOutOfRangeException("Out of DrawableIds size");
        }
        return DrawableIds[drawableIndex];
    }

    /// <summary>
    /// 描画順リストを取得する（官方 5-r.5 で csmGetDrawableRenderOrders → csmGetRenderOrders に改名）。
    /// </summary>
    /// <returns>描画順リスト</returns>
    /// <remarks>
    /// G1 門禁で実測済み: 返る配列の長さは drawableCount + offscreenCount（Ren で 198 + 24 = 222）。
    /// drawable だけを想定して drawableCount でループすると offscreen の描画順が壊れるため、
    /// 呼び出し側は GetDrawableCount() + GetOffscreenCount() で走査すること。
    /// </remarks>
    public unsafe int* GetRenderOrders()
    {
        return CubismCore.GetRenderOrders(Model);
    }

    /// <summary>
    /// Drawableのテクスチャインデックスを取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableのテクスチャインデックス</returns>
    public unsafe int GetDrawableTextureIndex(int drawableIndex)
    {
        var textureIndices = CubismCore.GetDrawableTextureIndices(Model);
        return textureIndices[drawableIndex];
    }

    /// <summary>
    /// Drawableの頂点インデックスの個数を取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableの頂点インデックスの個数</returns>
    public unsafe int GetDrawableVertexIndexCount(int drawableIndex)
    {
        return CubismCore.GetDrawableIndexCounts(Model)[drawableIndex];
    }

    /// <summary>
    /// Drawableの頂点の個数を取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableの頂点の個数</returns>
    public unsafe int GetDrawableVertexCount(int drawableIndex)
    {
        return CubismCore.GetDrawableVertexCounts(Model)[drawableIndex];
    }

    /// <summary>
    /// Drawableの頂点リストを取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableの頂点リスト</returns>
    public unsafe float* GetDrawableVertices(int drawableIndex)
    {
        return (float*)GetDrawableVertexPositions(drawableIndex);
    }

    /// <summary>
    /// Drawableの頂点インデックスリストを取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableの頂点インデックスリスト</returns>
    public unsafe ushort* GetDrawableVertexIndices(int drawableIndex)
    {
        return CubismCore.GetDrawableIndices(Model)[drawableIndex];
    }

    /// <summary>
    /// Drawableの頂点リストを取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableの頂点リスト</returns>
    public unsafe Vector2* GetDrawableVertexPositions(int drawableIndex)
    {
        return CubismCore.GetDrawableVertexPositions(Model)[drawableIndex];
    }

    /// <summary>
    /// Drawableの頂点のUVリストを取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableの頂点のUVリスト</returns>
    public unsafe Vector2* GetDrawableVertexUvs(int drawableIndex)
    {
        return CubismCore.GetDrawableVertexUvs(Model)[drawableIndex];
    }

    /// <summary>
    /// Drawableの不透明度を取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableの不透明度</returns>
    public unsafe float GetDrawableOpacity(int drawableIndex)
    {
        return CubismCore.GetDrawableOpacities(Model)[drawableIndex];
    }

    /// <summary>
    /// Drawableの乗算色を取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableの乗算色</returns>
    public unsafe Vector4 GetDrawableMultiplyColor(int drawableIndex)
    {
        return CubismCore.GetDrawableMultiplyColors(Model)[drawableIndex];
    }

    /// <summary>
    /// Drawableのスクリーン色を取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableのスクリーン色</returns>
    public unsafe Vector4 GetDrawableScreenColor(int drawableIndex)
    {
        return CubismCore.GetDrawableScreenColors(Model)[drawableIndex];
    }

    /// <summary>
    /// Drawableの親パーツのインデックスを取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>drawableの親パーツのインデックス</returns>
    public unsafe int GetDrawableParentPartIndex(int drawableIndex)
    {
        return CubismCore.GetDrawableParentPartIndices(Model)[drawableIndex];
    }

    /// <summary>
    /// Drawableのブレンドモードを取得する（官方 5-r.5 で GetDrawableBlendMode → GetDrawableBlendModeType に改名）。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableのブレンドモード</returns>
    public unsafe CsmBlendMode GetDrawableBlendModeType(int drawableIndex)
    {
        var blendMode = new CsmBlendMode();

        // 5.2以前のモデルでも使用することが可能
        var blendModes = CubismCore.GetDrawableBlendModes(Model);
        blendMode.SetBlendMode(blendModes[drawableIndex]);

        return blendMode;
    }

    /// <summary>
    /// Drawableのマスク使用時の反転設定を取得する。
    /// マスクを使用しない場合は無視される
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableのマスクの反転設定</returns>
    public unsafe bool GetDrawableInvertedMask(int drawableIndex)
    {
        var constantFlags = CubismCore.GetDrawableConstantFlags(Model)[drawableIndex];
        return IsBitSet(constantFlags, CsmEnum.CsmIsInvertedMask);
    }

    /// <summary>
    /// Drawableの表示情報を取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>true    Drawableが表示
    /// false   Drawableが非表示</returns>
    public unsafe bool GetDrawableDynamicFlagIsVisible(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmIsVisible);
    }

    /// <summary>
    /// 直近のCubismModel::Update関数でDrawableの表示状態が変化したかを取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>true    Drawableの表示状態が直近のCubismModel::Update関数で変化した
    /// false   Drawableの表示状態が直近のCubismModel::Update関数で変化していない</returns>
    public unsafe bool GetDrawableDynamicFlagVisibilityDidChange(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmVisibilityDidChange);
    }

    /// <summary>
    /// 直近のCubismModel::Update関数でDrawableの不透明度が変化したかを取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>true    Drawableの不透明度が直近のCubismModel::Update関数で変化した
    /// false   Drawableの不透明度が直近のCubismModel::Update関数で変化していない</returns>
    public unsafe bool GetDrawableDynamicFlagOpacityDidChange(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmOpacityDidChange);
    }

    /// <summary>
    /// 直近のCubismModel::Update関数でDrawableのDrawOrderが変化したかを取得する。
    /// DrawOrderはArtMesh上で指定する0から1000の情報
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>true    Drawableの不透明度が直近のCubismModel::Update関数で変化した
    /// false   Drawableの不透明度が直近のCubismModel::Update関数で変化していない</returns>
    public unsafe bool GetDrawableDynamicFlagDrawOrderDidChange(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmDrawOrderDidChange);
    }

    /// <summary>
    /// 直近のCubismModel::Update関数でDrawableの描画の順序が変化したかを取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>true    Drawableの描画の順序が直近のCubismModel::Update関数で変化した
    /// false   Drawableの描画の順序が直近のCubismModel::Update関数で変化していない</returns>
    public unsafe bool GetDrawableDynamicFlagRenderOrderDidChange(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmRenderOrderDidChange);
    }

    /// <summary>
    /// 直近のCubismModel::Update関数でDrawableの頂点情報が変化したかを取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>true    Drawableの頂点情報が直近のCubismModel::Update関数で変化した
    /// false   Drawableの頂点情報が直近のCubismModel::Update関数で変化していない</returns>
    public unsafe bool GetDrawableDynamicFlagVertexPositionsDidChange(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmVertexPositionsDidChange);
    }

    /// <summary>
    /// 直近のCubismModel::Update関数でDrawableの乗算色・スクリーン色が変化したかを取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>true    Drawableの乗算色・スクリーン色が直近のCubismModel::Update関数で変化した
    /// false   Drawableの乗算色・スクリーン色が直近のCubismModel::Update関数で変化していない</returns>
    public unsafe bool GetDrawableDynamicFlagBlendColorDidChange(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmBlendColorDidChange);
    }

    /// <summary>
    /// Drawableのクリッピングマスクリストを取得する。
    /// </summary>
    /// <returns>Drawableのクリッピングマスクリスト</returns>
    public unsafe int** GetDrawableMasks()
    {
        return CubismCore.GetDrawableMasks(Model);
    }

    /// <summary>
    /// Drawableのクリッピングマスクの個数リストを取得する。
    /// </summary>
    /// <returns>Drawableのクリッピングマスクの個数リスト</returns>
    public unsafe int* GetDrawableMaskCounts()
    {
        return CubismCore.GetDrawableMaskCounts(Model);
    }

    //========================================================
    //  Offscreen Functions.
    //========================================================

    /// <summary>
    /// Offscreen のブレンドモードを取得する（官方 5-r.5 の新規 API）。
    /// </summary>
    /// <param name="offscreenIndex">Offscreen のインデックス</param>
    /// <returns>Offscreen のブレンドモード</returns>
    public unsafe CsmBlendMode GetOffscreenBlendModeType(int offscreenIndex)
    {
        var blendMode = new CsmBlendMode();

        var blendModes = CubismCore.GetOffscreenBlendModes(Model);
        if (blendModes != null)
        {
            blendMode.SetBlendMode(blendModes[offscreenIndex]);
        }

        return blendMode;
    }

    /// <summary>
    /// Offscreen の個数を取得する。
    /// </summary>
    /// <returns>Offscreen の個数</returns>
    public int GetOffscreenCount()
    {
        return CubismCore.GetOffscreenCount(Model);
    }

    /// <summary>
    /// Offscreen のクリッピングマスクリストを取得する。
    /// </summary>
    /// <returns>Offscreen のクリッピングマスクリスト</returns>
    public unsafe int** GetOffscreenMasks()
    {
        return CubismCore.GetOffscreenMasks(Model);
    }

    /// <summary>
    /// Offscreen のクリッピングマスクの個数リストを取得する。
    /// </summary>
    /// <returns>Offscreen のクリッピングマスクの個数リスト</returns>
    public unsafe int* GetOffscreenMaskCounts()
    {
        return CubismCore.GetOffscreenMaskCounts(Model);
    }

    /// <summary>
    /// Offscreen のオーナー（パーツ）インデックスリストを取得する。
    /// </summary>
    /// <returns>Offscreen のオーナーインデックスリスト</returns>
    public unsafe int* GetOffscreenOwnerIndices()
    {
        return CubismCore.GetOffscreenOwnerIndices(Model);
    }

    /// <summary>
    /// Offscreen のオーナー（パーツ）ID を取得する。
    /// オーナーインデックスはパーツのインデックスであることに注意（Drawable ではない）。
    /// </summary>
    /// <param name="offscreenIndex">Offscreen のインデックス</param>
    /// <returns>オーナー（パーツ）の ID</returns>
    public unsafe string GetOffscreenOwnerId(int offscreenIndex)
    {
        var ownerIndex = GetOffscreenOwnerIndices()[offscreenIndex];
        var partIds = CubismCore.GetPartIds(Model);

        return CubismFramework.CubismIdManager.GetId(new string(partIds[ownerIndex]));
    }

    /// <summary>
    /// Offscreen の乗算色を取得する。
    /// </summary>
    /// <param name="offscreenIndex">Offscreen のインデックス</param>
    /// <returns>Offscreen の乗算色</returns>
    public unsafe Vector4 GetOffscreenMultiplyColor(int offscreenIndex)
    {
        return CubismCore.GetOffscreenMultiplyColors(Model)[offscreenIndex];
    }

    /// <summary>
    /// Offscreen のスクリーン色を取得する。
    /// </summary>
    /// <param name="offscreenIndex">Offscreen のインデックス</param>
    /// <returns>Offscreen のスクリーン色</returns>
    public unsafe Vector4 GetOffscreenScreenColor(int offscreenIndex)
    {
        return CubismCore.GetOffscreenScreenColors(Model)[offscreenIndex];
    }

    /// <summary>
    /// Offscreen のマスク使用時の反転設定を取得する。
    /// </summary>
    /// <param name="offscreenIndex">Offscreen のインデックス</param>
    /// <returns>Offscreen のマスクの反転設定</returns>
    public unsafe bool GetOffscreenInvertedMask(int offscreenIndex)
    {
        var constantFlags = CubismCore.GetOffscreenConstantFlags(Model)[offscreenIndex];
        return IsBitSet(constantFlags, CsmEnum.CsmIsInvertedMask);
    }

    /// <summary>
    /// Offscreen の不透明度を取得する。
    /// </summary>
    /// <param name="offscreenIndex">Offscreen のインデックス</param>
    /// <returns>Offscreen の不透明度</returns>
    public unsafe float GetOffscreenOpacity(int offscreenIndex)
    {
        if (offscreenIndex < 0 || offscreenIndex >= CubismCore.GetOffscreenCount(Model))
        {
            return 1.0f; // Offscreen が無いのでスキップ
        }

        return CubismCore.GetOffscreenOpacities(Model)[offscreenIndex];
    }

    /// <summary>
    /// クリッピングマスクを使用しているかどうか？
    /// </summary>
    /// <returns>true    クリッピングマスクを使用している
    /// false   クリッピングマスクを使用していない</returns>
    public unsafe bool IsUsingMasking()
    {
        for (int d = 0; d < CubismCore.GetDrawableCount(Model); ++d)
        {
            if (CubismCore.GetDrawableMaskCounts(Model)[d] <= 0)
            {
                continue;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Offscreen がクリッピングマスクを使用しているかどうか？
    /// </summary>
    /// <returns>true    Offscreen がクリッピングマスクを使用している
    /// false   Offscreen がクリッピングマスクを使用していない</returns>
    public unsafe bool IsUsingMaskingForOffscreen()
    {
        for (int d = 0; d < CubismCore.GetOffscreenCount(Model); ++d)
        {
            if (CubismCore.GetOffscreenMaskCounts(Model)[d] <= 0)
            {
                continue;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// 保存されたパラメータを読み込む
    /// </summary>
    public unsafe void LoadParameters()
    {
        int parameterCount = CubismCore.GetParameterCount(Model);
        int savedParameterCount = _savedParameters.Count;

        if (parameterCount > savedParameterCount)
        {
            parameterCount = savedParameterCount;
        }

        for (int i = 0; i < parameterCount; ++i)
        {
            _parameterValues[i] = _savedParameters[i];
        }
    }

    /// <summary>
    /// パラメータを保存する。
    /// </summary>
    public unsafe void SaveParameters()
    {
        int parameterCount = CubismCore.GetParameterCount(Model);
        int savedParameterCount = _savedParameters.Count;

        if (savedParameterCount != parameterCount)
        {
            _savedParameters.Clear();
            for (int i = 0; i < parameterCount; ++i)
            {
                _savedParameters.Add(_parameterValues[i]);
            }
        }
        else
        {
            for (int i = 0; i < parameterCount; ++i)
            {
                _savedParameters[i] = _parameterValues[i];
            }
        }
    }

    /// <summary>
    /// パラメータのリピート設定をモデル全体で上書きするか取得する。
    /// </summary>
    /// <returns>true の場合、モデル全体でパラメータリピートを行う</returns>
    public bool GetOverrideFlagForModelParameterRepeat()
    {
        return _isOverriddenParameterRepeat;
    }

    /// <summary>
    /// パラメータのリピート設定をモデル全体で上書きするか設定する。
    /// </summary>
    /// <param name="isRepeat">true の場合、モデル全体でパラメータリピートを行う</param>
    public void SetOverrideFlagForModelParameterRepeat(bool isRepeat)
    {
        _isOverriddenParameterRepeat = isRepeat;
    }

    /// <summary>
    /// パラメータ個別のリピート設定の上書きフラグを取得する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <returns>上書きするなら true</returns>
    public bool GetOverrideFlagForParameterRepeat(int parameterIndex)
    {
        return _userParameterRepeatDataList[parameterIndex].IsOverridden;
    }

    /// <summary>
    /// パラメータ個別のリピート設定の上書きフラグを設定する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <param name="value">上書きするなら true</param>
    public void SetOverrideFlagForParameterRepeat(int parameterIndex, bool value)
    {
        _userParameterRepeatDataList[parameterIndex].IsOverridden = value;
    }

    /// <summary>
    /// パラメータ個別のリピートフラグを取得する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <returns>リピートするなら true</returns>
    public bool GetRepeatFlagForParameterRepeat(int parameterIndex)
    {
        return _userParameterRepeatDataList[parameterIndex].IsParameterRepeated;
    }

    /// <summary>
    /// パラメータ個別のリピートフラグを設定する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのインデックス</param>
    /// <param name="value">リピートするなら true</param>
    public void SetRepeatFlagForParameterRepeat(int parameterIndex, bool value)
    {
        _userParameterRepeatDataList[parameterIndex].IsParameterRepeated = value;
    }

    /// <summary>
    /// 乗算色・スクリーン色の設定を取得する（官方 GetOverrideMultiplyAndScreenColor）。
    /// </summary>
    /// <returns>乗算色・スクリーン色の設定</returns>
    public CubismModelMultiplyAndScreenColor GetOverrideMultiplyAndScreenColor()
    {
        return _overrideMultiplyAndScreenColors;
    }

    //========================================================
    //  Culling Functions.
    //========================================================

    /// <summary>
    /// Drawableのカリング情報を取得する。
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <returns>Drawableのカリング情報（0: カリングしない / 1: カリングする）</returns>
    public unsafe int GetDrawableCulling(int drawableIndex)
    {
        if (GetOverrideFlagForModelCullings() || GetOverrideFlagForDrawableCullings(drawableIndex))
        {
            return _userDrawableCullings[drawableIndex].IsCulling;
        }

        var constantFlags = CubismCore.GetDrawableConstantFlags(Model);
        return IsBitSet(constantFlags[drawableIndex], CsmEnum.CsmIsDoubleSided) ? 0 : 1;
    }

    /// <summary>
    /// Drawableのカリング情報を設定する
    /// </summary>
    /// <param name="drawableIndex">Drawableのインデックス</param>
    /// <param name="isCulling">カリングするなら 1、しないなら 0</param>
    public void SetDrawableCulling(int drawableIndex, int isCulling)
    {
        _userDrawableCullings[drawableIndex].IsCulling = isCulling;
    }

    /// <summary>
    /// Offscreenのカリング情報を取得する。
    /// </summary>
    /// <param name="offscreenIndex">Offscreenのインデックス</param>
    /// <returns>Offscreenのカリング情報（0: カリングしない / 1: カリングする）</returns>
    public unsafe int GetOffscreenCulling(int offscreenIndex)
    {
        if (GetOverrideFlagForModelCullings() || GetOverrideFlagForOffscreenCullings(offscreenIndex))
        {
            return _userOffscreenCullings[offscreenIndex].IsCulling;
        }

        var constantFlags = CubismCore.GetOffscreenConstantFlags(Model);
        return IsBitSet(constantFlags[offscreenIndex], CsmEnum.CsmIsDoubleSided) ? 0 : 1;
    }

    /// <summary>
    /// Offscreenのカリング情報を設定する
    /// </summary>
    /// <param name="offscreenIndex">Offscreenのインデックス</param>
    /// <param name="isCulling">カリングするなら 1、しないなら 0</param>
    public void SetOffscreenCulling(int offscreenIndex, int isCulling)
    {
        _userOffscreenCullings[offscreenIndex].IsCulling = isCulling;
    }

    /// <summary>
    /// SDKからモデル全体のカリング設定を上書きするか。
    /// </summary>
    /// <returns>true    ->  SDK上のカリング設定を使用
    /// false   ->  モデルのカリング設定を使用</returns>
    public bool GetOverrideFlagForModelCullings()
    {
        return _isOverriddenCullings;
    }

    /// <summary>
    /// SDKからモデル全体のカリング設定を上書きするかをセットする
    /// SDK上のカリング設定を使うならtrue、モデルのカリング設定を使うならfalse
    /// </summary>
    public void SetOverrideFlagForModelCullings(bool value)
    {
        _isOverriddenCullings = value;
    }

    /// <summary>
    /// SDKからdrawableのカリング設定を上書きするか。
    /// </summary>
    /// <returns>true    ->  SDK上のカリング設定を使用
    /// false   ->  モデルのカリング設定を使用</returns>
    public bool GetOverrideFlagForDrawableCullings(int drawableIndex)
    {
        return _userDrawableCullings[drawableIndex].IsOverridden;
    }

    /// <summary>
    /// SDKからdrawableのカリング設定を上書きするかをセットする
    /// SDK上のカリング設定を使うならtrue、モデルのカリング設定を使うならfalse
    /// </summary>
    public void SetOverrideFlagForDrawableCullings(int drawableIndex, bool value)
    {
        _userDrawableCullings[drawableIndex].IsOverridden = value;
    }

    /// <summary>
    /// SDKからoffscreenのカリング設定を上書きするか。
    /// </summary>
    /// <returns>true    ->  SDK上のカリング設定を使用
    /// false   ->  モデルのカリング設定を使用</returns>
    public bool GetOverrideFlagForOffscreenCullings(int offscreenIndex)
    {
        return _userOffscreenCullings[offscreenIndex].IsOverridden;
    }

    /// <summary>
    /// SDKからoffscreenのカリング設定を上書きするかをセットする
    /// SDK上のカリング設定を使うならtrue、モデルのカリング設定を使うならfalse
    /// </summary>
    public void SetOverrideFlagForOffscreenCullings(int offscreenIndex, bool value)
    {
        _userOffscreenCullings[offscreenIndex].IsOverridden = value;
    }

    /// <summary>
    /// ブレンドモードを使用して描画する必要があるか取得する。
    /// </summary>
    /// <returns>true の場合、ブレンドモードを使用する</returns>
    public bool IsBlendModeEnabled()
    {
        return _isBlendModeEnabled;
    }

    /// <summary>
    /// モデルの不透明度を取得する
    /// </summary>
    /// <returns>不透明度の値</returns>
    public float GetModelOpacity()
    {
        return _modelOpacity;
    }

    /// <summary>
    /// モデルの不透明度を設定する
    /// </summary>
    /// <param name="value">不透明度の値</param>
    public void SetModelOpacity(float value)
    {
        _modelOpacity = value;
    }

    /// <summary>
    /// パラメータのIDを取得する。
    /// </summary>
    /// <param name="parameterIndex">パラメータのIndex</param>
    /// <returns>パラメータのID</returns>
    public string GetParameterId(int parameterIndex)
    {
        if (0 <= parameterIndex && parameterIndex < ParameterIds.Count)
        {
            throw new IndexOutOfRangeException("Out of ParameterIds size");
        }
        return ParameterIds[parameterIndex];
    }

    private static bool IsBitSet(byte data, byte mask)
    {
        return (data & mask) == mask;
    }
}
