using System.Numerics;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Math;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Math;

/// <summary>
/// CubismMath 的纯托管单元测试 (A1).
///
/// 覆盖内容: 浮点包装函数的冒烟对照, 缓动, 限幅, 方向换算, 取余,
/// 以及二次/三次方程求解的边界; 不加载 Core, 不触碰真实 GL.
/// </summary>
[TestClass]
public sealed class CubismMathTests
{
    /// <summary>
    /// 与 MathF 对照时使用的浮点误差容限.
    /// </summary>
    private const float Tolerance = 1e-6f;

    /// <summary>
    /// 常量契约: Pi 与 Epsilon 的取值.
    /// </summary>
    [TestMethod]
    public void ConstantsMatchContract()
    {
        Assert.AreEqual(3.1415927f, CubismMath.Pi, Tolerance);
        Assert.AreEqual(0.00001f, CubismMath.Epsilon, 0.0f);
    }

    /// <summary>
    /// SinF 与 MathF.Sin 的误差小于 1e-6.
    /// </summary>
    /// <param name="value">角度[rad]</param>
    [TestMethod]
    [DataRow(0.0f)]
    [DataRow(0.5235988f)]
    [DataRow(1.5707964f)]
    [DataRow(3.1415927f)]
    [DataRow(-1.0471976f)]
    [DataRow(12.5f)]
    public void SinFMatchesMathF(float value)
    {
        Assert.AreEqual(MathF.Sin(value), CubismMath.SinF(value), Tolerance);
    }

    /// <summary>
    /// CosF 与 MathF.Cos 的误差小于 1e-6.
    /// </summary>
    /// <param name="value">角度[rad]</param>
    [TestMethod]
    [DataRow(0.0f)]
    [DataRow(0.5235988f)]
    [DataRow(1.5707964f)]
    [DataRow(3.1415927f)]
    [DataRow(-1.0471976f)]
    [DataRow(12.5f)]
    public void CosFMatchesMathF(float value)
    {
        Assert.AreEqual(MathF.Cos(value), CubismMath.CosF(value), Tolerance);
    }

    /// <summary>
    /// AbsF 与 MathF.Abs 的误差小于 1e-6.
    /// </summary>
    /// <param name="value">输入值</param>
    [TestMethod]
    [DataRow(0.0f)]
    [DataRow(-0.0f)]
    [DataRow(1.5f)]
    [DataRow(-1.5f)]
    [DataRow(-123456.75f)]
    public void AbsFMatchesMathF(float value)
    {
        Assert.AreEqual(MathF.Abs(value), CubismMath.AbsF(value), Tolerance);
    }

    /// <summary>
    /// SqrtF 与 MathF.Sqrt 的误差小于 1e-6.
    /// </summary>
    /// <param name="value">输入值</param>
    [TestMethod]
    [DataRow(0.0f)]
    [DataRow(1.0f)]
    [DataRow(2.0f)]
    [DataRow(4.0f)]
    [DataRow(0.25f)]
    [DataRow(0.000001f)]
    public void SqrtFMatchesMathF(float value)
    {
        Assert.AreEqual(MathF.Sqrt(value), CubismMath.SqrtF(value), Tolerance);
    }

    /// <summary>
    /// GetEasingSine 的缓动曲线与越界钳制.
    /// </summary>
    /// <param name="value">缓动输入</param>
    /// <param name="expected">期望值</param>
    [TestMethod]
    [DataRow(-1.0f, 0.0f)]
    [DataRow(-0.001f, 0.0f)]
    [DataRow(0.0f, 0.0f)]
    [DataRow(0.25f, 0.1464466f)]
    [DataRow(0.5f, 0.5f)]
    [DataRow(0.75f, 0.8535534f)]
    [DataRow(1.0f, 1.0f)]
    [DataRow(1.5f, 1.0f)]
    public void GetEasingSineReturnsClampedEaseInOut(float value, float expected)
    {
        Assert.AreEqual(expected, CubismMath.GetEasingSine(value), Tolerance);
    }

    /// <summary>
    /// Clamp 的整数限幅边界.
    /// </summary>
    /// <param name="value">被限制的值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="expected">期望值</param>
    [TestMethod]
    [DataRow(5, 0, 10, 5)]
    [DataRow(-1, 0, 10, 0)]
    [DataRow(11, 0, 10, 10)]
    [DataRow(0, 0, 10, 0)]
    [DataRow(10, 0, 10, 10)]
    [DataRow(7, 7, 7, 7)]
    [DataRow(-3, -5, -1, -3)]
    [DataRow(-9, -5, -1, -5)]
    public void ClampLimitsIntegerValue(int value, int min, int max, int expected)
    {
        Assert.AreEqual(expected, CubismMath.Clamp(value, min, max));
    }

