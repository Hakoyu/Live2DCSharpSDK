using Live2DCSharpSDK.Framework.Math;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Math;

/// <summary>
/// CubismTargetPoint 的纯托管单元测试 (A3).
///
/// 覆盖内容: 目标设置, 趋近方向, 不越过目标, 零增量不改变状态,
/// 以及收敛后的残差; 不加载 Core, 不触碰真实 GL.
/// 实现没有 clamp, 因此不断言 clamp 行为.
/// </summary>
[TestClass]
public sealed class CubismTargetPointTests
{
    /// <summary>
    /// 单帧增量, 与实现内置的 30 FPS 一致.
    /// </summary>
    private const float FrameDeltaTime = 1.0f / 30.0f;

    /// <summary>
    /// 收敛用例的帧数, 实测 14 帧后进入静止, 这里留足余量.
    /// </summary>
    private const int ConvergenceFrames = 300;

    /// <summary>
    /// 目标为 1 时的收敛值, 由 float 逐帧递推实测得到.
    /// </summary>
    private const float ConvergedFaceValue = 0.9980649f;

    /// <summary>
    /// 常量契约: 帧率与提前返回阈值.
    /// </summary>
    [TestMethod]
    public void ConstantsMatchContract()
    {
        Assert.AreEqual(30, CubismTargetPoint.FrameRate);
        Assert.AreEqual(0.01f, CubismTargetPoint.Epsilon, 0.0f);
    }

    /// <summary>
    /// Set 只保存目标值, 不立即改变当前朝向.
    /// </summary>
    [TestMethod]
    public void SetOnlyStoresTargetAndKeepsFaceAtOrigin()
    {
        var point = new CubismTargetPoint();

        point.Set(1.0f, 1.0f);

        Assert.AreEqual(0.0f, point.FaceX, 0.0f);
        Assert.AreEqual(0.0f, point.FaceY, 0.0f);
    }

    /// <summary>
    /// 第一次 Update 只记录时间基准, 不产生位移.
    /// </summary>
    [TestMethod]
    public void FirstUpdateOnlyEstablishesTimeBase()
    {
        var point = new CubismTargetPoint();
        point.Set(1.0f, 1.0f);

        point.Update(FrameDeltaTime);

        Assert.AreEqual(0.0f, point.FaceX, 0.0f);
        Assert.AreEqual(0.0f, point.FaceY, 0.0f);
    }

    /// <summary>
    /// 连续以 1/30 秒更新时朝向单调趋向目标, 且不越过目标.
    /// </summary>
    [TestMethod]
    public void UpdateApproachesTargetWithoutOvershoot()
    {
        var point = new CubismTargetPoint();
        point.Set(1.0f, 1.0f);

        // 首帧只记录时间基准, 从第二帧开始才会位移.
        point.Update(FrameDeltaTime);

        float previousX = point.FaceX;
        float previousY = point.FaceY;

        for (int frame = 0; frame < ConvergenceFrames; frame++)
        {
            point.Update(FrameDeltaTime);

            Assert.IsTrue(point.FaceX >= previousX, $"FaceX must not decrease at frame {frame}: {previousX} -> {point.FaceX}.");
            Assert.IsTrue(point.FaceY >= previousY, $"FaceY must not decrease at frame {frame}: {previousY} -> {point.FaceY}.");
            Assert.IsTrue(point.FaceX <= 1.0f, $"FaceX must not overshoot the target at frame {frame}: {point.FaceX}.");
            Assert.IsTrue(point.FaceY <= 1.0f, $"FaceY must not overshoot the target at frame {frame}: {point.FaceY}.");

            previousX = point.FaceX;
            previousY = point.FaceY;
        }

        // 至少要有明显位移, 否则上面的单调断言没有意义.
        Assert.IsTrue(point.FaceX > 0.9f, $"FaceX must approach the target, got {point.FaceX}.");
    }

    /// <summary>
    /// 位移发生前, 零增量更新不改变朝向.
    /// </summary>
    [TestMethod]
    public void UpdateWithZeroDeltaKeepsFaceBeforeMotion()
    {
        var point = new CubismTargetPoint();
        point.Set(1.0f, 1.0f);

        point.Update(0.0f);
        point.Update(0.0f);

        Assert.AreEqual(0.0f, point.FaceX, 0.0f);
        Assert.AreEqual(0.0f, point.FaceY, 0.0f);
    }

