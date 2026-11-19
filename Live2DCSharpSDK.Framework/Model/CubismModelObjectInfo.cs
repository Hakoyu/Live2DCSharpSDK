namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 对象的类别 (官方 CubismModelObjectInfo::ObjectType).
/// 由于 C# 不能使成员名与嵌套类型名相同 (CS0102), 因此只把枚举类型放在顶层.
/// 值名直接使用官方的 ObjectType_Drawable / ObjectType_Parts.
/// </summary>
public enum CubismModelObjectType
{
    /// <summary>Drawable</summary>
    ObjectType_Drawable = 0,
    /// <summary>Part</summary>
    ObjectType_Parts = 1
}

/// <summary>
/// 模型的对象信息 (官方 CubismModel.hpp:25-57 的 CubismModelObjectInfo).
/// 表示部件层级的子对象 (Drawable / Part).
/// </summary>
public struct CubismModelObjectInfo
{
    /// <summary>
    /// 对象的类别
    /// </summary>
    public CubismModelObjectType ObjectType;
    /// <summary>
    /// 对象的索引
    /// </summary>
    public int ObjectIndex;

    /// <summary>
    /// 构造函数.
    /// </summary>
    /// <param name="objectIndex">对象的索引</param>
    /// <param name="type">对象的类别</param>
    public CubismModelObjectInfo(int objectIndex, CubismModelObjectType type)
    {
        ObjectIndex = objectIndex;
        ObjectType = type;
    }
}
