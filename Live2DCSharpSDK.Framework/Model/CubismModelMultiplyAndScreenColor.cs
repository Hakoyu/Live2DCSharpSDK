using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 处理模型的乘算色与屏幕色的类.
/// 移植自官方 Framework/src/Model/CubismModelMultiplyAndScreenColor.{hpp,cpp}.
/// 5-r.5 中从 CubismModel 分离 (CHANGELOG: Change multiply and screen color functions to separate class with renamed methods).
/// </summary>
public class CubismModelMultiplyAndScreenColor
{
    /// <summary>
    /// 绘制对象的颜色信息 (官方 ColorData).
    /// 替代旧的 DrawableColorData / PartColorData (官方已整合到本类的 ColorData).
    /// </summary>
    public struct ColorData
    {
        /// <summary>
        /// 是否用 SDK 侧的颜色覆盖
        /// </summary>
        public bool IsOverridden;
        /// <summary>
        /// 色
        /// </summary>
        public CubismTextureColor Color;

        /// <summary>
        /// 构造函数.
        /// </summary>
        public ColorData(bool isOverridden, CubismTextureColor color)
        {
            IsOverridden = isOverridden;
            Color = color;
        }
    }

    /// <summary>
    /// 模型 (用于获取部件层级与颜色数量)
    /// </summary>
    private readonly CubismModel _model;
    /// <summary>
    /// 部件层级(引用 CubismModel 持有的 List, 不要重新赋值).
    /// </summary>
    private readonly List<CubismModelPartInfo> _partsHierarchy;
    /// <summary>
    /// 是否覆盖全部乘算色?
    /// </summary>
    private bool _isOverriddenModelMultiplyColors;
    /// <summary>
    /// 是否覆盖全部屏幕色?
    /// </summary>
    private bool _isOverriddenModelScreenColors;

    private ColorData[] _userPartScreenColors = [];
    private ColorData[] _userPartMultiplyColors = [];
    private ColorData[] _userDrawableScreenColors = [];
    private ColorData[] _userDrawableMultiplyColors = [];
    private ColorData[] _userOffscreenScreenColors = [];
    private ColorData[] _userOffscreenMultiplyColors = [];

    /// <summary>
    /// 构造函数.
    /// 官方在 CubismModel 的初始化列表中传入 this 和 _partsHierarchy 的地址.
    /// C# 侧同样在构造函数中接收模型与层级列表的引用 (引用会在之后填充).
    /// </summary>
    /// <param name="model">Cubism 模型</param>
    /// <param name="partsHierarchy">部件层级 (CubismModel 持有的 List 本身)</param>
    public CubismModelMultiplyAndScreenColor(CubismModel model, List<CubismModelPartInfo> partsHierarchy)
    {
        _model = model;
        _partsHierarchy = partsHierarchy;
        _isOverriddenModelMultiplyColors = false;
        _isOverriddenModelScreenColors = false;
    }

