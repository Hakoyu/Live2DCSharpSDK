using Live2DCSharpSDK.Framework.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Rendering;

/// <summary>
/// A4: CubismMaskGroupUtility 的纯逻辑入口 CountUniqueMaskGroups.
///
/// 该入口只读托管视图 (每个 drawable 的掩码个数 + 按顺序拼接的掩码 ID), 不触碰 Core, 因此属于 A 档.
/// 文件末尾的 CountUniqueMaskGroupsLegacy 逐行复刻重构前的指针实现, 用于守护 "重构不改分组结果".
/// </summary>
[TestClass]
public sealed class CubismMaskGroupUtilityTests
{
    /// <summary>
    /// 没有任何 drawable 时返回 0 组.
    /// </summary>
    [TestMethod]
    public void EmptyInputReturnsZero()
    {
        Assert.AreEqual(0, CubismMaskGroupUtility.CountUniqueMaskGroups([], []));
    }

    /// <summary>
    /// 掩码个数全为 -1 或 0 (表示该 drawable 没有掩码) 时返回 0 组.
    /// </summary>
    [TestMethod]
    public void AllMaskCountsNegativeOrZeroReturnsZero()
    {
        int[] maskCounts = [-1, -1, 0, -5, 0];

        Assert.AreEqual(0, CubismMaskGroupUtility.CountUniqueMaskGroups(maskCounts, []));
    }

    /// <summary>
    /// 多个 drawable 共用同一组掩码时只算 1 组.
    /// </summary>
    [TestMethod]
    public void SingleGroupIsCountedOnce()
    {
        int[] maskCounts = [2, 2, 2];
        int[] maskIds = [1, 2, 1, 2, 1, 2];

        Assert.AreEqual(1, CubismMaskGroupUtility.CountUniqueMaskGroups(maskCounts, maskIds));
    }

    /// <summary>
    /// 与官方 FindSameClip 一致: 组内元素顺序不同仍算同一组.
    /// </summary>
    [TestMethod]
    public void SameMaskIdsInDifferentOrderFormOneGroup()
    {
        int[] maskCounts = [2, 2];
        int[] maskIds = [1, 2, 2, 1];

        Assert.AreEqual(1, CubismMaskGroupUtility.CountUniqueMaskGroups(maskCounts, maskIds));
    }

    /// <summary>
    /// 元素个数不同的掩码互不相同: [1, 2] 与 [1, 2, 3] 算 2 组.
    /// </summary>
    [TestMethod]
    public void DifferentMaskLengthsAreDistinctGroups()
    {
        int[] maskCounts = [2, 2, 3];
        int[] maskIds = [1, 2, 2, 1, 1, 2, 3];

        Assert.AreEqual(2, CubismMaskGroupUtility.CountUniqueMaskGroups(maskCounts, maskIds));
    }

    /// <summary>
    /// 跨组复用: [1, 2] 被 3 个 drawable 引用, [4, 5] 被 2 个引用, [1, 2, 3] 被 1 个引用, 共 3 组.
    /// </summary>
    [TestMethod]
    public void CrossGroupReuseIsDeduplicated()
    {
        int[] maskCounts = [2, 2, 2, 2, 2, 3];
        int[] maskIds = [1, 2, 1, 2, 1, 2, 4, 5, 4, 5, 1, 2, 3];

        Assert.AreEqual(3, CubismMaskGroupUtility.CountUniqueMaskGroups(maskCounts, maskIds));
    }

    /// <summary>
    /// 掩码个数为 -1 或 0 的 drawable 夹在有效组之间时被忽略, 且不消耗掩码 ID.
    /// </summary>
    [TestMethod]
    public void NegativeCountsBetweenGroupsAreIgnored()
    {
        int[] maskCounts = [2, -1, 0, 2, -7, 3];
        int[] maskIds = [1, 2, 1, 2, 5, 6, 7];

        Assert.AreEqual(2, CubismMaskGroupUtility.CountUniqueMaskGroups(maskCounts, maskIds));
    }

    /// <summary>
    /// 掩码 ID 为负数时按值比较, 不做特殊处理.
    /// </summary>
    [TestMethod]
    public void NegativeMaskIdsAreComparedAsValues()
    {
        int[] maskCounts = [1, 1, 2];
        int[] maskIds = [-1, -1, -1, -1];

        Assert.AreEqual(2, CubismMaskGroupUtility.CountUniqueMaskGroups(maskCounts, maskIds));
    }

