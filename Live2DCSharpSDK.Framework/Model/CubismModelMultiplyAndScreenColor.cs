using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// モデルの乗算色・スクリーン色を扱うクラス。
/// 官方 Framework/src/Model/CubismModelMultiplyAndScreenColor.{hpp,cpp} の移植。
/// 5-r.5 で CubismModel から分離された（CHANGELOG: Change multiply and screen color functions to separate class with renamed methods）。
/// </summary>
public class CubismModelMultiplyAndScreenColor
{
    /// <summary>
    /// 描画オブジェクトの色情報（官方 ColorData）。
    /// 旧 DrawableColorData / PartColorData の代替（官方は本クラスの ColorData に統合）。
    /// </summary>
    public struct ColorData
    {
        /// <summary>
        /// SDK 側の色で上書きするか
        /// </summary>
        public bool IsOverridden;
        /// <summary>
        /// 色
        /// </summary>
        public CubismTextureColor Color;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public ColorData(bool isOverridden, CubismTextureColor color)
        {
            IsOverridden = isOverridden;
            Color = color;
        }
    }

    /// <summary>
    /// モデル（パーツ階層と色数取得に使用）
    /// </summary>
    private readonly CubismModel _model;
    /// <summary>
    /// パーツ階層（CubismModel が保持する List の参照。再代入しないこと：PLAN U04）
    /// </summary>
    private readonly List<CubismModelPartInfo> _partsHierarchy;
    /// <summary>
    /// 乗算色を全て上書きするか？
    /// </summary>
    private bool _isOverriddenModelMultiplyColors;
    /// <summary>
    /// スクリーン色を全て上書きするか？
    /// </summary>
    private bool _isOverriddenModelScreenColors;

    private ColorData[] _userPartScreenColors = [];
    private ColorData[] _userPartMultiplyColors = [];
    private ColorData[] _userDrawableScreenColors = [];
    private ColorData[] _userDrawableMultiplyColors = [];
    private ColorData[] _userOffscreenScreenColors = [];
    private ColorData[] _userOffscreenMultiplyColors = [];

    /// <summary>
    /// コンストラクタ。
    /// 官方は CubismModel の初期化子リストで this と _partsHierarchy のアドレスを渡す（PLAN U04）。
    /// C# 側も同じくコンストラクタでモデルと階層リストの参照を受け取る（参照は後から埋まる）。
    /// </summary>
    /// <param name="model">Cubism モデル</param>
    /// <param name="partsHierarchy">パーツ階層（CubismModel が保持する List そのもの）</param>
    public CubismModelMultiplyAndScreenColor(CubismModel model, List<CubismModelPartInfo> partsHierarchy)
    {
        _model = model;
        _partsHierarchy = partsHierarchy;
        _isOverriddenModelMultiplyColors = false;
        _isOverriddenModelScreenColors = false;
    }

    /// <summary>
    /// 乗算色・スクリーン色を使うための初期化。
    /// </summary>
    /// <param name="partCount">パーツの個数</param>
    /// <param name="drawableCount">Drawable の個数</param>
    /// <param name="offscreenCount">Offscreen の個数</param>
    public void Initialize(int partCount, int drawableCount, int offscreenCount)
    {
        // 乗算色
        var userMultiplyColor = new ColorData(false, new CubismTextureColor(1.0f, 1.0f, 1.0f, 1.0f));

        // スクリーン色
        var userScreenColor = new ColorData(false, new CubismTextureColor(0.0f, 0.0f, 0.0f, 1.0f));

        // Part
        _userPartMultiplyColors = new ColorData[partCount];
        _userPartScreenColors = new ColorData[partCount];
        for (int i = 0; i < partCount; ++i)
        {
            _userPartMultiplyColors[i] = userMultiplyColor;
            _userPartScreenColors[i] = userScreenColor;
        }

        // Drawable
        _userDrawableMultiplyColors = new ColorData[drawableCount];
        _userDrawableScreenColors = new ColorData[drawableCount];
        for (int i = 0; i < drawableCount; ++i)
        {
            _userDrawableMultiplyColors[i] = userMultiplyColor;
            _userDrawableScreenColors[i] = userScreenColor;
        }

        // Offscreen
        _userOffscreenMultiplyColors = new ColorData[offscreenCount];
        _userOffscreenScreenColors = new ColorData[offscreenCount];
        for (int i = 0; i < offscreenCount; ++i)
        {
            _userOffscreenMultiplyColors[i] = userMultiplyColor;
            _userOffscreenScreenColors[i] = userScreenColor;
        }
    }

