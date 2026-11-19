using System.Numerics;
using System.Reflection;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Core;

/// <summary>
/// B2: <see cref="CubismModel"/> 的释放幂等性 (迁移自 probe-v1).
///
/// 修复前 <see cref="CubismModel.Dispose"/> 没有幂等保护, 第二次调用会对同一块 model 内存再次
/// DeallocateAligned, 也就是 double-free; 修复后 Dispose 只释放一次, 并把 <see cref="CubismModel.Model"/>
/// 置为 <see cref="IntPtr.Zero"/>, 之后的访问器全部走守卫返回安全值.
/// 用例用跟踪分配器把重复释放变成可断言的计数, 避免测试宿主堆损坏.
/// 只解析 moc3 与读写模型元数据, 不创建 renderer, 不调用任何绘制 API.
/// </summary>
[TestClass]
[TestCategory("RequiresCore")]
public sealed class CubismModelDisposeCoreTests
{
    /// <summary>
    /// 同一个 <see cref="CubismModel"/> 连续 Dispose 三次不崩, 且不会重复释放 model 内存.
    /// </summary>
    /// <param name="relativePath">相对夹具根目录的 moc3 路径</param>
    [TestMethod]
    [DataRow(ModelFixtures.HaruRelativePath)]
    [DataRow(ModelFixtures.RenRelativePath)]
    public void DisposeThreeTimes_DoesNotFreeModelMemoryTwice(string relativePath)
    {
        ModelFixtureEntry fixture = ModelFixtures.Require(relativePath);
        TrackingAllocator allocator = new();
        CubismContext context = CoreTestEnvironment.CreateContext(allocator);
        CubismMoc? moc = null;
        try
        {
            moc = new CubismMoc(context, ModelFixtures.ReadBytes(fixture));
            CubismModel model = moc.Model;
            Assert.AreNotEqual(
                IntPtr.Zero,
                model.Model,
                "The revived model handle must not be null."
            );
            Assert.AreEqual(
                fixture.DrawableCount,
                model.GetDrawableCount(),
                $"The live model must report the recorded drawable count for '{relativePath}'."
            );

            for (int round = 1; round <= 3; round++)
            {
                // 修复前: 第 2 次 Dispose 会对同一块 model 内存再次 DeallocateAligned (double-free).
                model.Dispose();

                Assert.AreEqual(
                    IntPtr.Zero,
                    model.Model,
                    $"Dispose #{round} must reset the model handle to zero for '{relativePath}'."
                );
                Assert.AreEqual(
                    0,
                    allocator.DoubleFreeCount,
                    $"Dispose #{round} must not free the model memory twice for '{relativePath}'."
                );

                // 此时只应剩下 moc 缓冲区 (moc 尚未 Dispose).
                Assert.AreEqual(
                    1,
                    allocator.LiveBlockCount,
                    $"Dispose #{round} must release exactly the model memory for '{relativePath}'."
                );
            }

            moc.Dispose();
            moc.Dispose();

            Assert.AreEqual(
                0,
                allocator.DoubleFreeCount,
                $"Disposing the moc twice must not free the moc buffer twice for '{relativePath}'."
            );
            Assert.AreEqual(
                0,
                allocator.LiveBlockCount,
                $"Disposing the moc must release the moc buffer for '{relativePath}'."
            );
        }
        finally
        {
            moc?.Dispose();
            context.Dispose();
        }
    }

    /// <summary>
    /// Dispose 之后所有读访问器返回安全值, 写访问器与子对象只是 no-op, 不触碰已释放的 Core 内存.
    /// </summary>
    /// <param name="relativePath">相对夹具根目录的 moc3 路径</param>
    [TestMethod]
    [DataRow(ModelFixtures.HaruRelativePath)]
    [DataRow(ModelFixtures.RenRelativePath)]
    public void AfterDispose_AccessorsReturnSafeValues(string relativePath)
    {
        ModelFixtureEntry fixture = ModelFixtures.Require(relativePath);
        TrackingAllocator allocator = new();
        CubismContext context = CoreTestEnvironment.CreateContext(allocator);
        CubismMoc? moc = null;
        try
        {
            moc = new CubismMoc(context, ModelFixtures.ReadBytes(fixture));
            CubismModel model = moc.Model;
            string drawableId = model.GetDrawableId(0);

            model.Dispose();

            AssertSafeAccessors(model, fixture, drawableId);
            Assert.AreEqual(
                0,
                allocator.DoubleFreeCount,
                $"Disposing the model must not free any block twice for '{relativePath}'."
            );
            Assert.AreEqual(
                1,
                allocator.LiveBlockCount,
                $"Only the moc buffer may stay alive after disposing the model for '{relativePath}'."
            );
        }
        finally
        {
            moc?.Dispose();
            context.Dispose();
        }
    }

