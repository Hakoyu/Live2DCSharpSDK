using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;
using Live2DCSharpSDK.Tests.Id;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Core;

/// <summary>
/// B 档: <see cref="CubismMaskGroupUtility.CountDrawableMaskGroups"/> 的 Core 适配层.
///
/// A 档只测内部的纯逻辑入口 (CountUniqueMaskGroups), 这条公开入口需要真实 moc3 才能走到;
/// 这里用 Haru / Ren 两个夹具验证适配层, 并断言与历史渲染门禁记录的分组数一致
/// (run-smoke 的 masks 列: Haru 3 组, Ren 4 组).
/// 用例只解析 moc3 与读取遮罩数据, 不创建 renderer, 不调用任何绘制 API.
/// </summary>
[TestClass]
[TestCategory("RequiresCore")]
public sealed class CubismMaskGroupCoreTests
{
    /// <summary>
    /// 公开入口按真实模型的遮罩数据分组, 组数与渲染门禁记录一致;
    /// 结果必须落在 1 到 drawable 数量之间.
    /// </summary>
    /// <param name="relativePath">相对夹具根目录的 moc3 路径</param>
    /// <param name="expectedGroups">渲染门禁记录的唯一遮罩组数</param>
    [TestMethod]
    [DataRow(ModelFixtures.HaruRelativePath, 3)]
    [DataRow(ModelFixtures.RenRelativePath, 4)]
    public void CountDrawableMaskGroups_MatchesRecordedGroupCount(string relativePath, int expectedGroups)
    {
        ModelFixtureEntry fixture = ModelFixtures.Require(relativePath);
        byte[] mocBytes = ModelFixtures.ReadBytes(fixture);

        TrackingAllocator allocator = new();
        CubismContext context = CoreTestEnvironment.CreateContext(allocator);
        try
        {
            using CubismMoc moc = new(context, mocBytes);
            CubismModel model = moc.Model;

            int groups = CubismMaskGroupUtility.CountDrawableMaskGroups(model);

            Assert.AreEqual(expectedGroups, groups,
                $"The unique mask group count must match the recorded render gate value for '{relativePath}'.");
            Assert.IsTrue(groups > 0,
                $"A model that ships mask data must report at least one group for '{relativePath}'.");
            Assert.IsTrue(groups <= model.GetDrawableCount(),
                $"The group count cannot exceed the drawable count for '{relativePath}'.");
        }
        finally
        {
            context.Dispose();
        }

        Assert.AreEqual(0, allocator.LiveBlockCount,
            $"Reading the mask groups must not leak native blocks for '{relativePath}'.");
        Assert.AreEqual(0, allocator.DoubleFreeCount,
            $"Reading the mask groups must not free a block twice for '{relativePath}'.");
    }
}
