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

        List<int[]> groups = [];

        for (int i = 0; i < drawableCount; i++)
        {
            int count = maskCounts[i];
            if (count <= 0)
            {
                continue;
            }

            int* mask = masks[i];
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
            for (int j = 0; j < count; j++)
            {
                copy[j] = mask[j];
            }
            groups.Add(copy);
        }

        return groups.Count;
    }
}