    /// <summary>
    /// ClampF 的浮点限幅边界.
    /// </summary>
    /// <param name="value">被限制的值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="expected">期望值</param>
    [TestMethod]
    [DataRow(5.5f, 0.0f, 10.0f, 5.5f)]
    [DataRow(-0.5f, 0.0f, 10.0f, 0.0f)]
    [DataRow(10.5f, 0.0f, 10.0f, 10.0f)]
    [DataRow(0.0f, 0.0f, 10.0f, 0.0f)]
    [DataRow(10.0f, 0.0f, 10.0f, 10.0f)]
    [DataRow(-2.5f, -1.0f, 1.0f, -1.0f)]
    public void ClampFLimitsFloatingValue(float value, float min, float max, float expected)
    {
        Assert.AreEqual(expected, CubismMath.ClampF(value, min, max), 0.0f);
    }

    /// <summary>
    /// RangeF 的浮点限幅边界.
    /// </summary>
    /// <param name="value">被限制的值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="expected">期望值</param>
    [TestMethod]
    [DataRow(-1.0f, 0.0f, 1.0f, 0.0f)]
    [DataRow(2.0f, 0.0f, 1.0f, 1.0f)]
    [DataRow(0.5f, 0.0f, 1.0f, 0.5f)]
    [DataRow(1.0f, 0.0f, 1.0f, 1.0f)]
    public void RangeFLimitsFloatingValue(float value, float min, float max, float expected)
    {
        Assert.AreEqual(expected, CubismMath.RangeF(value, min, max), 0.0f);
    }

    /// <summary>
    /// Max 与 Min 的取值.
    /// </summary>
    /// <param name="left">左侧值</param>
    /// <param name="right">右侧值</param>
    /// <param name="expectedMax">期望的较大值</param>
    /// <param name="expectedMin">期望的较小值</param>
    [TestMethod]
    [DataRow(1.0f, 2.0f, 2.0f, 1.0f)]
    [DataRow(-1.0f, -2.0f, -1.0f, -2.0f)]
    [DataRow(3.0f, 3.0f, 3.0f, 3.0f)]
    public void MaxAndMinReturnBoundValues(float left, float right, float expectedMax, float expectedMin)
    {
        Assert.AreEqual(expectedMax, CubismMath.Max(left, right), 0.0f);
        Assert.AreEqual(expectedMin, CubismMath.Min(left, right), 0.0f);
    }

    /// <summary>
    /// 角度转弧度的黄金值.
    /// </summary>
    /// <param name="degrees">角度值</param>
    /// <param name="expected">期望的弧度值</param>
    [TestMethod]
    [DataRow(0.0f, 0.0f)]
    [DataRow(90.0f, 1.5707964f)]
    [DataRow(180.0f, 3.1415927f)]
    [DataRow(-45.0f, -0.7853982f)]
    [DataRow(360.0f, 6.2831855f)]
    public void DegreesToRadianGolden(float degrees, float expected)
    {
        Assert.AreEqual(expected, CubismMath.DegreesToRadian(degrees), Tolerance);
    }

    /// <summary>
    /// 弧度转角度的黄金值.
    /// </summary>
    /// <param name="radian">弧度值</param>
    /// <param name="expected">期望的角度值</param>
    [TestMethod]
    [DataRow(0.0f, 0.0f)]
    [DataRow(1.5707964f, 90.0f)]
    [DataRow(3.1415927f, 180.0f)]
    [DataRow(-0.7853982f, -45.0f)]
    public void RadianToDegreesGolden(float radian, float expected)
    {
        Assert.AreEqual(expected, CubismMath.RadianToDegrees(radian), 1e-4f);
    }

    /// <summary>
    /// DirectionToRadian 在两个方向向量之间的夹角.
    /// </summary>
    /// <param name="fromX">起点 X</param>
    /// <param name="fromY">起点 Y</param>
    /// <param name="toX">终点 X</param>
    /// <param name="toY">终点 Y</param>
    /// <param name="expected">期望的弧度值</param>
    [TestMethod]
    [DataRow(1.0f, 0.0f, 0.0f, 1.0f, 1.5707964f)]
    [DataRow(1.0f, 0.0f, -1.0f, 0.0f, 3.1415927f)]
    [DataRow(1.0f, 0.0f, 0.0f, -1.0f, -1.5707964f)]
    [DataRow(1.0f, 0.0f, 1.0f, 1.0f, 0.7853982f)]
    [DataRow(1.0f, 0.0f, 1.0f, 0.0f, 0.0f)]
    [DataRow(0.0f, 1.0f, 1.0f, 0.0f, -1.5707964f)]
    public void DirectionToRadianReturnsAngleBetweenVectors(float fromX, float fromY, float toX, float toY, float expected)
    {
        var from = new Vector2(fromX, fromY);
        var to = new Vector2(toX, toY);

        Assert.AreEqual(expected, CubismMath.DirectionToRadian(from, to), Tolerance);
    }

