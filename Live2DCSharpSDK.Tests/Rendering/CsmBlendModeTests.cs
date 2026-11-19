using Live2DCSharpSDK.Framework.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Rendering;

/// <summary>
/// A5: CsmBlendMode 的编解码与字符串表.
///
/// 期望字符串按官方 csmBlendMode.cpp 的表逐项写在用例里, 不复用产品实现的数组,
/// 避免 "表写错了测试也跟着错".
/// </summary>
[TestClass]
public sealed class CsmBlendModeTests
{
    private static readonly string[] ExpectedColorNames =
    [
        "Normal",
        "AddCompatible",
        "MultiplyCompatible",
        "Add",
        "AddGlow",
        "Darken",
        "Multiply",
        "ColorBurn",
        "LinearBurn",
        "Lighten",
        "Screen",
        "ColorDodge",
        "Overlay",
        "SoftLight",
        "HardLight",
        "LinearLight",
        "Hue",
        "Color",
    ];

    private static readonly string[] ExpectedAlphaNames =
    [
        "Over",
        "Atop",
        "Out",
        "ConjointOver",
        "DisjointOver",
    ];

    /// <summary>
    /// 颜色混合种类 0..17 逐项返回官方字符串.
    /// </summary>
    /// <param name="type">颜色混合种类</param>
    /// <param name="expected">期望字符串</param>
    [TestMethod]
    [DataRow(0, "Normal")]
    [DataRow(1, "AddCompatible")]
    [DataRow(2, "MultiplyCompatible")]
    [DataRow(3, "Add")]
    [DataRow(4, "AddGlow")]
    [DataRow(5, "Darken")]
    [DataRow(6, "Multiply")]
    [DataRow(7, "ColorBurn")]
    [DataRow(8, "LinearBurn")]
    [DataRow(9, "Lighten")]
    [DataRow(10, "Screen")]
    [DataRow(11, "ColorDodge")]
    [DataRow(12, "Overlay")]
    [DataRow(13, "SoftLight")]
    [DataRow(14, "HardLight")]
    [DataRow(15, "LinearLight")]
    [DataRow(16, "Hue")]
    [DataRow(17, "Color")]
    public void ColorBlendModeToStringReturnsOfficialTable(int type, string expected)
    {
        Assert.AreEqual(expected, CsmBlendMode.ColorBlendModeToString(type));
    }

    /// <summary>
    /// Alpha 混合种类 0..4 逐项返回官方字符串.
    /// </summary>
    /// <param name="type">Alpha 混合种类</param>
    /// <param name="expected">期望字符串</param>
    [TestMethod]
    [DataRow(0, "Over")]
    [DataRow(1, "Atop")]
    [DataRow(2, "Out")]
    [DataRow(3, "ConjointOver")]
    [DataRow(4, "DisjointOver")]
    public void AlphaBlendModeToStringReturnsOfficialTable(int type, string expected)
    {
        Assert.AreEqual(expected, CsmBlendMode.AlphaBlendModeToString(type));
    }

    /// <summary>
    /// 未知的颜色混合种类返回空字符串.
    /// </summary>
    /// <param name="type">越界的颜色混合种类</param>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(18)]
    [DataRow(19)]
    [DataRow(255)]
    [DataRow(int.MaxValue)]
    public void UnknownColorBlendTypeReturnsEmptyString(int type)
    {
        Assert.AreEqual(string.Empty, CsmBlendMode.ColorBlendModeToString(type));
    }

    /// <summary>
    /// 未知的 Alpha 混合种类返回空字符串.
    /// </summary>
    /// <param name="type">越界的 Alpha 混合种类</param>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(255)]
    [DataRow(int.MaxValue)]
    public void UnknownAlphaBlendTypeReturnsEmptyString(int type)
    {
        Assert.AreEqual(string.Empty, CsmBlendMode.AlphaBlendModeToString(type));
    }

    /// <summary>
    /// 默认构造为 Normal / Over.
    /// </summary>
    [TestMethod]
    public void DefaultInstanceIsNormalOver()
    {
        var blendMode = new CsmBlendMode();

        Assert.AreEqual(0, blendMode.GetColorBlendType());
        Assert.AreEqual(0, blendMode.GetAlphaBlendType());
        Assert.AreEqual("Normal", blendMode.GetColorBlendTypeString());
        Assert.AreEqual("Over", blendMode.GetAlphaBlendTypeString());
    }

    /// <summary>
    /// 结构体的默认值同样落在 Normal / Over (0 与 0).
    /// </summary>
    [TestMethod]
    public void DefaultStructValueIsNormalOver()
    {
        CsmBlendMode blendMode = default;

        Assert.AreEqual("Normal", blendMode.GetColorBlendTypeString());
        Assert.AreEqual("Over", blendMode.GetAlphaBlendTypeString());
    }

    /// <summary>
    /// 从 blendMode 值解码: 低 8 bit 是颜色, 接下来的 8 bit 是 Alpha.
    /// </summary>
    [TestMethod]
    public void DecodesColorAndAlphaFromBlendModeValue()
    {
        var blendMode = new CsmBlendMode(0x0302);

        Assert.AreEqual(2, blendMode.GetColorBlendType());
        Assert.AreEqual(3, blendMode.GetAlphaBlendType());
        Assert.AreEqual("MultiplyCompatible", blendMode.GetColorBlendTypeString());
        Assert.AreEqual("ConjointOver", blendMode.GetAlphaBlendTypeString());
    }

    /// <summary>
    /// 颜色 0..17 与 Alpha 0..4 的全组合编码后再解码都是原值, 字符串与官方表一致.
    /// </summary>
    [TestMethod]
    public void EncodeDecodeRoundTripsForAllKnownValues()
    {
        for (int color = 0; color < ExpectedColorNames.Length; color++)
        {
            for (int alpha = 0; alpha < ExpectedAlphaNames.Length; alpha++)
            {
                int value = (alpha << 8) | color;
                var blendMode = new CsmBlendMode(value);

                Assert.AreEqual(color, blendMode.GetColorBlendType());
                Assert.AreEqual(alpha, blendMode.GetAlphaBlendType());
                Assert.AreEqual(ExpectedColorNames[color], blendMode.GetColorBlendTypeString());
                Assert.AreEqual(ExpectedAlphaNames[alpha], blendMode.GetAlphaBlendTypeString());
            }
        }
    }

    /// <summary>
    /// SetBlendMode 覆盖已有实例的两个种类, 未知值同样退化为空字符串.
    /// </summary>
    [TestMethod]
    public void SetBlendModeOverwritesBothTypes()
    {
        var blendMode = new CsmBlendMode();
        blendMode.SetBlendMode(0x0A06);

        Assert.AreEqual(6, blendMode.GetColorBlendType());
        Assert.AreEqual("Multiply", blendMode.GetColorBlendTypeString());
        Assert.AreEqual(10, blendMode.GetAlphaBlendType());
        Assert.AreEqual(string.Empty, blendMode.GetAlphaBlendTypeString());
    }

    /// <summary>
    /// 负数 blendMode 解码为 255 / 255, 字符串退化为空.
    /// </summary>
    [TestMethod]
    public void NegativeBlendModeValueDecodesToUnknownTypes()
    {
        var blendMode = new CsmBlendMode(-1);

        Assert.AreEqual(255, blendMode.GetColorBlendType());
        Assert.AreEqual(255, blendMode.GetAlphaBlendType());
        Assert.AreEqual(string.Empty, blendMode.GetColorBlendTypeString());
        Assert.AreEqual(string.Empty, blendMode.GetAlphaBlendTypeString());
    }
}
