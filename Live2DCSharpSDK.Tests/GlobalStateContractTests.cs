using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests;

/// <summary>
/// 全局状态夹具的契约测试.
///
/// 重点验证分档边界: A 档 (纯托管) 不允许触碰原生库,
/// 因此 <see cref="GlobalStateFixture.CoreLibraryPath"/> 必须为 null,
/// 且不得通过 <c>NativeLibrary.TryLoad</c> 加载过 Core.
/// </summary>
[TestClass]
public class GlobalStateContractTests
{
    /// <summary>
    /// A 档不得探测原生 Core 库.
    /// 非 A 档直接返回 (不使用 Inconclusive, 避免把用例算成跳过).
    /// </summary>
    [TestMethod]
    public void ATier_DoesNotProbeCoreLibrary()
    {
        if (
            string.Equals(GlobalStateFixture.Tier, "a", StringComparison.OrdinalIgnoreCase) is false
        )
        {
            return;
        }

        Assert.IsNull(GlobalStateFixture.CoreLibraryPath, "A 档不应解析 Core 库路径.");
        Assert.IsFalse(GlobalStateFixture.IsCoreLibraryAvailable, "A 档不应加载 Core 库.");
    }
}
