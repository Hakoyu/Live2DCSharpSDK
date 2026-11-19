namespace Live2DCSharpSDK.Framework.Math;

/// <summary>
/// 4x4 矩阵的便捷类.
/// </summary>
public record CubismMatrix44
{
    private readonly float[] Ident =
    [
        1.0f,
        0.0f,
        0.0f,
        0.0f,
        0.0f,
        1.0f,
        0.0f,
        0.0f,
        0.0f,
        0.0f,
        1.0f,
        0.0f,
        0.0f,
        0.0f,
        0.0f,
        1.0f,
    ];

    private readonly float[] _mpt1 =
    [
        1.0f,
        0.0f,
        0.0f,
        0.0f,
        0.0f,
        1.0f,
        0.0f,
        0.0f,
        0.0f,
        0.0f,
        1.0f,
        0.0f,
        0.0f,
        0.0f,
        0.0f,
        1.0f,
    ];

    private readonly float[] _mpt2 =
    [
        1.0f,
        0.0f,
        0.0f,
        0.0f,
        0.0f,
        1.0f,
        0.0f,
        0.0f,
        0.0f,
        0.0f,
        1.0f,
        0.0f,
        0.0f,
        0.0f,
        0.0f,
        1.0f,
    ];

    /// <summary>
    /// 4x4 矩阵数据
    /// </summary>
    protected float[] _tr = new float[16];

    public float[] Tr => _tr;

    /// <summary>
    /// 构造函数.
    /// </summary>
    public CubismMatrix44()
    {
        LoadIdentity();
    }

    /// <summary>
    /// 初始化为单位矩阵.
    /// </summary>
    public void LoadIdentity()
    {
        SetMatrix(Ident);
    }

    /// <summary>
    /// 设置矩阵.
    /// </summary>
    /// <param name="tr">由 16 个浮点数表示的 4x4 矩阵</param>
    public void SetMatrix(float[] tr)
    {
        Array.Copy(tr, _tr, 16);
    }

    public void SetMatrix(CubismMatrix44 tr)
    {
        SetMatrix(tr.Tr);
    }

    /// <summary>
    /// 获取 X 轴的缩放率.
    /// </summary>
    /// <returns>X 轴的缩放率</returns>
    public float GetScaleX()
    {
        return _tr[0];
    }

    /// <summary>
    /// 获取 Y 轴的缩放率.
    /// </summary>
    /// <returns>Y 轴的缩放率</returns>
    public float GetScaleY()
    {
        return _tr[5];
    }

    /// <summary>
    /// 获取 X 轴的移动量.
    /// </summary>
    /// <returns>X 轴的移动量</returns>
    public float GetTranslateX()
    {
        return _tr[12];
    }

    /// <summary>
    /// 获取 Y 轴的移动量.
    /// </summary>
    /// <returns>Y 轴的移动量</returns>
    public float GetTranslateY()
    {
        return _tr[13];
    }

    /// <summary>
    /// 用当前矩阵计算 X 轴的值.
    /// </summary>
    /// <param name="src">X 轴的值</param>
    /// <returns>用当前矩阵计算得到的 X 轴的值</returns>
    public float TransformX(float src)
    {
        return _tr[0] * src + _tr[12];
    }

    /// <summary>
    /// 用当前矩阵计算 Y 轴的值.
    /// </summary>
    /// <param name="src">Y 轴的值</param>
    /// <returns>用当前矩阵计算得到的 Y 轴的值</returns>
    public float TransformY(float src)
    {
        return _tr[5] * src + _tr[13];
    }

    /// <summary>
    /// 用当前矩阵逆算 X 轴的值.
    /// </summary>
    /// <param name="src">X 轴的值</param>
    /// <returns>用当前矩阵逆算得到的 X 轴的值</returns>
    public float InvertTransformX(float src)
    {
        return (src - _tr[12]) / _tr[0];
    }

    /// <summary>
    /// 用当前矩阵逆算 Y 轴的值.
    /// </summary>
    /// <param name="src">Y 轴的值</param>
    /// <returns>用当前矩阵逆算得到的 Y 轴的值</returns>
    public float InvertTransformY(float src)
    {
        return (src - _tr[13]) / _tr[5];
    }

    /// <summary>
    /// 以当前矩阵的位置为起点进行相对移动.
    /// </summary>
    /// <param name="x">X 轴的移动量</param>
    /// <param name="y">Y 轴的移动量</param>
    public void TranslateRelative(float x, float y)
    {
        _mpt1[12] = x;
        _mpt1[13] = y;
        MultiplyByMatrix(_mpt1);
    }

    /// <summary>
    /// 把当前矩阵的位置移动到指定位置.
    /// </summary>
    /// <param name="x">X 轴的移动量</param>
    /// <param name="y">Y 轴的移动量</param>
    public void Translate(float x, float y)
    {
        _tr[12] = x;
        _tr[13] = y;
    }

