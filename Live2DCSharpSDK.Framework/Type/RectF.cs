namespace Live2DCSharpSDK.Framework.Type;

/// <summary>
/// 定义矩形形状(坐标与长度均为 float 值)的类
/// </summary>
public class RectF
{
    /// <summary>
    /// 左端 X 坐标
    /// </summary>
    public float X;

    /// <summary>
    /// 上端 Y 坐标
    /// </summary>
    public float Y;

    /// <summary>
    /// 宽度
    /// </summary>
    public float Width;

    /// <summary>
    /// 高度
    /// </summary>
    public float Height;

    public RectF() { }

    /// <summary>
    /// 带参数的构造函数
    /// </summary>
    /// <param name="x">左端 X 坐标</param>
    /// <param name="y">上端 Y 坐标</param>
    /// <param name="w">宽度</param>
    /// <param name="h">高度</param>
    public RectF(float x, float y, float w, float h)
    {
        X = x;
        Y = y;
        Width = w;
        Height = h;
    }

    /// <summary>
    /// 为矩形赋值
    /// </summary>
    /// <param name="r">矩形实例</param>
    public void SetRect(RectF r)
    {
        X = r.X;
        Y = r.Y;
        Width = r.Width;
        Height = r.Height;
    }

    /// <summary>
    /// 以矩形中心为轴按纵横方向缩放
    /// </summary>
    /// <param name="w">宽度方向的缩放量</param>
    /// <param name="h">高度方向的缩放量</param>
    public void Expand(float w, float h)
    {
        X -= w;
        Y -= h;
        Width += w * 2.0f;
        Height += h * 2.0f;
    }

    /// <summary>
    /// 获取矩形中心的 X 坐标
    /// </summary>
    public float GetCenterX()
    {
        return X + 0.5f * Width;
    }

    /// <summary>
    /// 获取矩形中心的 Y 坐标
    /// </summary>
    public float GetCenterY()
    {
        return Y + 0.5f * Height;
    }

    /// <summary>
    /// 获取右端的 X 坐标
    /// </summary>
    public float GetRight()
    {
        return X + Width;
    }

    /// <summary>
    /// 获取下端的 Y 坐标
    /// </summary>
    public float GetBottom()
    {
        return Y + Height;
    }
}
