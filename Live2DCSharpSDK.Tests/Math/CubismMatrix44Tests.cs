using Live2DCSharpSDK.Framework.Math;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Math;

/// <summary>
/// CubismMatrix44 的纯托管单元测试 (A2).
///
/// 覆盖内容: 单位矩阵, 元素拷贝, 乘法, GetInvert 的可逆/奇异契约,
/// 以及平移与缩放黄金矩阵的往返一致; 不加载 Core, 不触碰真实 GL.
/// </summary>
/// <remarks>
/// 矩阵按行主序存放, 平移量在 Tr[12] / Tr[13], 属于行向量约定 (v' = v * M).
/// MultiplyByMatrix(a) 的语义是 this = a * this, 所以两个矩阵相乘统一走 <see cref="Multiply"/>.
/// </remarks>
[TestClass]
public sealed class CubismMatrix44Tests
{
    /// <summary>
    /// 逆矩阵往返的浮点容限.
    /// </summary>
    private const float InvertTolerance = 1e-5f;

    /// <summary>
    /// 黄金矩阵比对的浮点容限.
    /// </summary>
    private const float GoldenTolerance = 1e-6f;

    /// <summary>
    /// 新建矩阵必须是单位矩阵.
    /// </summary>
    [TestMethod]
    public void NewMatrixIsIdentity()
    {
        AssertIdentity(new CubismMatrix44(), 0.0f, "A new matrix must be the identity matrix.");
    }

    /// <summary>
    /// SetMatrix 逐元素拷贝 16 个值, 且不与源数组共享存储.
    /// </summary>
    [TestMethod]
    public void SetMatrixCopiesAllSixteenElements()
    {
        float[] values =
        [
            1.0f, 2.0f, 3.0f, 4.0f,
            5.0f, 6.0f, 7.0f, 8.0f,
            9.0f, 10.0f, 11.0f, 12.0f,
            13.0f, 14.0f, 15.0f, 16.0f
        ];
        var matrix = new CubismMatrix44();

        matrix.SetMatrix(values);

        AssertElementsEqual(matrix, values, 0.0f, "SetMatrix must copy all 16 elements.");

        // 源数组后续改动不应影响矩阵, 证明内部是拷贝而不是引用.
        values[0] = 100.0f;
        Assert.AreEqual(1.0f, matrix.Tr[0], 0.0f);
    }

    /// <summary>
    /// SetMatrix 传入不足 16 个元素时抛 ArgumentException, 且矩阵保持原状.
    /// </summary>
    [TestMethod]
    public void SetMatrixRejectsShortArrayWithoutChangingState()
    {
        var matrix = new CubismMatrix44();

        // Array.Copy 会先校验长度再拷贝, 因此失败后矩阵仍是单位矩阵.
        Assert.ThrowsExactly<ArgumentException>(() => matrix.SetMatrix(new float[8]));

        AssertIdentity(matrix, 0.0f, "A rejected SetMatrix must leave the matrix unchanged.");
    }

    /// <summary>
    /// SetMatrix 传入 null 数组时抛 ArgumentNullException.
    /// </summary>
    [TestMethod]
    public void SetMatrixRejectsNullArray()
    {
        var matrix = new CubismMatrix44();

        // 显式转换用于选中 float[] 重载.
        Assert.ThrowsExactly<ArgumentNullException>(() => matrix.SetMatrix((float[])null!));
    }

    /// <summary>
    /// 缩放, 平移与正逆变换的黄金值.
    /// </summary>
    [TestMethod]
    public void ScaleTranslateAndTransformGoldenValues()
    {
        var matrix = new CubismMatrix44();
        matrix.Scale(2.0f, 4.0f);
        matrix.Translate(10.0f, 20.0f);

        Assert.AreEqual(2.0f, matrix.GetScaleX(), 0.0f);
        Assert.AreEqual(4.0f, matrix.GetScaleY(), 0.0f);
        Assert.AreEqual(10.0f, matrix.GetTranslateX(), 0.0f);
        Assert.AreEqual(20.0f, matrix.GetTranslateY(), 0.0f);

        // 行向量约定: x' = scaleX * x + translateX, y' = scaleY * y + translateY.
        Assert.AreEqual(16.0f, matrix.TransformX(3.0f), 0.0f);
        Assert.AreEqual(40.0f, matrix.TransformY(5.0f), 0.0f);
        Assert.AreEqual(3.0f, matrix.InvertTransformX(16.0f), 0.0f);
        Assert.AreEqual(5.0f, matrix.InvertTransformY(40.0f), 0.0f);
    }

