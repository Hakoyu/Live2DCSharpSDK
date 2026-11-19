using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Tests.Id;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Core;

/// <summary>
/// B1: moc3 夹具的指纹与 <see cref="CubismMoc"/> 的解析契约.
///
/// 只在 Windows x64 运行, 需要 Live2DCubismCore.dll 与 LIVE2D_TEST_MODELS 指向的样例模型;
/// 夹具缺失或哈希不符时用例失败而不是跳过 (见 PLAN-06 §2 / §3).
/// 用例只解析 moc3 与读取模型元数据, 不创建 renderer, 不调用任何绘制 API.
/// </summary>
[TestClass]
[TestCategory("RequiresCore")]
public sealed class CubismMocCoreTests
{
    /// <summary>
    /// Haru / Ren 的 SHA-256, MocVersion 与各项计数与 TestData/models.sha256 一致,
    /// 且静态版本查询与实例解析得到同一个版本.
    /// </summary>
    /// <param name="relativePath">相对夹具根目录的 moc3 路径</param>
    [TestMethod]
    [DataRow(ModelFixtures.HaruRelativePath)]
    [DataRow(ModelFixtures.RenRelativePath)]
    public void Moc3Fixture_MatchesRecordedFingerprint(string relativePath)
    {
        ModelFixtureEntry fixture = ModelFixtures.Require(relativePath);
        byte[] mocBytes = ModelFixtures.ReadBytes(fixture);

        TrackingAllocator allocator = new();
        CubismContext context = CoreTestEnvironment.CreateContext(allocator);
        try
        {
            Assert.AreEqual(
                fixture.MocVersion,
                CubismMoc.GetMocVersionFromBuffer(context, mocBytes),
                $"GetMocVersionFromBuffer must report the recorded version for '{relativePath}'."
            );
            Assert.IsTrue(
                CubismMoc.HasMocConsistencyFromUnrevivedMoc(context, mocBytes),
                $"HasMocConsistencyFromUnrevivedMoc must accept the recorded fixture '{relativePath}'."
            );

            using (CubismMoc moc = new(context, mocBytes))
            {
                Assert.AreEqual(
                    fixture.MocVersion,
                    moc.MocVersion,
                    $"MocVersion must match the recorded value for '{relativePath}'."
                );
                Assert.AreSame(
                    context,
                    moc.Model.Context,
                    "The model must be bound to the context that revived the moc."
                );

                CubismModel model = moc.Model;
                Assert.AreNotEqual(
                    IntPtr.Zero,
                    model.Model,
                    "The revived model handle must not be null."
                );
                Assert.AreEqual(
                    fixture.DrawableCount,
                    model.GetDrawableCount(),
                    $"DrawableCount must match the recorded value for '{relativePath}'."
                );
                Assert.AreEqual(
                    fixture.PartCount,
                    model.GetPartCount(),
                    $"PartCount must match the recorded value for '{relativePath}'."
                );
                Assert.AreEqual(
                    fixture.ParameterCount,
                    model.GetParameterCount(),
                    $"ParameterCount must match the recorded value for '{relativePath}'."
                );
                Assert.AreEqual(
                    fixture.OffscreenCount,
                    model.GetOffscreenCount(),
                    $"OffscreenCount must match the recorded value for '{relativePath}'."
                );
                Assert.AreEqual(
                    fixture.ParameterCount,
                    model.ParameterIds.Count,
                    "Every parameter id must be registered on the model."
                );
                Assert.AreEqual(
                    fixture.PartCount,
                    model.PartIds.Count,
                    "Every part id must be registered on the model."
                );
                Assert.AreEqual(
                    fixture.DrawableCount,
                    model.DrawableIds.Count,
                    "Every drawable id must be registered on the model."
                );
                Assert.AreEqual(
                    2,
                    allocator.LiveBlockCount,
                    "Reviving a moc must keep exactly the moc buffer and the model memory alive."
                );

                foreach (string drawableId in model.DrawableIds)
                {
                    Assert.IsFalse(
                        string.IsNullOrEmpty(drawableId),
                        "Drawable ids must not be empty."
                    );
                }
            }

            Assert.AreEqual(
                0,
                allocator.LiveBlockCount,
                $"Disposing the moc must return every block to the allocator for '{relativePath}'."
            );
            Assert.AreEqual(
                0,
                allocator.DoubleFreeCount,
                $"Disposing the moc must not free a block twice for '{relativePath}'."
            );
        }
        finally
        {
            context.Dispose();
        }
    }