    /// <summary>
    /// 逐项校验已 Dispose 模型的读访问器安全值与写访问器的 no-op 行为.
    /// 期望值来自 probe-v1 的实测基线 (修复后行为).
    /// </summary>
    /// <param name="model">已 Dispose 的模型</param>
    /// <param name="fixture">夹具元数据, 仅用于错误消息</param>
    /// <param name="drawableId">Dispose 前记录的第一个 Drawable ID</param>
    private static void AssertSafeAccessors(
        CubismModel model,
        ModelFixtureEntry fixture,
        string drawableId
    )
    {
        string label = fixture.RelativePath;
        Assert.AreEqual(
            IntPtr.Zero,
            model.Model,
            $"The disposed model handle must be zero for '{label}'."
        );

        Assert.AreEqual(
            0,
            model.GetDrawableCount(),
            $"GetDrawableCount must be safe for '{label}'."
        );
        Assert.AreEqual(0, model.GetPartCount(), $"GetPartCount must be safe for '{label}'.");
        Assert.AreEqual(
            0,
            model.GetParameterCount(),
            $"GetParameterCount must be safe for '{label}'."
        );
        Assert.AreEqual(
            0,
            model.GetOffscreenCount(),
            $"GetOffscreenCount must be safe for '{label}'."
        );
        Assert.AreEqual(
            0.0f,
            model.GetCanvasWidthPixel(),
            $"GetCanvasWidthPixel must be safe for '{label}'."
        );
        Assert.AreEqual(
            0.0f,
            model.GetCanvasHeightPixel(),
            $"GetCanvasHeightPixel must be safe for '{label}'."
        );
        Assert.AreEqual(
            0.0f,
            model.GetPixelsPerUnit(),
            $"GetPixelsPerUnit must be safe for '{label}'."
        );
        Assert.AreEqual(
            0.0f,
            model.GetCanvasWidth(),
            $"GetCanvasWidth must be safe for '{label}'."
        );
        Assert.AreEqual(
            0.0f,
            model.GetCanvasHeight(),
            $"GetCanvasHeight must be safe for '{label}'."
        );

        Assert.AreEqual(
            0,
            model.GetDrawableVertexCount(0),
            $"GetDrawableVertexCount must be safe for '{label}'."
        );
        Assert.AreEqual(
            0,
            model.GetDrawableVertexIndexCount(0),
            $"GetDrawableVertexIndexCount must be safe for '{label}'."
        );
        Assert.AreEqual(
            0,
            model.GetDrawableTextureIndex(0),
            $"GetDrawableTextureIndex must be safe for '{label}'."
        );
        Assert.AreEqual(
            0.0f,
            model.GetDrawableOpacity(0),
            $"GetDrawableOpacity must be safe for '{label}'."
        );
        Assert.AreEqual(
            default(Vector4),
            model.GetDrawableMultiplyColor(0),
            $"GetDrawableMultiplyColor must be safe for '{label}'."
        );
        Assert.AreEqual(
            default(Vector4),
            model.GetDrawableScreenColor(0),
            $"GetDrawableScreenColor must be safe for '{label}'."
        );
        Assert.AreEqual(
            CubismModel.CubismNoIndex_Parent,
            model.GetDrawableParentPartIndex(0),
            $"GetDrawableParentPartIndex must be safe for '{label}'."
        );
        Assert.AreEqual(
            0,
            model.GetDrawableCulling(0),
            $"GetDrawableCulling must be safe for '{label}'."
        );
        Assert.AreEqual(
            0,
            model.GetOffscreenCulling(0),
            $"GetOffscreenCulling must be safe for '{label}'."
        );
        Assert.IsFalse(
            model.GetDrawableInvertedMask(0),
            $"GetDrawableInvertedMask must be safe for '{label}'."
        );
        Assert.IsFalse(
            model.GetDrawableDynamicFlagIsVisible(0),
            $"GetDrawableDynamicFlagIsVisible must be safe for '{label}'."
        );
        Assert.IsFalse(
            model.GetDrawableDynamicFlagVisibilityDidChange(0),
            $"GetDrawableDynamicFlagVisibilityDidChange must be safe for '{label}'."
        );
        Assert.IsFalse(
            model.GetDrawableDynamicFlagOpacityDidChange(0),
            $"GetDrawableDynamicFlagOpacityDidChange must be safe for '{label}'."
        );
        Assert.IsFalse(
            model.GetDrawableDynamicFlagDrawOrderDidChange(0),
            $"GetDrawableDynamicFlagDrawOrderDidChange must be safe for '{label}'."
        );
        Assert.IsFalse(
            model.GetDrawableDynamicFlagRenderOrderDidChange(0),
            $"GetDrawableDynamicFlagRenderOrderDidChange must be safe for '{label}'."
        );
        Assert.IsFalse(
            model.GetDrawableDynamicFlagVertexPositionsDidChange(0),
            $"GetDrawableDynamicFlagVertexPositionsDidChange must be safe for '{label}'."
        );
        Assert.IsFalse(
            model.GetDrawableDynamicFlagBlendColorDidChange(0),
            $"GetDrawableDynamicFlagBlendColorDidChange must be safe for '{label}'."
        );
        Assert.AreEqual(
            1.0f,
            model.GetOffscreenOpacity(0),
            $"GetOffscreenOpacity must be safe for '{label}'."
        );
        Assert.AreEqual(
            string.Empty,
            model.GetOffscreenOwnerId(0),
            $"GetOffscreenOwnerId must be safe for '{label}'."
        );
        Assert.AreEqual(
            default(Vector4),
            model.GetOffscreenMultiplyColor(0),
            $"GetOffscreenMultiplyColor must be safe for '{label}'."
        );
        Assert.AreEqual(
            default(Vector4),
            model.GetOffscreenScreenColor(0),
            $"GetOffscreenScreenColor must be safe for '{label}'."
        );
        Assert.AreEqual(
            0,
            model.GetDrawableBlendModeType(0).GetColorBlendType(),
            $"GetDrawableBlendModeType must be safe for '{label}'."
        );
        Assert.AreEqual(
            0,
            model.GetOffscreenBlendModeType(0).GetColorBlendType(),
            $"GetOffscreenBlendModeType must be safe for '{label}'."
        );

        Assert.AreEqual(
            -1,
            model.GetPartIndex("NoSuchPartV1"),
            $"GetPartIndex must be safe for '{label}'."
        );
        Assert.AreEqual(
            -1,
            model.GetParameterIndex("NoSuchParameterV1"),
            $"GetParameterIndex must be safe for '{label}'."
        );
        Assert.AreEqual(
            0.0f,
            model.GetPartOpacity(0),
            $"GetPartOpacity must be safe for '{label}'."
        );
        Assert.AreEqual(
            0.0f,
            model.GetParameterValue(0),
            $"GetParameterValue must be safe for '{label}'."
        );
        Assert.AreEqual(
            0,
            model.GetParameterType(0),
            $"GetParameterType must be safe for '{label}'."
        );
        Assert.AreEqual(
            0.0f,
            model.GetParameterMaximumValue(0),
            $"GetParameterMaximumValue must be safe for '{label}'."
        );
        Assert.AreEqual(
            0.0f,
            model.GetParameterMinimumValue(0),
            $"GetParameterMinimumValue must be safe for '{label}'."
        );
        Assert.AreEqual(
            0.0f,
            model.GetParameterDefaultValue(0),
            $"GetParameterDefaultValue must be safe for '{label}'."
        );
        Assert.IsFalse(model.IsRepeat(0), $"IsRepeat must be safe for '{label}'.");
        Assert.IsFalse(
            model.GetParameterRepeats(0),
            $"GetParameterRepeats must be safe for '{label}'."
        );
        Assert.AreEqual(
            3.0f,
            model.GetParameterRepeatValue(0, 3.0f),
            $"GetParameterRepeatValue must be safe for '{label}'."
        );
        Assert.AreEqual(
            3.0f,
            model.GetParameterClampValue(0, 3.0f),
            $"GetParameterClampValue must be safe for '{label}'."
        );
        Assert.IsFalse(model.IsUsingMasking(), $"IsUsingMasking must be safe for '{label}'.");
        Assert.IsFalse(
            model.IsUsingMaskingForOffscreen(),
            $"IsUsingMaskingForOffscreen must be safe for '{label}'."
        );
        Assert.AreEqual(
            CubismModel.CubismNoIndex_Parent,
            model.GetPartParentPartIndex(0),
            $"GetPartParentPartIndex must be safe for '{label}'."
        );

        // 指针返回值无法在未启用 unsafe 的测试工程里直接比较; 这些访问器同样有 Model 守卫,
        // 这里至少要求调用不抛异常 (守卫缺失时会读取已释放的 Core 内存).
        InvokePointerAccessors(model);

        // 写访问器与子对象在 Dispose 后必须是安全 no-op, 不能写回已释放的 Core 内存.
        model.SetPartOpacity(0, 1.0f);
        model.SetParameterValue(0, 1.0f);
        model.AddParameterValue(0, 1.0f);
        model.MultiplyParameterValue(0, 1.0f);
        model.SaveParameters();
        model.LoadParameters();
        model.Update();
        model.SetModelOpacity(0.5f);
        model.GetPartChildDrawObjects(0);

        Assert.AreEqual(
            IntPtr.Zero,
            model.Model,
            $"The write accessors must not revive the disposed model for '{label}'."
        );
        Assert.AreEqual(
            0.5f,
            model.GetModelOpacity(),
            $"SetModelOpacity must only change managed state after Dispose for '{label}'."
        );

        CubismModelMultiplyAndScreenColor colors = model.GetOverrideMultiplyAndScreenColor();
        CubismTextureColor fallbackColor = colors.GetDrawableMultiplyColor(0);
        Assert.AreEqual(
            1.0f,
            fallbackColor.R,
            $"The multiply color helper must fall back to white after Dispose for '{label}'."
        );
        Assert.AreEqual(
            1.0f,
            fallbackColor.G,
            $"The multiply color helper must fall back to white after Dispose for '{label}'."
        );
        Assert.AreEqual(
            1.0f,
            fallbackColor.B,
            $"The multiply color helper must fall back to white after Dispose for '{label}'."
        );
        Assert.AreEqual(
            1.0f,
            fallbackColor.A,
            $"The multiply color helper must fall back to white after Dispose for '{label}'."
        );

        colors.SetPartMultiplyColor(0, 1.0f, 1.0f, 1.0f, 1.0f);
        colors.SetPartMultiplyColorEnabled(0, true);
        colors.SetDrawableScreenColor(0, 0.0f, 0.0f, 0.0f, 1.0f);
        colors.SetOffscreenMultiplyColor(0, 1.0f, 1.0f, 1.0f, 1.0f);
        Assert.AreEqual(
            1.0f,
            colors.GetDrawableMultiplyColor(0).A,
            $"The multiply color write accessors must be no-ops after Dispose for '{label}'."
        );
        Assert.AreEqual(
            IntPtr.Zero,
            model.Model,
            $"The multiply color helpers must not revive the disposed model for '{label}'."
        );

        // Drawable ID 来自托管列表, Dispose 之后仍然可以查询, 且不会触碰 Core.
        Assert.IsFalse(
            string.IsNullOrEmpty(drawableId),
            "The recorded drawable id must not be empty."
        );
        Assert.AreEqual(
            0,
            model.GetDrawableIndex(drawableId),
            $"GetDrawableIndex must read the managed id list after Dispose for '{label}'."
        );
    }

