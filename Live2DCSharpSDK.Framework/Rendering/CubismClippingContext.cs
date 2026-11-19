using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Type;

namespace Live2DCSharpSDK.Framework.Rendering;

public abstract unsafe class CubismClippingContext(
    CubismClippingManager manager,
    int* clippingDrawableIndices,
    int clipCount
)
{
    /// <summary>
    /// 如果当前的绘制状态需要准备遮罩则为 true
    /// </summary>
    public bool IsUsing;

    /// <summary>
    /// 裁剪遮罩的 ID 列表
    /// </summary>
    public unsafe int* ClippingIdList = clippingDrawableIndices;

    /// <summary>
    /// 裁剪遮罩的数量
    /// </summary>
    public int ClippingIdCount = clipCount;

    /// <summary>
    /// 把这个裁剪放置到 RGBA 的哪个通道(0:R , 1:G , 2:B , 3:A)
    /// </summary>
    public int LayoutChannelIndex = 0;

    /// <summary>
    /// 把遮罩放入遮罩用通道的哪个区域(View 坐标 -1..1, UV 换算为 0..1)
    /// </summary>
    public RectF LayoutBounds = new();

    /// <summary>
    /// 使用此裁剪时, 所有被裁剪的绘制对象的包围矩形 (每次更新)
    /// </summary>
    public RectF AllClippedDrawRect = new();

    /// <summary>
    /// 保持遮罩位置计算结果的矩阵
    /// </summary>
    public CubismMatrix44 MatrixForMask = new();

    /// <summary>
    /// 保持绘制对象位置计算结果的矩阵
    /// </summary>
    public CubismMatrix44 MatrixForDraw = new();

    /// <summary>
    /// 被此遮罩裁剪的绘制对象列表
    /// </summary>
    public List<int> ClippedDrawableIndexList = [];

    /// <summary>
    /// 被此遮罩裁剪的 Offscreen 列表 (R5 追加, CubismRenderer.hpp:351)
    /// </summary>
    public List<int> ClippedOffscreenIndexList = [];

    /// <summary>
    /// 此遮罩被分配的渲染纹理 (帧缓冲) 与颜色缓冲区的索引
    /// </summary>
    public int BufferIndex;

    public CubismClippingManager Manager { get; } = manager;

    /// <summary>
    /// 添加被此遮罩裁剪的绘制对象
    /// </summary>
    /// <param name="drawableIndex">要添加到裁剪目标的绘制对象的索引</param>
    public void AddClippedDrawable(int drawableIndex)
    {
        ClippedDrawableIndexList.Add(drawableIndex);
    }

    /// <summary>
    /// 添加被此遮罩裁剪的 Offscreen (R5 追加, CubismRenderer.cpp:215-218)
    /// </summary>
    /// <param name="offscreenIndex">要添加到裁剪目标的 Offscreen 的索引</param>
    public void AddClippedOffscreen(int offscreenIndex)
    {
        ClippedOffscreenIndexList.Add(offscreenIndex);
    }
}