    /// <summary>
    /// 49 个互不相同的 2 元素组 (大肥鱼 49 组历史结论的规模) 返回 49.
    /// 每个组都挂在 2 个 drawable 上, 复刻实际模型里同一组被多个 drawable 复用的形态.
    /// </summary>
    [TestMethod]
    public void FortyNineDistinctGroupsReturnsFortyNine()
    {
        List<int> maskCounts = [];
        List<int> maskIds = [];

        for (int group = 0; group < 49; group++)
        {
            for (int repeat = 0; repeat < 2; repeat++)
            {
                maskCounts.Add(2);
                maskIds.Add(group);
                maskIds.Add(100 + group);
            }
        }

        Assert.AreEqual(49, CubismMaskGroupUtility.CountUniqueMaskGroups(maskCounts.ToArray(), maskIds.ToArray()));
    }

    /// <summary>
    /// 单个 drawable 的掩码数上限 16 可用: 16 个 ID 的组算 1 组, 换序仍是同一组, 改 1 个 ID 才是新组.
    /// </summary>
    [TestMethod]
    public void MaxMaskCountSixteenIsSupported()
    {
        int[] maskCounts = [16, 16, 16];
        List<int> maskIds = [];

        for (int id = 0; id < 16; id++)
        {
            maskIds.Add(id);
        }

        for (int id = 15; id >= 0; id--)
        {
            maskIds.Add(id);
        }

        for (int id = 0; id < 15; id++)
        {
            maskIds.Add(id);
        }

        maskIds.Add(99);

        Assert.AreEqual(2, CubismMaskGroupUtility.CountUniqueMaskGroups(maskCounts, maskIds.ToArray()));
    }

    /// <summary>
    /// 掩码 ID 数量不足时抛出明确的参数异常 (入口约定被破坏).
    /// </summary>
    [TestMethod]
    public void TooShortMaskIdsThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CubismMaskGroupUtility.CountUniqueMaskGroups([2], [7]));
    }

    /// <summary>
    /// 200 组确定性随机输入下与重构前的指针实现逐例对照.
    /// 取值域刻意压小 (0..7), 以便覆盖重复 ID, 顺序差异与 [1, 1] 这类退化组.
    /// </summary>
    [TestMethod]
    public void MatchesLegacyImplementationOnRandomInputs()
    {
        var random = new Random(20251119);

        for (int round = 0; round < 200; round++)
        {
            int drawableCount = random.Next(0, 31);
            int[] maskCounts = new int[drawableCount];
            int[][] drawableMasks = new int[drawableCount][];
            List<int> maskIds = [];

            for (int i = 0; i < drawableCount; i++)
            {
                int count = random.Next(-2, 17);
                maskCounts[i] = count;

                if (count <= 0)
                {
                    drawableMasks[i] = [];
                    continue;
                }

                int[] ids = new int[count];
                for (int j = 0; j < count; j++)
                {
                    ids[j] = random.Next(0, 8);
                }

                drawableMasks[i] = ids;
                maskIds.AddRange(ids);
            }

            int expected = CountUniqueMaskGroupsLegacy(drawableMasks);
            int actual = CubismMaskGroupUtility.CountUniqueMaskGroups(maskCounts, maskIds.ToArray());

            Assert.AreEqual(expected, actual, $"round={round}, drawables={drawableCount}");
        }
    }

    /// <summary>
    /// 重构前 CountDrawableMaskGroups 的逐行复刻: 指针读取改为 int[] 视图, 算法体不变.
    /// 仅作等价性对照, 不参与产品代码.
    /// </summary>
    /// <param name="drawableMasks">每个 drawable 的掩码 ID 数组, 空数组表示没有掩码</param>
    /// <returns>唯一的掩码组数</returns>
    private static int CountUniqueMaskGroupsLegacy(IReadOnlyList<int[]> drawableMasks)
    {
        List<int[]> groups = [];

        for (int i = 0; i < drawableMasks.Count; i++)
        {
            int count = drawableMasks[i].Length;
            if (count <= 0)
            {
                continue;
            }

            int[] mask = drawableMasks[i];
            bool found = false;

            for (int g = 0; g < groups.Count; g++)
            {
                int[] group = groups[g];
                if (group.Length != count)
                {
                    continue;
                }

                int sameCount = 0;
                for (int j = 0; j < count; j++)
                {
                    int clipId = group[j];
                    for (int k = 0; k < count; k++)
                    {
                        if (mask[k] == clipId)
                        {
                            sameCount++;
                            break;
                        }
                    }
                }

                if (sameCount == count)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                continue;
            }

            int[] copy = new int[count];
            Array.Copy(mask, copy, count);
            groups.Add(copy);
        }

        return groups.Count;
    }
}