    /// <summary>
    /// DirectionToRadian 的折返处理: 结果必须落在 [-Pi, Pi] 内.
    /// </summary>
    [TestMethod]
    public void DirectionToRadianWrapsIntoPiRange()
    {
        // 两个方向都接近 -X 轴且分居两侧, 原始差值为 2*Pi 量级, 折返后应在 -0.0002 rad 附近.
        var from = new Vector2(-1.0f, -0.0001f);
        var to = new Vector2(-1.0f, 0.0001f);

        float radian = CubismMath.DirectionToRadian(from, to);

        Assert.AreEqual(-0.0002f, radian, 1e-4f);
        Assert.IsTrue(radian >= -CubismMath.Pi && radian <= CubismMath.Pi, "DirectionToRadian must return an angle within [-Pi, Pi].");
    }

    /// <summary>
    /// DirectionToDegrees 的黄金值: X 分量增大时角度取负.
    /// </summary>
    /// <remarks>
    /// 起点为原点时 atan2(0, 0) = 0, 因此从原点到正上方 (0, 1) 为 +90 度, 到正下方 (0, -1) 为 -90 度.
    /// 只有 (to.X - from.X) > 0 时角度才取负, 原点到正上方时 X 差为 0, 不触发取负.
    /// </remarks>
    /// <param name="fromX">起点 X</param>
    /// <param name="fromY">起点 Y</param>
    /// <param name="toX">终点 X</param>
    /// <param name="toY">终点 Y</param>
    /// <param name="expected">期望的角度值</param>
    [TestMethod]
    // 起点为原点时 atan2(0, 0) = 0, 故 (0, 0) -> (0, 1) 为 +90 度, (0, 0) -> (0, -1) 为 -90 度.
    [DataRow(0.0f, 0.0f, 1.0f, 1.0f, -45.0f)]
    [DataRow(0.0f, 0.0f, -1.0f, 1.0f, 135.0f)]
    [DataRow(0.0f, 0.0f, 0.0f, 1.0f, 90.0f)]
    [DataRow(0.0f, 0.0f, 0.0f, -1.0f, -90.0f)]
    [DataRow(0.0f, 0.0f, -1.0f, -1.0f, -135.0f)]
    public void DirectionToDegreesGolden(float fromX, float fromY, float toX, float toY, float expected)
    {
        var from = new Vector2(fromX, fromY);
        var to = new Vector2(toX, toY);

        Assert.AreEqual(expected, CubismMath.DirectionToDegrees(from, to), 1e-4f);
    }

    /// <summary>
    /// RadianToDirection 的黄金值: 返回 (sin, cos) 分量.
    /// </summary>
    /// <param name="radian">弧度值</param>
    /// <param name="expectedX">期望的 X 分量</param>
    /// <param name="expectedY">期望的 Y 分量</param>
    [TestMethod]
    [DataRow(0.0f, 0.0f, 1.0f)]
    [DataRow(1.5707964f, 1.0f, 0.0f)]
    [DataRow(3.1415927f, 0.0f, -1.0f)]
    [DataRow(-1.5707964f, -1.0f, 0.0f)]
    public void RadianToDirectionGolden(float radian, float expectedX, float expectedY)
    {
        Vector2 direction = CubismMath.RadianToDirection(radian);

        Assert.AreEqual(expectedX, direction.X, Tolerance);
        Assert.AreEqual(expectedY, direction.Y, Tolerance);
    }

    /// <summary>
    /// QuadraticEquation 的常规根与退化分支.
    /// </summary>
    /// <param name="a">二次项系数</param>
    /// <param name="b">一次项系数</param>
    /// <param name="c">常数项</param>
    /// <param name="expected">期望的根</param>
    [TestMethod]
    [DataRow(1.0f, -3.0f, 2.0f, 1.0f)]
    [DataRow(2.0f, -6.0f, 4.0f, 1.0f)]
    [DataRow(1.0f, 0.0f, -0.25f, -0.5f)]
    [DataRow(0.0f, 2.0f, -4.0f, 2.0f)]
    [DataRow(0.0f, 0.0f, -3.0f, 3.0f)]
    [DataRow(0.000001f, 1.0f, -0.5f, 0.5f)]
    public void QuadraticEquationReturnsExpectedRoot(float a, float b, float c, float expected)
    {
        Assert.AreEqual(expected, CubismMath.QuadraticEquation(a, b, c), 1e-5f);
    }