    /// <summary>
    /// 位移过程中, 零增量更新不改变朝向.
    /// </summary>
    [TestMethod]
    public void UpdateWithZeroDeltaKeepsFaceMidMotion()
    {
        var point = new CubismTargetPoint();
        point.Set(1.0f, 1.0f);
        Advance(point, 6);

        float faceX = point.FaceX;
        float faceY = point.FaceY;

        Assert.IsTrue(faceX > 0.0f, $"Face must have moved before the zero-delta update, got {faceX}.");

        point.Update(0.0f);

        Assert.AreEqual(faceX, point.FaceX, 0.0f);
        Assert.AreEqual(faceY, point.FaceY, 0.0f);
    }

    /// <summary>
    /// 目标为 1 时收敛到 1 附近.
    /// </summary>
    /// <remarks>
    /// 实现以 Epsilon (0.01) 作为提前返回阈值, 实测收敛值 0.9980649,
    /// 与 1 的残差约 1.94e-3, 因此这里用 1e-3 的黄金值容限加 Epsilon 邻域双重断言.
    /// </remarks>
    [TestMethod]
    public void ConvergesToTargetOne()
    {
        var point = new CubismTargetPoint();
        point.Set(1.0f, 1.0f);

        Advance(point, ConvergenceFrames);

        Assert.AreEqual(ConvergedFaceValue, point.FaceX, 1e-3f);
        Assert.AreEqual(ConvergedFaceValue, point.FaceY, 1e-3f);
        Assert.IsTrue(System.MathF.Abs(point.FaceX - 1.0f) <= CubismTargetPoint.Epsilon, $"FaceX must settle within Epsilon of the target, got {point.FaceX}.");
        Assert.IsTrue(System.MathF.Abs(point.FaceY - 1.0f) <= CubismTargetPoint.Epsilon, $"FaceY must settle within Epsilon of the target, got {point.FaceY}.");
    }

    /// <summary>
    /// 目标为 -1 时收敛到 -1 附近, 且不越过目标.
    /// </summary>
    [TestMethod]
    public void NegativeTargetConvergesSymmetrically()
    {
        var point = new CubismTargetPoint();
        point.Set(-1.0f, -1.0f);

        Advance(point, ConvergenceFrames);

        Assert.AreEqual(-ConvergedFaceValue, point.FaceX, 1e-3f);
        Assert.AreEqual(-ConvergedFaceValue, point.FaceY, 1e-3f);
        Assert.IsTrue(point.FaceX >= -1.0f, $"FaceX must not overshoot the negative target, got {point.FaceX}.");
        Assert.IsTrue(point.FaceY >= -1.0f, $"FaceY must not overshoot the negative target, got {point.FaceY}.");
    }

    /// <summary>
    /// 中间目标 (0.5) 的收敛结果落在 Epsilon 邻域内.
    /// </summary>
    /// <remarks>
    /// 实测末步会略微越过 0.5 (0.5012870), 因此这里只断言残差在 Epsilon 内,
    /// 不断言不越过目标.
    /// </remarks>
    [TestMethod]
    public void IntermediateTargetStaysWithinEpsilonOfTarget()
    {
        var point = new CubismTargetPoint();
        point.Set(0.5f, 0.5f);

        Advance(point, ConvergenceFrames);

        Assert.IsTrue(System.MathF.Abs(point.FaceX - 0.5f) <= CubismTargetPoint.Epsilon, $"FaceX must stay within Epsilon of the target, got {point.FaceX}.");
        Assert.IsTrue(System.MathF.Abs(point.FaceY - 0.5f) <= CubismTargetPoint.Epsilon, $"FaceY must stay within Epsilon of the target, got {point.FaceY}.");
    }

    /// <summary>
    /// 以固定帧增量推进指定帧数.
    /// </summary>
    /// <param name="point">被测实例</param>
    /// <param name="frames">帧数</param>
    private static void Advance(CubismTargetPoint point, int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            point.Update(FrameDeltaTime);
        }
    }
}