    /// <summary>
    /// モデル全体の乗算色として実行時に設定した色を使用するか設定する。
    /// </summary>
    /// <param name="value">true の場合、実行時に設定した色を使用する</param>
    public void SetMultiplyColorEnabled(bool value)
    {
        _isOverriddenModelMultiplyColors = value;
    }

    /// <summary>
    /// モデル全体の乗算色として実行時に設定した色を使用するか取得する。
    /// </summary>
    public bool GetMultiplyColorEnabled()
    {
        return _isOverriddenModelMultiplyColors;
    }

    /// <summary>
    /// モデル全体のスクリーン色として実行時に設定した色を使用するか設定する。
    /// </summary>
    /// <param name="value">true の場合、実行時に設定した色を使用する</param>
    public void SetScreenColorEnabled(bool value)
    {
        _isOverriddenModelScreenColors = value;
    }

    /// <summary>
    /// モデル全体のスクリーン色として実行時に設定した色を使用するか取得する。
    /// </summary>
    public bool GetScreenColorEnabled()
    {
        return _isOverriddenModelScreenColors;
    }

    /// <summary>
    /// パーツの乗算色を SDK 側の色で上書きするか設定する。
    /// </summary>
    public void SetPartMultiplyColorEnabled(int partIndex, bool value)
    {
        if (partIndex < 0 || _model.GetPartCount() <= partIndex)
        {
            WarnIndexOutOfRange(nameof(SetPartMultiplyColorEnabled), partIndex, _model.GetPartCount() - 1);
            return;
        }

        SetPartColorEnabled(partIndex, value, _userPartMultiplyColors, _userDrawableMultiplyColors, _userOffscreenMultiplyColors);
    }

    /// <summary>
    /// パーツの乗算色を SDK 側の色で上書きするか取得する。
    /// </summary>
    public bool GetPartMultiplyColorEnabled(int partIndex)
    {
        if (partIndex < 0 || _model.GetPartCount() <= partIndex)
        {
            WarnIndexOutOfRange(nameof(GetPartMultiplyColorEnabled), partIndex, _model.GetPartCount() - 1);
            return false;
        }

        return _userPartMultiplyColors[partIndex].IsOverridden;
    }

    /// <summary>
    /// パーツのスクリーン色を SDK 側の色で上書きするか設定する。
    /// </summary>
    public void SetPartScreenColorEnabled(int partIndex, bool value)
    {
        if (partIndex < 0 || _model.GetPartCount() <= partIndex)
        {
            WarnIndexOutOfRange(nameof(SetPartScreenColorEnabled), partIndex, _model.GetPartCount() - 1);
            return;
        }

        SetPartColorEnabled(partIndex, value, _userPartScreenColors, _userDrawableScreenColors, _userOffscreenScreenColors);
    }

    /// <summary>
    /// パーツのスクリーン色を SDK 側の色で上書きするか取得する。
    /// </summary>
    public bool GetPartScreenColorEnabled(int partIndex)
    {
        if (partIndex < 0 || _model.GetPartCount() <= partIndex)
        {
            WarnIndexOutOfRange(nameof(GetPartScreenColorEnabled), partIndex, _model.GetPartCount() - 1);
            return false;
        }

        return _userPartScreenColors[partIndex].IsOverridden;
    }