    /// <summary>
    /// 通过反射调用返回指针的访问器, 只校验不抛异常.
    /// </summary>
    /// <param name="model">已 Dispose 的模型</param>
    private static void InvokePointerAccessors(CubismModel model)
    {
        InvokePointerAccessor(model, nameof(CubismModel.GetDrawableVertices), 0);
        InvokePointerAccessor(model, nameof(CubismModel.GetDrawableVertexPositions), 0);
        InvokePointerAccessor(model, nameof(CubismModel.GetDrawableVertexUvs), 0);
        InvokePointerAccessor(model, nameof(CubismModel.GetDrawableVertexIndices), 0);
        InvokePointerAccessor(model, nameof(CubismModel.GetRenderOrders));
        InvokePointerAccessor(model, nameof(CubismModel.GetDrawableMasks));
        InvokePointerAccessor(model, nameof(CubismModel.GetDrawableMaskCounts));
        InvokePointerAccessor(model, nameof(CubismModel.GetPartParentPartIndices));
        InvokePointerAccessor(model, nameof(CubismModel.GetPartOffscreenIndices));
        InvokePointerAccessor(model, nameof(CubismModel.GetOffscreenOwnerIndices));
        InvokePointerAccessor(model, nameof(CubismModel.GetOffscreenMasks));
        InvokePointerAccessor(model, nameof(CubismModel.GetOffscreenMaskCounts));
    }

    /// <summary>
    /// 通过反射调用一个返回指针的访问器.
    /// </summary>
    /// <param name="model">已 Dispose 的模型</param>
    /// <param name="methodName">访问器名称</param>
    /// <param name="arguments">访问器参数</param>
    private static void InvokePointerAccessor(
        CubismModel model,
        string methodName,
        params object?[] arguments
    )
    {
        MethodInfo method = typeof(CubismModel).GetMethod(methodName)
            ?? throw new InvalidOperationException($"CubismModel.{methodName} does not exist.");
        Assert.IsTrue(
            method.ReturnType.IsPointer,
            $"CubismModel.{methodName} is expected to return a pointer."
        );

        _ = method.Invoke(model, arguments);
    }
}
