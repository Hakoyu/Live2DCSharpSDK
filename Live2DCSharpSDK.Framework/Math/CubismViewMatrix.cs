namespace Live2DCSharpSDK.Framework.Math;

/// <summary>
/// 用于相机位置变化的便捷 4x4 矩阵类.
/// </summary>
public record CubismViewMatrix : CubismMatrix44
{
    /// <summary>
    /// 与设备对应的逻辑坐标上的范围 (左边缘 X 轴位置)
    /// </summary>
    public float ScreenLeft { get; private set; }
    /// <summary>
    /// 与设备对应的逻辑坐标上的范围 (右边缘 X 轴位置)
    /// </summary>
    public float ScreenRight { get; private set; }
    /// <summary>
    /// 与设备对应的逻辑坐标上的范围 (下边缘 Y 轴位置)
    /// </summary>
    public float ScreenTop { get; private set; }
    /// <summary>
    /// 与设备对应的逻辑坐标上的范围 (上边缘 Y 轴位置)
    /// </summary>
    public float ScreenBottom { get; private set; }
    /// <summary>
    /// 逻辑坐标上的可移动范围 (左边缘 X 轴位置)
    /// </summary>
    public float MaxLeft { get; private set; }
    /// <summary>
    /// 逻辑坐标上的可移动范围 (右边缘 X 轴位置)
    /// </summary>
    public float MaxRight { get; private set; }
    /// <summary>
    /// 逻辑坐标上的可移动范围 (下边缘 Y 轴位置)
    /// </summary>
    public float MaxTop { get; private set; }
    /// <summary>
    /// 逻辑坐标上的可移动范围 (上边缘 Y 轴位置)
    /// </summary>
    public float MaxBottom { get; private set; }
    /// <summary>
    /// 缩放率的最大值
    /// </summary>
    public float MaxScale { get; set; }
    /// <summary>
    /// 缩放率的最小值
    /// </summary>
    public float MinScale { get; set; }

    /// <summary>
    /// 调整移动.
    /// </summary>
    /// <param name="x">X 轴的移动量</param>
    /// <param name="y">Y 轴的移动量</param>
    public void AdjustTranslate(float x, float y)
    {
        if (_tr[0] * MaxLeft + (_tr[12] + x) > ScreenLeft)
        {
            x = ScreenLeft - _tr[0] * MaxLeft - _tr[12];
        }

        if (_tr[0] * MaxRight + (_tr[12] + x) < ScreenRight)
        {
            x = ScreenRight - _tr[0] * MaxRight - _tr[12];
        }


        if (_tr[5] * MaxTop + (_tr[13] + y) < ScreenTop)
        {
            y = ScreenTop - _tr[5] * MaxTop - _tr[13];
        }

        if (_tr[5] * MaxBottom + (_tr[13] + y) > ScreenBottom)
        {
            y = ScreenBottom - _tr[5] * MaxBottom - _tr[13];
        }

        float[] tr1 = [ 1.0f,   0.0f,   0.0f, 0.0f,
                        0.0f,   1.0f,   0.0f, 0.0f,
                        0.0f,   0.0f,   1.0f, 0.0f,
                        x,      y,      0.0f, 1.0f ];
        MultiplyByMatrix(tr1);
    }

    /// <summary>
    /// 调整缩放率.
    /// </summary>
    /// <param name="cx">进行缩放的 X 轴中心位置</param>
    /// <param name="cy">进行缩放的 Y 轴中心位置</param>
    /// <param name="scale">缩放率</param>
    public void AdjustScale(float cx, float cy, float scale)
    {
        float maxScale = MaxScale;
        float minScale = MinScale;

        float targetScale = scale * _tr[0]; //

        if (targetScale < minScale)
        {
            if (_tr[0] > 0.0f)
            {
                scale = minScale / _tr[0];
            }
        }
        else if (targetScale > maxScale)
        {
            if (_tr[0] > 0.0f)
            {
                scale = maxScale / _tr[0];
            }
        }

        MultiplyByMatrix([1.0f, 0.0f, 0.0f, 0.0f,
                            0.0f, 1.0f, 0.0f, 0.0f,
                            0.0f, 0.0f, 1.0f, 0.0f,
                            -cx,  -cy,  0.0f, 1.0f]);
        MultiplyByMatrix([scale, 0.0f,  0.0f, 0.0f,
                            0.0f,  scale, 0.0f, 0.0f,
                            0.0f,  0.0f,  1.0f, 0.0f,
                            0.0f,  0.0f,  0.0f, 1.0f]);
        MultiplyByMatrix([1.0f, 0.0f, 0.0f, 0.0f,
                            0.0f, 1.0f, 0.0f, 0.0f,
                            0.0f, 0.0f, 1.0f, 0.0f,
                            cx,   cy,   0.0f, 1.0f]);
    }

    /// <summary>
    /// 设置与设备对应的逻辑坐标上的范围.
    /// </summary>
    /// <param name="left">左边缘的 X 轴位置</param>
    /// <param name="right">右边缘的 X 轴位置</param>
    /// <param name="bottom">下边缘的 Y 轴位置</param>
    /// <param name="top">上边缘的 Y 轴位置</param>
    public void SetScreenRect(float left, float right, float bottom, float top)
    {
        ScreenLeft = left;
        ScreenRight = right;
        ScreenTop = top;
        ScreenBottom = bottom;
    }

    /// <summary>
    /// 设置与设备对应的逻辑坐标上的可移动范围.
    /// </summary>
    /// <param name="left">左边缘的 X 轴位置</param>
    /// <param name="right">右边缘的 X 轴位置</param>
    /// <param name="bottom">下边缘的 Y 轴位置</param>
    /// <param name="top">上边缘的 Y 轴位置</param>
    public void SetMaxScreenRect(float left, float right, float bottom, float top)
    {
        MaxLeft = left;
        MaxRight = right;
        MaxTop = top;
        MaxBottom = bottom;
    }

    /// <summary>
    /// 确认缩放率是否已达到最大值.
    /// </summary>
    /// <returns>true    缩放率已达到最大值
    /// false   缩放率尚未达到最大值</returns>
    public bool IsMaxScale()
    {
        return GetScaleX() >= MaxScale;
    }

    /// <summary>
    /// 确认缩放率是否已达到最小值.
    /// </summary>
    /// <returns>true    缩放率已达到最小值
    /// false   缩放率尚未达到最小值</returns>
    public bool IsMinScale()
    {
        return GetScaleX() <= MinScale;
    }
}