    /// <summary>
    /// パーツの乗算色を設定する。
    /// </summary>
    public void SetPartMultiplyColor(int partIndex, CubismTextureColor color)
    {
        SetPartMultiplyColor(partIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// パーツの乗算色を設定する。
    /// </summary>
    public void SetPartMultiplyColor(int partIndex, float r, float g, float b, float a = 1.0f)
    {
        if (partIndex < 0 || _model.GetPartCount() <= partIndex)
        {
            WarnIndexOutOfRange(nameof(SetPartMultiplyColor), partIndex, _model.GetPartCount() - 1);
            return;
        }

        SetPartColor(partIndex, r, g, b, a, _userPartMultiplyColors, _userDrawableMultiplyColors, _userOffscreenMultiplyColors);
    }

    /// <summary>
    /// パーツの乗算色を取得する。
    /// </summary>
    public CubismTextureColor GetPartMultiplyColor(int partIndex)
    {
        if (partIndex < 0 || _model.GetPartCount() <= partIndex)
        {
            WarnIndexOutOfRange(nameof(GetPartMultiplyColor), partIndex, _model.GetPartCount() - 1);
            return new CubismTextureColor(1.0f, 1.0f, 1.0f, 1.0f);
        }

        return _userPartMultiplyColors[partIndex].Color;
    }

    /// <summary>
    /// パーツのスクリーン色を設定する。
    /// </summary>
    public void SetPartScreenColor(int partIndex, CubismTextureColor color)
    {
        SetPartScreenColor(partIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// パーツのスクリーン色を設定する。
    /// </summary>
    public void SetPartScreenColor(int partIndex, float r, float g, float b, float a = 1.0f)
    {
        if (partIndex < 0 || _model.GetPartCount() <= partIndex)
        {
            WarnIndexOutOfRange(nameof(SetPartScreenColor), partIndex, _model.GetPartCount() - 1);
            return;
        }

        SetPartColor(partIndex, r, g, b, a, _userPartScreenColors, _userDrawableScreenColors, _userOffscreenScreenColors);
    }

    /// <summary>
    /// パーツのスクリーン色を取得する。
    /// </summary>
    public CubismTextureColor GetPartScreenColor(int partIndex)
    {
        if (partIndex < 0 || _model.GetPartCount() <= partIndex)
        {
            WarnIndexOutOfRange(nameof(GetPartScreenColor), partIndex, _model.GetPartCount() - 1);
            return new CubismTextureColor(0.0f, 0.0f, 0.0f, 1.0f);
        }

        return _userPartScreenColors[partIndex].Color;
    }

    /// <summary>
    /// Drawable の乗算色として実行時に設定した色を使用するか設定する。
    /// </summary>
    public void SetDrawableMultiplyColorEnabled(int drawableIndex, bool value)
    {
        if (drawableIndex < 0 || _model.GetDrawableCount() <= drawableIndex)
        {
            WarnIndexOutOfRange(nameof(SetDrawableMultiplyColorEnabled), drawableIndex, _model.GetDrawableCount() - 1);
            return;
        }

        _userDrawableMultiplyColors[drawableIndex].IsOverridden = value;
    }

    /// <summary>
    /// Drawable の乗算色として実行時に設定した色を使用するか取得する。
    /// </summary>
    public bool GetDrawableMultiplyColorEnabled(int drawableIndex)
    {
        if (drawableIndex < 0 || _model.GetDrawableCount() <= drawableIndex)
        {
            WarnIndexOutOfRange(nameof(GetDrawableMultiplyColorEnabled), drawableIndex, _model.GetDrawableCount() - 1);
            return false;
        }

        return _userDrawableMultiplyColors[drawableIndex].IsOverridden;
    }

    /// <summary>
    /// Drawable のスクリーン色として実行時に設定した色を使用するか設定する。
    /// </summary>
    public void SetDrawableScreenColorEnabled(int drawableIndex, bool value)
    {
        if (drawableIndex < 0 || _model.GetDrawableCount() <= drawableIndex)
        {
            WarnIndexOutOfRange(nameof(SetDrawableScreenColorEnabled), drawableIndex, _model.GetDrawableCount() - 1);
            return;
        }

        _userDrawableScreenColors[drawableIndex].IsOverridden = value;
    }

    /// <summary>
    /// Drawable のスクリーン色として実行時に設定した色を使用するか取得する。
    /// </summary>
    public bool GetDrawableScreenColorEnabled(int drawableIndex)
    {
        if (drawableIndex < 0 || _model.GetDrawableCount() <= drawableIndex)
        {
            WarnIndexOutOfRange(nameof(GetDrawableScreenColorEnabled), drawableIndex, _model.GetDrawableCount() - 1);
            return false;
        }

        return _userDrawableScreenColors[drawableIndex].IsOverridden;
    }

    /// <summary>
    /// Drawable の乗算色を設定する。
    /// </summary>
    public void SetDrawableMultiplyColor(int drawableIndex, CubismTextureColor color)
    {
        SetDrawableMultiplyColor(drawableIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// Drawable の乗算色を設定する。
    /// </summary>
    public void SetDrawableMultiplyColor(int drawableIndex, float r, float g, float b, float a = 1.0f)
    {
        if (drawableIndex < 0 || _model.GetDrawableCount() <= drawableIndex)
        {
            WarnIndexOutOfRange(nameof(SetDrawableMultiplyColor), drawableIndex, _model.GetDrawableCount() - 1);
            return;
        }

        _userDrawableMultiplyColors[drawableIndex].Color.R = r;
        _userDrawableMultiplyColors[drawableIndex].Color.G = g;
        _userDrawableMultiplyColors[drawableIndex].Color.B = b;
        _userDrawableMultiplyColors[drawableIndex].Color.A = a;
    }

    /// <summary>
    /// Drawable の乗算色を取得する。
    /// SDK 側で上書きしていない場合はモデル（Core）の色を返す。
    /// </summary>
    public CubismTextureColor GetDrawableMultiplyColor(int drawableIndex)
    {
        if (drawableIndex < 0 || _model.GetDrawableCount() <= drawableIndex)
        {
            WarnIndexOutOfRange(nameof(GetDrawableMultiplyColor), drawableIndex, _model.GetDrawableCount() - 1);
            return new CubismTextureColor(1.0f, 1.0f, 1.0f, 1.0f);
        }

        if (GetMultiplyColorEnabled() || GetDrawableMultiplyColorEnabled(drawableIndex))
        {
            return _userDrawableMultiplyColors[drawableIndex].Color;
        }

        var tmpColor = _model.GetDrawableMultiplyColor(drawableIndex);

        return new CubismTextureColor(tmpColor.X, tmpColor.Y, tmpColor.Z, tmpColor.W);
    }

    /// <summary>
    /// Drawable のスクリーン色を設定する。
    /// </summary>
    public void SetDrawableScreenColor(int drawableIndex, CubismTextureColor color)
    {
        SetDrawableScreenColor(drawableIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// Drawable のスクリーン色を設定する。
    /// </summary>
    public void SetDrawableScreenColor(int drawableIndex, float r, float g, float b, float a = 1.0f)
    {
        if (drawableIndex < 0 || _model.GetDrawableCount() <= drawableIndex)
        {
            WarnIndexOutOfRange(nameof(SetDrawableScreenColor), drawableIndex, _model.GetDrawableCount() - 1);
            return;
        }

        _userDrawableScreenColors[drawableIndex].Color.R = r;
        _userDrawableScreenColors[drawableIndex].Color.G = g;
        _userDrawableScreenColors[drawableIndex].Color.B = b;
        _userDrawableScreenColors[drawableIndex].Color.A = a;
    }

    /// <summary>
    /// Drawable のスクリーン色を取得する。
    /// SDK 側で上書きしていない場合はモデル（Core）の色を返す。
    /// </summary>
    public CubismTextureColor GetDrawableScreenColor(int drawableIndex)
    {
        if (drawableIndex < 0 || _model.GetDrawableCount() <= drawableIndex)
        {
            WarnIndexOutOfRange(nameof(GetDrawableScreenColor), drawableIndex, _model.GetDrawableCount() - 1);
            return new CubismTextureColor(0.0f, 0.0f, 0.0f, 1.0f);
        }

        if (GetScreenColorEnabled() || GetDrawableScreenColorEnabled(drawableIndex))
        {
            return _userDrawableScreenColors[drawableIndex].Color;
        }

        var tmpColor = _model.GetDrawableScreenColor(drawableIndex);

        return new CubismTextureColor(tmpColor.X, tmpColor.Y, tmpColor.Z, tmpColor.W);
    }

    /// <summary>
    /// Offscreen の乗算色を SDK 側の色で上書きするか設定する。
    /// </summary>
    public void SetOffscreenMultiplyColorEnabled(int offscreenIndex, bool value)
    {
        if (offscreenIndex < 0 || _model.GetOffscreenCount() <= offscreenIndex)
        {
            WarnIndexOutOfRange(nameof(SetOffscreenMultiplyColorEnabled), offscreenIndex, _model.GetOffscreenCount() - 1);
            return;
        }

        _userOffscreenMultiplyColors[offscreenIndex].IsOverridden = value;
    }

    /// <summary>
    /// Offscreen の乗算色を SDK 側の色で上書きするか取得する。
    /// </summary>
    public bool GetOffscreenMultiplyColorEnabled(int offscreenIndex)
    {
        if (offscreenIndex < 0 || _model.GetOffscreenCount() <= offscreenIndex)
        {
            WarnIndexOutOfRange(nameof(GetOffscreenMultiplyColorEnabled), offscreenIndex, _model.GetOffscreenCount() - 1);
            return false;
        }

        return _userOffscreenMultiplyColors[offscreenIndex].IsOverridden;
    }

    /// <summary>
    /// Offscreen のスクリーン色を SDK 側の色で上書きするか設定する。
    /// </summary>
    public void SetOffscreenScreenColorEnabled(int offscreenIndex, bool value)
    {
        if (offscreenIndex < 0 || _model.GetOffscreenCount() <= offscreenIndex)
        {
            WarnIndexOutOfRange(nameof(SetOffscreenScreenColorEnabled), offscreenIndex, _model.GetOffscreenCount() - 1);
            return;
        }

        _userOffscreenScreenColors[offscreenIndex].IsOverridden = value;
    }

    /// <summary>
    /// Offscreen のスクリーン色を SDK 側の色で上書きするか取得する。
    /// </summary>
    public bool GetOffscreenScreenColorEnabled(int offscreenIndex)
    {
        if (offscreenIndex < 0 || _model.GetOffscreenCount() <= offscreenIndex)
        {
            WarnIndexOutOfRange(nameof(GetOffscreenScreenColorEnabled), offscreenIndex, _model.GetOffscreenCount() - 1);
            return false;
        }

        return _userOffscreenScreenColors[offscreenIndex].IsOverridden;
    }

    /// <summary>
    /// Offscreen の乗算色を設定する。
    /// </summary>
    public void SetOffscreenMultiplyColor(int offscreenIndex, CubismTextureColor color)
    {
        SetOffscreenMultiplyColor(offscreenIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// Offscreen の乗算色を設定する。
    /// </summary>
    public void SetOffscreenMultiplyColor(int offscreenIndex, float r, float g, float b, float a = 1.0f)
    {
        if (offscreenIndex < 0 || _model.GetOffscreenCount() <= offscreenIndex)
        {
            WarnIndexOutOfRange(nameof(SetOffscreenMultiplyColor), offscreenIndex, _model.GetOffscreenCount() - 1);
            return;
        }

        _userOffscreenMultiplyColors[offscreenIndex].Color.R = r;
        _userOffscreenMultiplyColors[offscreenIndex].Color.G = g;
        _userOffscreenMultiplyColors[offscreenIndex].Color.B = b;
        _userOffscreenMultiplyColors[offscreenIndex].Color.A = a;
    }

    /// <summary>
    /// Offscreen の乗算色を取得する。
    /// </summary>
    public CubismTextureColor GetOffscreenMultiplyColor(int offscreenIndex)
    {
        if (offscreenIndex < 0 || _model.GetOffscreenCount() <= offscreenIndex)
        {
            WarnIndexOutOfRange(nameof(GetOffscreenMultiplyColor), offscreenIndex, _model.GetOffscreenCount() - 1);
            return new CubismTextureColor(1.0f, 1.0f, 1.0f, 1.0f);
        }

        if (GetMultiplyColorEnabled() || GetOffscreenMultiplyColorEnabled(offscreenIndex))
        {
            return _userOffscreenMultiplyColors[offscreenIndex].Color;
        }

        var tmpColor = _model.GetOffscreenMultiplyColor(offscreenIndex);

        return new CubismTextureColor(tmpColor.X, tmpColor.Y, tmpColor.Z, tmpColor.W);
    }

    /// <summary>
    /// Offscreen のスクリーン色を設定する。
    /// </summary>
    public void SetOffscreenScreenColor(int offscreenIndex, CubismTextureColor color)
    {
        SetOffscreenScreenColor(offscreenIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// Offscreen のスクリーン色を設定する。
    /// </summary>
    public void SetOffscreenScreenColor(int offscreenIndex, float r, float g, float b, float a = 1.0f)
    {
        if (offscreenIndex < 0 || _model.GetOffscreenCount() <= offscreenIndex)
        {
            WarnIndexOutOfRange(nameof(SetOffscreenScreenColor), offscreenIndex, _model.GetOffscreenCount() - 1);
            return;
        }

        _userOffscreenScreenColors[offscreenIndex].Color.R = r;
        _userOffscreenScreenColors[offscreenIndex].Color.G = g;
        _userOffscreenScreenColors[offscreenIndex].Color.B = b;
        _userOffscreenScreenColors[offscreenIndex].Color.A = a;
    }

    /// <summary>
    /// Offscreen のスクリーン色を取得する。
    /// </summary>
    public CubismTextureColor GetOffscreenScreenColor(int offscreenIndex)
    {
        if (offscreenIndex < 0 || _model.GetOffscreenCount() <= offscreenIndex)
        {
            WarnIndexOutOfRange(nameof(GetOffscreenScreenColor), offscreenIndex, _model.GetOffscreenCount() - 1);
            return new CubismTextureColor(0.0f, 0.0f, 0.0f, 1.0f);
        }

        if (GetScreenColorEnabled() || GetOffscreenScreenColorEnabled(offscreenIndex))
        {
            return _userOffscreenScreenColors[offscreenIndex].Color;
        }

        var tmpColor = _model.GetOffscreenScreenColor(offscreenIndex);

        return new CubismTextureColor(tmpColor.X, tmpColor.Y, tmpColor.Z, tmpColor.W);
    }

    /// <summary>
    /// パーツの色を設定する（官方 SetPartColor）。
    /// Offscreen が無いパーツは子へ再帰、Offscreen があるパーツはその Offscreen のみに反映する。
    /// </summary>
    private void SetPartColor(int partIndex, float r, float g, float b, float a,
        ColorData[] partColors,
        ColorData[] drawableColors,
        ColorData[] offscreenColors)
    {
        partColors[partIndex].Color.R = r;
        partColors[partIndex].Color.G = g;
        partColors[partIndex].Color.B = b;
        partColors[partIndex].Color.A = a;

        if (partColors[partIndex].IsOverridden)
        {
            unsafe
            {
                var offscreenIndex = CubismCore.GetPartOffscreenIndices(_model.Model)[partIndex];
                if (offscreenIndex == CubismModel.CubismNoIndex_Offscreen)
                {
                    // Offscreen が無い場合は子へ反映する
                    var objects = _partsHierarchy[partIndex].Objects;
                    for (int i = 0; i < objects.Count; ++i)
                    {
                        if (objects[i].ObjectType == CubismModelObjectType.ObjectType_Drawable)
                        {
                            var drawableIndex = objects[i].ObjectIndex;
                            drawableColors[drawableIndex].Color.R = r;
                            drawableColors[drawableIndex].Color.G = g;
                            drawableColors[drawableIndex].Color.B = b;
                            drawableColors[drawableIndex].Color.A = a;
                        }
                        else
                        {
                            var childPartIndex = objects[i].ObjectIndex;
                            SetPartColor(childPartIndex, r, g, b, a, partColors, drawableColors, offscreenColors);
                        }
                    }
                }
                else
                {
                    // Offscreen がある場合はその Offscreen のみに反映する
                    offscreenColors[offscreenIndex].Color.R = r;
                    offscreenColors[offscreenIndex].Color.G = g;
                    offscreenColors[offscreenIndex].Color.B = b;
                    offscreenColors[offscreenIndex].Color.A = a;
                }
            }
        }
    }

    /// <summary>
    /// パーツの色の上書きフラグを設定する（官方 SetPartColorEnabled）。
    /// </summary>
    private void SetPartColorEnabled(int partIndex, bool value,
        ColorData[] partColors,
        ColorData[] drawableColors,
        ColorData[] offscreenColors)
    {
        partColors[partIndex].IsOverridden = value;

        unsafe
        {
            var offscreenIndex = CubismCore.GetPartOffscreenIndices(_model.Model)[partIndex];
            if (offscreenIndex == CubismModel.CubismNoIndex_Offscreen)
            {
                // Offscreen が無い場合は子へ反映する
                var objects = _partsHierarchy[partIndex].Objects;
                for (int i = 0; i < objects.Count; ++i)
                {
                    if (objects[i].ObjectType == CubismModelObjectType.ObjectType_Drawable)
                    {
                        var drawableIndex = objects[i].ObjectIndex;
                        drawableColors[drawableIndex].IsOverridden = value;
                        if (value)
                        {
                            drawableColors[drawableIndex].Color.R = partColors[partIndex].Color.R;
                            drawableColors[drawableIndex].Color.G = partColors[partIndex].Color.G;
                            drawableColors[drawableIndex].Color.B = partColors[partIndex].Color.B;
                            drawableColors[drawableIndex].Color.A = partColors[partIndex].Color.A;
                        }
                    }
                    else
                    {
                        var childPartIndex = objects[i].ObjectIndex;
                        if (value)
                        {
                            partColors[childPartIndex].Color.R = partColors[partIndex].Color.R;
                            partColors[childPartIndex].Color.G = partColors[partIndex].Color.G;
                            partColors[childPartIndex].Color.B = partColors[partIndex].Color.B;
                            partColors[childPartIndex].Color.A = partColors[partIndex].Color.A;
                        }
                        SetPartColorEnabled(childPartIndex, value, partColors, drawableColors, offscreenColors);
                    }
                }
            }
            else
            {
                // Offscreen がある場合はその Offscreen のみに反映する
                offscreenColors[offscreenIndex].IsOverridden = value;
                if (value)
                {
                    offscreenColors[offscreenIndex].Color.R = partColors[partIndex].Color.R;
                    offscreenColors[offscreenIndex].Color.G = partColors[partIndex].Color.G;
                    offscreenColors[offscreenIndex].Color.B = partColors[partIndex].Color.B;
                    offscreenColors[offscreenIndex].Color.A = partColors[partIndex].Color.A;
                }
            }
        }
    }

    /// <summary>
    /// インデックスが範囲外のときに警告を出す（官方 WarnIndexOutOfRange）。
    /// </summary>
    private static void WarnIndexOutOfRange(string funcName, int index, int range)
    {
        CubismLog.Warning($"{funcName}() : index is out of range. index = {index}, valid range = [0, {range}].");
    }
}