    /// <summary>
    /// 把当前矩阵的 X 轴位置移动到指定位置.
    /// </summary>
    /// <param name="x">X 轴的移动量</param>
    public void TranslateX(float x)
    {
        _tr[12] = x;
    }

    /// <summary>
    /// 把当前矩阵的 Y 轴位置移动到指定位置.
    /// </summary>
    /// <param name="y">Y 轴的移动量</param>
    public void TranslateY(float y)
    {
        _tr[13] = y;
    }

    /// <summary>
    /// 相对设置当前矩阵的缩放率.
    /// </summary>
    /// <param name="x">X 轴的缩放率</param>
    /// <param name="y">Y 轴的缩放率</param>
    public void ScaleRelative(float x, float y)
    {
        _mpt2[0] = x;
        _mpt2[5] = y;
        MultiplyByMatrix(_mpt2);
    }

    /// <summary>
    /// 把当前矩阵的缩放率设置为指定倍率.
    /// </summary>
    /// <param name="x">X 轴的缩放率</param>
    /// <param name="y">Y 轴的缩放率</param>
    public void Scale(float x, float y)
    {
        _tr[0] = x;
        _tr[5] = y;
    }

    public float[] _mpt = new float[16];

    public void MultiplyByMatrix(float[] a)
    {
        Array.Fill(_mpt, 0);

        int n = 4;

        for (int i = 0; i < n; ++i)
        {
            for (int j = 0; j < n; ++j)
            {
                for (int k = 0; k < n; ++k)
                {
                    _mpt[j + i * 4] += a[k + i * 4] * _tr[j + k * 4];
                }
            }
        }

        Array.Copy(_mpt, _tr, 16);
    }

    /// <summary>
    /// 把矩阵乘到当前矩阵上.
    /// </summary>
    /// <param name="m">矩阵</param>
    public void MultiplyByMatrix(CubismMatrix44 m)
    {
        MultiplyByMatrix(m.Tr);
    }

    /// <summary>
    /// 返回当前矩阵的逆矩阵 (官方 5-r.5 中新增的 CubismMatrix44::GetInvert).
    /// </summary>
    /// <returns>逆矩阵. 行列式接近 0 时为单位矩阵</returns>
    public CubismMatrix44 GetInvert()
    {
        float r00 = _tr[0];
        float r10 = _tr[1];
        float r20 = _tr[2];
        float r01 = _tr[4];
        float r11 = _tr[5];
        float r21 = _tr[6];
        float r02 = _tr[8];
        float r12 = _tr[9];
        float r22 = _tr[10];

        float tx = _tr[12];
        float ty = _tr[13];
        float tz = _tr[14];

        float det =
            (r00 * ((r11 * r22) - (r12 * r21)))
            - (r01 * ((r10 * r22) - (r12 * r20)))
            + (r02 * ((r10 * r21) - (r11 * r20)));

        var dst = new CubismMatrix44();

        if (CubismMath.AbsF(det) < CubismMath.Epsilon)
        {
            dst.LoadIdentity();
            return dst;
        }

        float invDet = 1.0f / det;

        float inv00 = ((r11 * r22) - (r12 * r21)) * invDet;
        float inv01 = -((r01 * r22) - (r02 * r21)) * invDet;
        float inv02 = ((r01 * r12) - (r02 * r11)) * invDet;
        float inv10 = -((r10 * r22) - (r12 * r20)) * invDet;
        float inv11 = ((r00 * r22) - (r02 * r20)) * invDet;
        float inv12 = -((r00 * r12) - (r02 * r10)) * invDet;
        float inv20 = ((r10 * r21) - (r11 * r20)) * invDet;
        float inv21 = -((r00 * r21) - (r01 * r20)) * invDet;
        float inv22 = ((r00 * r11) - (r01 * r10)) * invDet;

        dst.Tr[0] = inv00;
        dst.Tr[1] = inv10;
        dst.Tr[2] = inv20;
        dst.Tr[3] = 0.0f;
        dst.Tr[4] = inv01;
        dst.Tr[5] = inv11;
        dst.Tr[6] = inv21;
        dst.Tr[7] = 0.0f;
        dst.Tr[8] = inv02;
        dst.Tr[9] = inv12;
        dst.Tr[10] = inv22;
        dst.Tr[11] = 0.0f;

        dst.Tr[12] = -((inv00 * tx) + (inv01 * ty) + (inv02 * tz));
        dst.Tr[13] = -((inv10 * tx) + (inv11 * ty) + (inv12 * tz));
        dst.Tr[14] = -((inv20 * tx) + (inv21 * ty) + (inv22 * tz));
        dst.Tr[15] = 1.0f;

        return dst;
    }
}
