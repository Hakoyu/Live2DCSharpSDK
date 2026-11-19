using Live2DCSharpSDK.Framework.Id;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Id;

/// <summary>
/// <see cref="CubismIdManager"/> 的登记去重行为.
/// 这组用例只操作纯托管数据, 不加载 Core, 因此属于 A 档.
/// </summary>
[TestClass]
public sealed class CubismIdManagerTests
{
    /// <summary>
    /// 同一个值重复 GetId 只登记一次, 且返回值与传入值相等.
    /// 当前实现返回传入的 item, 所以这里只断言值相等, 不断言引用相等.
    /// </summary>
    [TestMethod]
    public void GetId_RepeatedSameValue_RegistersOnce()
    {
        CubismIdManager manager = new();

        string first = manager.GetId("ParamAngleX");
        string second = manager.GetId("ParamAngleX");
        string third = manager.GetId("ParamAngleX");

        Assert.AreEqual("ParamAngleX", first);
        Assert.AreEqual("ParamAngleX", second);
        Assert.AreEqual("ParamAngleX", third);
        Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(manager));
        CollectionAssert.AreEqual(new[] { "ParamAngleX" }, CubismIdManagerProbe.GetRegisteredIds(manager).ToArray());
    }

    /// <summary>
    /// 大量重复 GetId 之后登记数量仍为 1.
    /// </summary>
    [TestMethod]
    public void GetId_ManyRepetitions_RegistersOnce()
    {
        CubismIdManager manager = new();

        for (int i = 0; i < 1000; i++)
        {
            manager.GetId("ParamEyeLOpen");
        }

        Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(manager));
        CollectionAssert.AreEqual(new[] { "ParamEyeLOpen" }, CubismIdManagerProbe.GetRegisteredIds(manager).ToArray());
    }

    /// <summary>
    /// 两个不同实例但值相同的字符串只登记一次, 证明去重按值而不是按引用.
    /// </summary>
    [TestMethod]
    public void GetId_EqualValuesFromDifferentInstances_RegisterOnce()
    {
        // 运行期构造两个独立实例, 避免字面量驻留让本用例退化成引用去重.
        string first = new('A', 8);
        string second = new string(first.ToCharArray());
        Assert.AreNotSame(first, second, "The two inputs must be distinct string instances.");

        CubismIdManager manager = new();
        manager.GetId(first);
        manager.GetId(second);

        Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(manager));
        CollectionAssert.AreEqual(new[] { first }, CubismIdManagerProbe.GetRegisteredIds(manager).ToArray());
    }

    /// <summary>
    /// 两个管理器实例的登记表互不共享.
    /// </summary>
    [TestMethod]
    public void GetId_TwoInstances_DoNotShareRegistrations()
    {
        CubismIdManager first = new();
        CubismIdManager second = new();

        first.GetId("ParamAngleX");

        Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(first));
        Assert.AreEqual(0, CubismIdManagerProbe.GetRegisteredCount(second));

        second.GetId("ParamAngleY");

        Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(first));
        Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(second));
        CollectionAssert.AreEqual(new[] { "ParamAngleX" }, CubismIdManagerProbe.GetRegisteredIds(first).ToArray());
        CollectionAssert.AreEqual(new[] { "ParamAngleY" }, CubismIdManagerProbe.GetRegisteredIds(second).ToArray());
    }

    /// <summary>
    /// RegisterIds 对列表中的重复值同样只登记一次.
    /// </summary>
    [TestMethod]
    public void RegisterIds_RepeatedValues_RegisterOnce()
    {
        CubismIdManager manager = new();

        manager.RegisterIds(["ParamAngleX", "ParamAngleX", "ParamAngleZ"]);

        Assert.AreEqual(2, CubismIdManagerProbe.GetRegisteredCount(manager));
        CollectionAssert.AreEqual(
            new[] { "ParamAngleX", "ParamAngleZ" },
            CubismIdManagerProbe.GetRegisteredIds(manager).ToArray());
    }

    /// <summary>
    /// 空串可以正常登记, 不抛异常.
    /// </summary>
    [TestMethod]
    public void GetId_EmptyString_DoesNotThrowAndRegisters()
    {
        CubismIdManager manager = new();

        string result = manager.GetId(string.Empty);

        Assert.AreEqual(string.Empty, result);
        Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(manager));
        Assert.AreEqual(string.Empty, manager.GetId(string.Empty));
        Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(manager));
    }

    /// <summary>
    /// 超长字符串可以正常登记, 不抛异常.
    /// </summary>
    [TestMethod]
    public void GetId_LongString_DoesNotThrowAndRegisters()
    {
        CubismIdManager manager = new();
        string longId = new('a', 8192);

        string result = manager.GetId(longId);

        Assert.AreEqual(longId, result);
        Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(manager));
        manager.GetId(longId);
        Assert.AreEqual(1, CubismIdManagerProbe.GetRegisteredCount(manager));
    }
}
