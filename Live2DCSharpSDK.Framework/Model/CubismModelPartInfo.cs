namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// パーツ情報（官方 CubismModel.hpp:95-140 の CubismModelPartInfo）。
/// </summary>
public class CubismModelPartInfo
{
    /// <summary>
    /// 子オブジェクト情報の一覧
    /// </summary>
    public List<CubismModelObjectInfo> Objects { get; } = [];
    /// <summary>
    /// 子描画オブジェクト情報
    /// </summary>
    public PartChildDrawObjects ChildDrawObjects { get; } = new();

    /// <summary>
    /// 子オブジェクトの個数を取得する。
    /// </summary>
    /// <returns>子オブジェクトの個数</returns>
    public int GetChildObjectCount()
    {
        return Objects.Count;
    }
}
