using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// Clipping 掩码组的计数工具.
/// 采用与官方 CubismClippingManager::FindSameClip(Framework/Rendering CubismClippingManager.tpp:144-173)
/// 相同的判定, 对 Core 的 drawable mask 数组去重.
/// 由各后端 (OpenGL / Vulkan) 的 LAppDelegate 共用, 用于决定 maskBufferCount.
/// </summary>
public static class CubismMaskGroupUtility
{
    /// <summary>
    /// 返回 drawable 的掩码组数 (maskCount 小于等于 0 的 drawable 不计入).
    /// </summary>
    /// <param name="model">模型实例</param>
    /// <returns>唯一的掩码组数</returns>
    public static unsafe int CountDrawableMaskGroups(CubismModel model)
    {
        int drawableCount = model.GetDrawableCount();
        int* maskCounts = model.GetDrawableMaskCounts();
        int** masks = model.GetDrawableMasks();

        // 把 Core 的指针视图复制成按 drawable 顺序拼接的托管数组, 再交给纯逻辑入口计数.
        // 本方法只在创建 renderer 时调用一次, 不在每帧路径上.
        int totalMaskCount = 0;
        for (int i = 0; i < drawableCount; i++)
        {
            if (maskCounts[i] > 0)
            {
                totalMaskCount += maskCounts[i];
            }
        }

        int[] maskIds = new int[totalMaskCount];

        int offset = 0;
        for (int i = 0; i < drawableCount; i++)
        {
            int count = maskCounts[i];
            if (count <= 0)
            {
                continue;
            }

            int* mask = masks[i];
            for (int j = 0; j < count; j++)
            {
                maskIds[offset++] = mask[j];
            }
        }

        return CountUniqueMaskGroups(new ReadOnlySpan<int>(maskCounts, drawableCount), maskIds);
    }

    /// <summary>
    /// 从纯托管视图统计唯一的掩码组数, 判定与重构前的指针实现逐分支相同.
    ///
    /// <paramref name="maskIds"/> 按 drawable 顺序拼接各 drawable 的掩码 ID:
    /// 第 i 个 drawable 占用从前面所有正掩码数累加得到的偏移开始的 maskCounts[i] 个元素;
    /// maskCounts[i] 小于等于 0 表示该 drawable 没有掩码, 不消耗 maskIds 中的元素.
    /// </summary>
    /// <param name="maskCounts">每个 drawable 的掩码个数</param>
    /// <param name="maskIds">按 drawable 顺序拼接的掩码 ID</param>
    /// <returns>唯一的掩码组数</returns>
    /// <exception cref="ArgumentException">maskIds 长度不足以容纳 maskCounts 声明的内容时抛出</exception>
    internal static int CountUniqueMaskGroups(ReadOnlySpan<int> maskCounts, ReadOnlySpan<int> maskIds)
    {
        List<int[]> groups = [];
        int offset = 0;

        for (int i = 0; i < maskCounts.Length; i++)
        {
            int count = maskCounts[i];
            if (count <= 0)
            {
                continue;
            }

            if (maskIds.Length - offset < count)
            {
                throw new ArgumentException(
                    $"maskIds must contain at least {offset + count} elements for drawable {i}, but it has {maskIds.Length}.",
                    nameof(maskIds));
            }

            int groupOffset = offset;
            offset += count;

            bool found = false;

            for (int g = 0; g < groups.Count; g++)
            {
                int[] group = groups[g];
                if (group.Length != count)
                {
                    continue;
                }

                // 与 FindSameClip 相同的判定: 元素个数相同, 且全部 ID 都一致即视为同一
                int sameCount = 0;
                for (int j = 0; j < count; j++)
                {
                    int clipId = group[j];
                    for (int k = 0; k < count; k++)
                    {
                        if (maskIds[groupOffset + k] == clipId)
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
            maskIds.Slice(groupOffset, count).CopyTo(copy);
            groups.Add(copy);
        }

        return groups.Count;
    }
}
