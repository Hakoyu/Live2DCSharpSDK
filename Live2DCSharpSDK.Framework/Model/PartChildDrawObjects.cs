namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// パーツの子描画オブジェクト情報（官方 CubismModel.hpp:59-93 の PartChildDrawObjects）。
/// 官方は csmVector&lt;csmUint32&gt; だが、C# 側は既存コードに合わせて int の List で持つ。
/// </summary>
public class PartChildDrawObjects
{
    /// <summary>
    /// 子 Drawable のインデックス一覧
    /// </summary>
    public List<int> DrawableIndices { get; } = [];
    /// <summary>
    /// 子 Offscreen のインデックス一覧
    /// </summary>
    public List<int> OffscreenIndices { get; } = [];
}