    /// <summary>
    /// 负判别式时 QuadraticEquation 返回 NaN, 而不是抛异常或给出虚假实根.
    /// </summary>
    [TestMethod]
    public void QuadraticEquationReturnsNaNForNegativeDiscriminant()
    {
        float root = CubismMath.QuadraticEquation(1.0f, 1.0f, 1.0f);

        Assert.IsTrue(float.IsNaN(root), "QuadraticEquation must return NaN when the discriminant is negative.");
    }

    /// <summary>
    /// CardanoAlgorithmForBezier 的退化, 单根, 重根与三实根分支, 结果必须落在 [0, 1].
    /// </summary>
    /// <param name="a">三次项系数</param>
    /// <param name="b">二次项系数</param>
    /// <param name="c">一次项系数</param>
    /// <param name="d">常数项</param>
    /// <param name="expected">期望的解</param>
    [TestMethod]
    [DataRow(0.0f, 0.0f, 1.0f, -0.5f, 0.5f)]
    [DataRow(0.0f, 0.0f, 0.0f, 1.0f, 0.0f)]
    [DataRow(0.0f, 0.0f, 1.0f, -2.0f, 1.0f)]
    [DataRow(1.0f, 0.0f, 0.0f, -0.125f, 0.5f)]
    [DataRow(1.0f, 0.0f, 0.0f, -8.0f, 1.0f)]
    [DataRow(1.0f, 0.0f, 0.0f, 8.0f, 0.0f)]
    [DataRow(1.0f, -1.5f, 0.75f, -0.125f, 0.5f)]
    [DataRow(1.0f, -1.5f, -1.5f, 1.0f, 0.5f)]
    public void CardanoAlgorithmForBezierPicksRootNearHalf(float a, float b, float c, float d, float expected)
    {
        float root = CubismMath.CardanoAlgorithmForBezier(a, b, c, d);

        Assert.AreEqual(expected, root, 1e-4f);
        Assert.IsTrue(root >= 0.0f && root <= 1.0f, $"CardanoAlgorithmForBezier must clamp its result into [0, 1], got {root}.");
    }

    /// <summary>
    /// ModF 的余数符号与被除数一致.
    /// </summary>
    /// <param name="dividend">被除数</param>
    /// <param name="divisor">除数</param>
    /// <param name="expected">期望的余数</param>
    [TestMethod]
    [DataRow(5.5f, 2.0f, 1.5f)]
    [DataRow(-5.5f, 2.0f, -1.5f)]
    [DataRow(4.0f, 2.0f, 0.0f)]
    [DataRow(-4.0f, 2.0f, 0.0f)]
    [DataRow(7.25f, 0.5f, 0.25f)]
    [DataRow(1.5f, 1.0f, 0.5f)]
    [DataRow(-1.5f, 1.0f, -0.5f)]
    [DataRow(0.0f, 3.0f, 0.0f)]
    [DataRow(5.5f, -2.0f, 1.5f)]
    public void ModFReturnsRemainderSignedLikeDividend(float dividend, float divisor, float expected)
    {
        Assert.AreEqual(expected, CubismMath.ModF(dividend, divisor), Tolerance);
    }

    /// <summary>
    /// 非有限输入或除数为 0 时 ModF 返回 NaN.
    /// </summary>
    /// <param name="dividend">被除数</param>
    /// <param name="divisor">除数</param>
    [TestMethod]
    [DataRow(float.NaN, 2.0f)]
    [DataRow(float.PositiveInfinity, 2.0f)]
    [DataRow(float.NegativeInfinity, 2.0f)]
    [DataRow(5.0f, 0.0f)]
    [DataRow(5.0f, -0.0f)]
    [DataRow(5.0f, float.NaN)]
    public void ModFReturnsNaNForInvalidInput(float dividend, float divisor)
    {
        // 实现会在该分支写一条 Warning 日志; 日志输出未显式设置时会回退到 Core 的日志回调,
        // 这里临时屏蔽输出, 保证 A 档不触碰 Core, 结束后在 finally 里恢复原值.
        var previousOutput = CubismContext.Default.LogOutput;

        try
        {
            CubismContext.Default.LogOutput = _ => { };

            float remainder = CubismMath.ModF(dividend, divisor);

            Assert.IsTrue(float.IsNaN(remainder), "ModF must return NaN for non-finite input or a zero divisor.");
        }
        finally
        {
            CubismContext.Default.LogOutput = previousOutput;
        }
    }
}
