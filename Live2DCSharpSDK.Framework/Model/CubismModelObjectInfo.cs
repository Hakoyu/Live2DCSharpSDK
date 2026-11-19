namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// モデルのオブジェクト種別（官方 CubismModelObjectInfo::ObjectType）。
/// C# はメンバー名と入れ子型名を同一にできない（CS0102）ため、列挙型のみトップレベルに置く。
/// 値名は官方の ObjectType_Drawable / ObjectType_Parts をそのまま使う。
/// </summary>
public enum CubismModelObjectType
{
    /// <summary>Drawable</summary>
    ObjectType_Drawable = 0,
    /// <summary>Part</summary>
    ObjectType_Parts = 1
}

/// <summary>
/// モデルのオブジェクト情報（官方 CubismModel.hpp:25-57 の CubismModelObjectInfo）。
/// パーツ階層の子オブジェクト（Drawable / Part）を表す。
/// </summary>
public struct CubismModelObjectInfo
{
    /// <summary>
    /// オブジェクトの種別
    /// </summary>
    public CubismModelObjectType ObjectType;
    /// <summary>
    /// オブジェクトのインデックス
    /// </summary>
    public int ObjectIndex;

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="objectIndex">オブジェクトのインデックス</param>
    /// <param name="type">オブジェクトの種別</param>
    public CubismModelObjectInfo(int objectIndex, CubismModelObjectType type)
    {
        ObjectIndex = objectIndex;
        ObjectType = type;
    }
}
