namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 部件信息 (官方 CubismModel.hpp:95-140 的 CubismModelPartInfo).
/// </summary>
public class CubismModelPartInfo
{
    /// <summary>
    /// 子对象信息的列表
    /// </summary>
    public List<CubismModelObjectInfo> Objects { get; } = [];
    /// <summary>
    /// 子绘制对象信息
    /// </summary>
    public PartChildDrawObjects ChildDrawObjects { get; } = new();

    /// <summary>
    /// 获取子对象的个数.
    /// </summary>
    /// <returns>子对象的个数</returns>
    public int GetChildObjectCount()
    {
        return Objects.Count;
    }
}