    /// <summary>
    /// 平移加缩放矩阵的逆矩阵黄金值, 以及点坐标往返.
    /// </summary>
    [TestMethod]
    public void GetInvertOfScaleAndTranslateMatchesGoldenMatrix()
    {
        var matrix = new CubismMatrix44();
        matrix.Scale(2.0f, 4.0f);
        matrix.Translate(10.0f, 20.0f);

        var invert = matrix.GetInvert();

        float[] expected =
        [
            0.5f, 0.0f, 0.0f, 0.0f,
            0.0f, 0.25f, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f,
            -5.0f, -5.0f, 0.0f, 1.0f
        ];
        AssertElementsEqual(invert, expected, GoldenTolerance, "The inverse of a scale/translate matrix is wrong.");

        // 正变换后再逆变换应回到原坐标.
        Assert.AreEqual(3.0f, invert.TransformX(matrix.TransformX(3.0f)), GoldenTolerance);
        Assert.AreEqual(5.0f, invert.TransformY(matrix.TransformY(5.0f)), GoldenTolerance);
    }

    /// <summary>
    /// 平移与缩放矩阵相乘的黄金值 (行向量约定下 T * S 会把平移量乘以缩放).
    /// </summary>
    [TestMethod]
    public void MultiplyByMatrixGoldenTranslationThenScale()
    {
        var translate = new CubismMatrix44();
        translate.Translate(10.0f, 20.0f);
        var scale = new CubismMatrix44();
        scale.Scale(2.0f, 4.0f);

        var product = Multiply(translate, scale);

        float[] expected =
        [
            2.0f, 0.0f, 0.0f, 0.0f,
            0.0f, 4.0f, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f,
            20.0f, 80.0f, 0.0f, 1.0f
        ];
        AssertElementsEqual(product, expected, 0.0f, "The translation-then-scale golden matrix is wrong.");
    }

    /// <summary>
    /// 可逆矩阵与其逆矩阵相乘约等于单位矩阵, 两个方向都要检查.
    /// </summary>
    [TestMethod]
    public void MultiplyByInvertYieldsIdentity()
    {
        var matrix = CreateRotationWithTranslation();
        var invert = matrix.GetInvert();

        // 约定: Multiply(left, right) = left * right, 这里验证 M * M^-1.
        var forward = Multiply(matrix, invert);
        AssertIdentity(forward, InvertTolerance, "M * M^-1 must be the identity matrix.");

        // 反方向 M^-1 * M 也应是单位矩阵, 数值误差可略大但仍须在容限内.
        var backward = Multiply(invert, matrix);
        AssertIdentity(backward, InvertTolerance, "M^-1 * M must be the identity matrix.");
    }

    /// <summary>
    /// 调用 GetInvert 不应修改原矩阵的任何一个元素.
    /// </summary>
    [TestMethod]
    public void GetInvertDoesNotMutateSourceMatrix()
    {
        var matrix = CreateRotationWithTranslation();
        float[] before = [.. matrix.Tr];

        _ = matrix.GetInvert();

        AssertElementsEqual(matrix, before, 0.0f, "GetInvert must not modify the source matrix.");
    }

    /// <summary>
    /// 奇异矩阵的 GetInvert 返回独立单位矩阵对象: 不是原矩阵, 不是 null, 也不与原矩阵共享存储.
    /// </summary>
    [TestMethod]
    public void GetInvertOfSingularMatrixReturnsIndependentIdentity()
    {
        // 缩放 X 为 0 使行列式为 0, 属于奇异矩阵.
        var matrix = new CubismMatrix44();
        matrix.Scale(0.0f, 2.0f);
        matrix.Translate(3.0f, 4.0f);

        var invert = matrix.GetInvert();

        Assert.IsNotNull(invert, "The inverse of a singular matrix must not be null.");
        Assert.AreNotSame(matrix, invert, "The inverse of a singular matrix must be a new object.");
        AssertIdentity(invert, 0.0f, "The inverse of a singular matrix must be the identity matrix.");

        // 每次调用都应返回新对象, 且与原矩阵不共享内部数组.
        var second = matrix.GetInvert();
        Assert.AreNotSame(invert, second, "Each GetInvert call must return a new object.");

        invert.Tr[0] = 42.0f;
        Assert.AreEqual(0.0f, matrix.Tr[0], 0.0f);
        Assert.AreEqual(1.0f, second.Tr[0], 0.0f);
    }

    /// <summary>
    /// 全零矩阵同样是奇异矩阵, GetInvert 返回单位矩阵.
    /// </summary>
    [TestMethod]
    public void GetInvertOfZeroMatrixReturnsIdentity()
    {
        var matrix = new CubismMatrix44();
        matrix.SetMatrix(new float[16]);

        var invert = matrix.GetInvert();

        AssertIdentity(invert, 0.0f, "The inverse of a zero matrix must be the identity matrix.");
    }