    /// <summary>
    /// 两个上下文各自解析自己的 moc: 模型绑定各自的 context, ID 管理器互不共享,
    /// 分配器也只看到自己的分配与释放.
    /// </summary>
    [TestMethod]
    public void TwoContexts_KeepTheirOwnMocAndIds()
    {
        TrackingAllocator haruAllocator = new();
        TrackingAllocator renAllocator = new();
        CubismContext haruContext = CoreTestEnvironment.CreateContext(haruAllocator);
        CubismContext renContext = CoreTestEnvironment.CreateContext(renAllocator);
        try
        {
            ModelFixtureEntry haruFixture = ModelFixtures.Require(ModelFixtures.HaruRelativePath);
            ModelFixtureEntry renFixture = ModelFixtures.Require(ModelFixtures.RenRelativePath);
            byte[] haruBytes = ModelFixtures.ReadBytes(haruFixture);
            byte[] renBytes = ModelFixtures.ReadBytes(renFixture);

            using (CubismMoc haruMoc = new(haruContext, haruBytes))
            using (CubismMoc renMoc = new(renContext, renBytes))
            {
                Assert.AreSame(
                    haruContext,
                    haruMoc.Model.Context,
                    "The Haru model must stay bound to the context that revived it."
                );
                Assert.AreSame(
                    renContext,
                    renMoc.Model.Context,
                    "The Ren model must stay bound to the context that revived it."
                );
                Assert.AreNotSame(
                    haruContext.IdManager,
                    renContext.IdManager,
                    "Each context must own its own id manager."
                );

                CubismModel haruModel = haruMoc.Model;
                CubismModel renModel = renMoc.Model;
                Assert.AreEqual(
                    haruFixture.DrawableCount,
                    haruModel.GetDrawableCount(),
                    "Haru must keep its own drawable count."
                );
                Assert.AreEqual(
                    renFixture.DrawableCount,
                    renModel.GetDrawableCount(),
                    "Ren must keep its own drawable count."
                );

                // 模型初始化会把参数 / 部件 / Drawable 的 id 登记到所属上下文的 IdManager;
                // 两个上下文各自只应看到自己模型的 id (数量与内容都相等, 因此不会有另一个模型的 id).
                string[] haruExpectedIds =
                [
                    .. haruModel.ParameterIds,
                    .. haruModel.PartIds,
                    .. haruModel.DrawableIds,
                ];
                string[] renExpectedIds =
                [
                    .. renModel.ParameterIds,
                    .. renModel.PartIds,
                    .. renModel.DrawableIds,
                ];
                CollectionAssert.AreEquivalent(
                    haruExpectedIds.Distinct().ToArray(),
                    CubismIdManagerProbe.GetRegisteredIds(haruContext.IdManager).ToArray(),
                    "The Haru context must register exactly the ids of the Haru model."
                );
                CollectionAssert.AreEquivalent(
                    renExpectedIds.Distinct().ToArray(),
                    CubismIdManagerProbe.GetRegisteredIds(renContext.IdManager).ToArray(),
                    "The Ren context must register exactly the ids of the Ren model."
                );
                Assert.AreEqual(
                    haruExpectedIds.Distinct().Count(),
                    CubismIdManagerProbe.GetRegisteredCount(haruContext.IdManager),
                    "The Haru context must not contain ids from another model."
                );
                Assert.AreEqual(
                    renExpectedIds.Distinct().Count(),
                    CubismIdManagerProbe.GetRegisteredCount(renContext.IdManager),
                    "The Ren context must not contain ids from another model."
                );

                Assert.AreEqual(
                    2,
                    haruAllocator.LiveBlockCount,
                    "The Haru allocator must own two live blocks."
                );
                Assert.AreEqual(
                    2,
                    renAllocator.LiveBlockCount,
                    "The Ren allocator must own two live blocks."
                );
            }

            Assert.AreEqual(
                0,
                haruAllocator.LiveBlockCount,
                "The Haru allocator must receive both frees."
            );
            Assert.AreEqual(
                0,
                renAllocator.LiveBlockCount,
                "The Ren allocator must receive both frees."
            );
            Assert.AreEqual(
                0,
                haruAllocator.DoubleFreeCount,
                "The Haru allocator must not see a double free."
            );
            Assert.AreEqual(
                0,
                renAllocator.DoubleFreeCount,
                "The Ren allocator must not see a double free."
            );
        }
        finally
        {
            haruContext.Dispose();
            renContext.Dispose();
        }
    }
}
