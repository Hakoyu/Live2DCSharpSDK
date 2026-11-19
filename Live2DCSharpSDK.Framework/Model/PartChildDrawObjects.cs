namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 部件的子绘制对象信息 (官方 CubismModel.hpp:59-93 的 PartChildDrawObjects).
/// 官方为 csmVector&lt;csmUint32&gt;, 但 C# 侧配合既有代码使用 int 的 List.
/// </summary>
public class PartChildDrawObjects
{
    /// <summary>
    /// 子 Drawable 的索引列表
    /// </summary>
    public List<int> DrawableIndices { get; } = [];

    /// <summary>
    /// 子 Offscreen 的索引列表
    /// </summary>
    public List<int> OffscreenIndices { get; } = [];
}