    /// <summary>
    /// 使用乘算色与屏幕色所需的初始化.
    /// </summary>
    /// <param name="partCount">部件的个数</param>
    /// <param name="drawableCount">Drawable 的个数</param>
    /// <param name="offscreenCount">Offscreen 的个数</param>
    public void Initialize(int partCount, int drawableCount, int offscreenCount)
    {
        // 乗算色
        var userMultiplyColor = new ColorData(false, new CubismTextureColor(1.0f, 1.0f, 1.0f, 1.0f));

        // 屏幕色
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
    /// 设置是否把运行时设置的颜色作为模型整体的乘算色使用.
    /// </summary>
    /// <param name="value">为 true 时, 使用运行时设置的颜色</param>
    public void SetMultiplyColorEnabled(bool value)
    {
        _isOverriddenModelMultiplyColors = value;
    }

    /// <summary>
    /// 获取是否把运行时设置的颜色作为模型整体的乘算色使用.
    /// </summary>
    public bool GetMultiplyColorEnabled()
    {
        return _isOverriddenModelMultiplyColors;
    }

    /// <summary>
    /// 设置是否把运行时设置的颜色作为模型整体的屏幕色使用.
    /// </summary>
    /// <param name="value">为 true 时, 使用运行时设置的颜色</param>
    public void SetScreenColorEnabled(bool value)
    {
        _isOverriddenModelScreenColors = value;
    }

    /// <summary>
    /// 获取是否把运行时设置的颜色作为模型整体的屏幕色使用.
    /// </summary>
    public bool GetScreenColorEnabled()
    {
        return _isOverriddenModelScreenColors;
    }

    /// <summary>
    /// 设置是否用 SDK 侧的颜色覆盖部件的乘算色.
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
    /// 获取是否用 SDK 侧的颜色覆盖部件的乘算色.
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
    /// 设置是否用 SDK 侧的颜色覆盖部件的屏幕色.
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
    /// 获取是否用 SDK 侧的颜色覆盖部件的屏幕色.
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
    /// 设置部件的乘算色.
    /// </summary>
    public void SetPartMultiplyColor(int partIndex, CubismTextureColor color)
    {
        SetPartMultiplyColor(partIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// 设置部件的乘算色.
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
    /// 获取部件的乘算色.
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
    /// 设置部件的屏幕色.
    /// </summary>
    public void SetPartScreenColor(int partIndex, CubismTextureColor color)
    {
        SetPartScreenColor(partIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// 设置部件的屏幕色.
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
    /// 获取部件的屏幕色.
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
    /// 设置是否把运行时设置的颜色作为 Drawable 的乘算色使用.
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
    /// 获取是否把运行时设置的颜色作为 Drawable 的乘算色使用.
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
    /// 设置是否把运行时设置的颜色作为 Drawable 的屏幕色使用.
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
    /// 获取是否把运行时设置的颜色作为 Drawable 的屏幕色使用.
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
    /// 设置 Drawable 的乘算色.
    /// </summary>
    public void SetDrawableMultiplyColor(int drawableIndex, CubismTextureColor color)
    {
        SetDrawableMultiplyColor(drawableIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// 设置 Drawable 的乘算色.
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
    /// 获取 Drawable 的乘算色.
    /// SDK 侧未覆盖时返回模型 (Core) 的颜色.
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
    /// 设置 Drawable 的屏幕色.
    /// </summary>
    public void SetDrawableScreenColor(int drawableIndex, CubismTextureColor color)
    {
        SetDrawableScreenColor(drawableIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// 设置 Drawable 的屏幕色.
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
    /// 获取 Drawable 的屏幕色.
    /// SDK 侧未覆盖时返回模型 (Core) 的颜色.
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
    /// 设置是否用 SDK 侧的颜色覆盖 Offscreen 的乘算色.
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
    /// 获取是否用 SDK 侧的颜色覆盖 Offscreen 的乘算色.
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
    /// 设置是否用 SDK 侧的颜色覆盖 Offscreen 的屏幕色.
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
    /// 获取是否用 SDK 侧的颜色覆盖 Offscreen 的屏幕色.
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
    /// 设置 Offscreen 的乘算色.
    /// </summary>
    public void SetOffscreenMultiplyColor(int offscreenIndex, CubismTextureColor color)
    {
        SetOffscreenMultiplyColor(offscreenIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// 设置 Offscreen 的乘算色.
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
    /// 获取 Offscreen 的乘算色.
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
    /// 设置 Offscreen 的屏幕色.
    /// </summary>
    public void SetOffscreenScreenColor(int offscreenIndex, CubismTextureColor color)
    {
        SetOffscreenScreenColor(offscreenIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// 设置 Offscreen 的屏幕色.
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
    /// 获取 Offscreen 的屏幕色.
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
    /// 设置部件的颜色 (官方 SetPartColor).
    /// 没有 Offscreen 的部件递归到子级, 有 Offscreen 的部件只反映到该 Offscreen.
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
                    // 没有 Offscreen 时反映到子级
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
                    // 有 Offscreen 时只反映到该 Offscreen
                    offscreenColors[offscreenIndex].Color.R = r;
                    offscreenColors[offscreenIndex].Color.G = g;
                    offscreenColors[offscreenIndex].Color.B = b;
                    offscreenColors[offscreenIndex].Color.A = a;
                }
            }
        }
    }

    /// <summary>
    /// 设置部件颜色的覆盖标志 (官方 SetPartColorEnabled).
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
                // 没有 Offscreen 时反映到子级
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
                // 有 Offscreen 时只反映到该 Offscreen
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
    /// 索引超出范围时发出警告 (官方 WarnIndexOutOfRange).
    /// </summary>
    private static void WarnIndexOutOfRange(string funcName, int index, int range)
    {
        CubismLog.Warning($"{funcName}() : index is out of range. index = {index}, valid range = [0, {range}].");
    }
}
