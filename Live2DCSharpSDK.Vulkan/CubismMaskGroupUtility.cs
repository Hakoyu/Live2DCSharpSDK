using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Vulkan;

/// <summary>
/// クリッピングマスクグループの数え上げユーティリティ。
/// 官方 CubismClippingManager::FindSameClip（Framework/Rendering CubismClippingManager.tpp:144-173）
/// と同じ判定で Core の drawable mask 配列を去重する。
/// LAppDelegateVulkan.CreateRenderer が maskBufferCount を決めるために使用する
/// （OpenGL 側 CubismMaskGroupUtility と同一ロジック（P8c で Vulkan 側に複製））。
/// </summary>
internal static class CubismMaskGroupUtility
{
    /// <summary>
    /// drawable のマスクグループ数を返す（maskCount が 0 以下の drawable は対象外）。
    /// </summary>
    /// <param name="model">モデルのインスタンス</param>
    /// <returns>ユニークなマスクグループ数</returns>
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

                // FindSameClip と同じ「要素数が同じで、全 ID が一致すれば同一」判定
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