    /// <summary>
    /// ScaleRelative 连续调用会把缩放率累乘.
    /// </summary>
    [TestMethod]
    public void ScaleRelativeMultipliesScale()
    {
        var matrix = new CubismMatrix44();

        matrix.ScaleRelative(2.0f, 3.0f);
        Assert.AreEqual(2.0f, matrix.GetScaleX(), 0.0f);
        Assert.AreEqual(3.0f, matrix.GetScaleY(), 0.0f);

        matrix.ScaleRelative(2.0f, 2.0f);
        Assert.AreEqual(4.0f, matrix.GetScaleX(), 0.0f);
        Assert.AreEqual(6.0f, matrix.GetScaleY(), 0.0f);
    }

    /// <summary>
    /// TranslateRelative 连续调用会把平移量累加.
    /// </summary>
    [TestMethod]
    public void TranslateRelativeAccumulatesTranslation()
    {
        var matrix = new CubismMatrix44();

        matrix.TranslateRelative(1.0f, 2.0f);
        Assert.AreEqual(1.0f, matrix.GetTranslateX(), 0.0f);
        Assert.AreEqual(2.0f, matrix.GetTranslateY(), 0.0f);

        matrix.TranslateRelative(3.0f, 4.0f);
        Assert.AreEqual(4.0f, matrix.GetTranslateX(), 0.0f);
        Assert.AreEqual(6.0f, matrix.GetTranslateY(), 0.0f);
    }

    /// <summary>
    /// TranslateX / TranslateY 只改写平移分量, 不改变缩放与旋转分量.
    /// </summary>
    [TestMethod]
    public void TranslateAxisSettersOnlyChangeTranslation()
    {
        var matrix = new CubismMatrix44();

        matrix.TranslateX(7.0f);
        matrix.TranslateY(-3.0f);

        Assert.AreEqual(7.0f, matrix.GetTranslateX(), 0.0f);
        Assert.AreEqual(-3.0f, matrix.GetTranslateY(), 0.0f);
        Assert.AreEqual(1.0f, matrix.GetScaleX(), 0.0f);
        Assert.AreEqual(1.0f, matrix.GetScaleY(), 0.0f);
        Assert.AreEqual(0.0f, matrix.Tr[1], 0.0f);
    }

    /// <summary>
    /// 计算左矩阵乘以右矩阵.
    /// MultiplyByMatrix 的语义是 result = 参数矩阵 * 当前矩阵,
    /// 因此先复制右矩阵, 再把左矩阵乘进去.
    /// </summary>
    /// <param name="left">左矩阵</param>
    /// <param name="right">右矩阵</param>
    /// <returns>left * right</returns>
    private static CubismMatrix44 Multiply(CubismMatrix44 left, CubismMatrix44 right)
    {
        var result = new CubismMatrix44();
        result.SetMatrix(right);
        result.MultiplyByMatrix(left);
        return result;
    }

    /// <summary>
    /// 构造 30 度旋转加平移 (5, -3) 的矩阵.
    /// 行向量约定: 第一行为 (cos, sin), 第二行为 (-sin, cos).
    /// </summary>
    /// <returns>旋转加平移矩阵</returns>
    private static CubismMatrix44 CreateRotationWithTranslation()
    {
        float angle = System.MathF.PI / 6.0f;
        float cos = System.MathF.Cos(angle);
        float sin = System.MathF.Sin(angle);

        var matrix = new CubismMatrix44();
        float[] values =
        [
            cos, sin, 0.0f, 0.0f,
            -sin, cos, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f,
            5.0f, -3.0f, 0.0f, 1.0f
        ];
        matrix.SetMatrix(values);
        return matrix;
    }

    /// <summary>
    /// 断言 16 个元素与期望值逐项相等.
    /// </summary>
    /// <param name="matrix">被测矩阵</param>
    /// <param name="expected">期望的 16 个元素</param>
    /// <param name="delta">浮点容限</param>
    /// <param name="because">失败说明</param>
    private static void AssertElementsEqual(CubismMatrix44 matrix, float[] expected, float delta, string because)
    {
        for (int i = 0; i < 16; i++)
        {
            Assert.AreEqual(expected[i], matrix.Tr[i], delta, $"element {i}: {because}");
        }
    }

    /// <summary>
    /// 断言矩阵是单位矩阵.
    /// </summary>
    /// <param name="matrix">被测矩阵</param>
    /// <param name="delta">浮点容限</param>
    /// <param name="because">失败说明</param>
    private static void AssertIdentity(CubismMatrix44 matrix, float delta, string because)
    {
        for (int i = 0; i < 16; i++)
        {
            float expected = i % 5 == 0 ? 1.0f : 0.0f;
            Assert.AreEqual(expected, matrix.Tr[i], delta, $"element {i}: {because}");
        }
    }
}
