namespace Live2DCSharpSDK.Framework;

public static class CubismModelSettingJson
{
    // JSON 键名
    public const string Version = "Version";
    public const string FileReferences = "FileReferences";
    public const string Groups = "Groups";
    public const string Layout = "Layout";
    public const string HitAreas = "HitAreas";

    public const string Moc = "Moc";
    public const string Textures = "Textures";
    public const string Physics = "Physics";
    public const string DisplayInfo = "DisplayInfo";
    public const string Pose = "Pose";
    public const string Expressions = "Expressions";
    public const string Motions = "Motions";

    public const string UserData = "UserData";
    public const string Name = "Name";
    public const string FilePath = "File";
    public const string Id = "Id";
    public const string Ids = "Ids";
    public const string Target = "Target";

    // 动作名
    public const string Idle = "Idle";
    public const string TapBody = "TapBody";
    public const string PinchIn = "PinchIn";
    public const string PinchOut = "PinchOut";
    public const string Shake = "Shake";
    public const string FlickHead = "FlickHead";
    public const string Parameter = "Parameter";

    public const string SoundPath = "Sound";
    public const string FadeInTime = "FadeInTime";
    public const string FadeOutTime = "FadeOutTime";

    // 布局
    public const string CenterX = "CenterX";
    public const string CenterY = "CenterY";
    public const string X = "X";
    public const string Y = "Y";
    public const string Width = "Width";
    public const string Height = "Height";

    public const string LipSync = "LipSync";
    public const string EyeBlink = "EyeBlink";

    public const string InitParameter = "init_param";
    public const string InitPartsVisible = "init_parts_visible";
    public const string Val = "val";

    public static bool GetLayoutMap(
        this ModelSettingObj obj,
        Dictionary<string, float> outLayoutMap
    )
    {
        var node = obj.Layout;
        if (node == null)
            return false;

        var ret = false;
        foreach (var item in node)
        {
            if (outLayoutMap.ContainsKey(item.Key))
            {
                outLayoutMap[item.Key] = item.Value;
            }
            else
            {
                outLayoutMap.Add(item.Key, item.Value);
            }
            ret = true;
        }
        return ret;
    }

    public static bool IsExistEyeBlinkParameters(this ModelSettingObj obj)
    {
        var node = obj.Groups;
        if (node == null)
        {
            return false;
        }

        foreach (var item in node)
        {
            if (item.Name == EyeBlink)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsExistLipSyncParameters(this ModelSettingObj obj)
    {
        var node = obj.Groups;
        if (node == null)
        {
            return false;
        }

        foreach (var item in node)
        {
            if (item.Name == LipSync)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 获取指定动作组的动作数量(对应官方 CubismModelSettingJson::GetMotionCount).
    /// </summary>
    /// <param name="obj">模型设置</param>
    /// <param name="groupName">动作组名</param>
    /// <returns>动作数量, 组不存在时为 0</returns>
    public static int GetMotionCount(this ModelSettingObj obj, string groupName)
    {
        var list = FindMotionList(obj, groupName);

        return list?.Count ?? 0;
    }

    /// <summary>
    /// 获取指定动作的文件名(对应官方 CubismModelSettingJson::GetMotionFileName).
    /// </summary>
    /// <param name="obj">模型设置</param>
    /// <param name="groupName">动作组名</param>
    /// <param name="index">组内索引</param>
    /// <returns>motion3.json 的文件名(与 model3.json 记载一致), 不存在时为 null</returns>
    public static string? GetMotionFileName(this ModelSettingObj obj, string groupName, int index)
    {
        return FindMotion(obj, groupName, index)?.File;
    }

    /// <summary>
    /// 获取指定动作的淡入时间(对应官方 CubismModelSettingJson::GetMotionFadeInTimeValue).
    /// 负值表示不覆盖 motion3.json 中的值.
    /// </summary>
    /// <remarks>
    /// 官方在未记录时返回 -1, 但本地 ModelSettingObj 的 [DefaultValue(-1f)] 在
    /// System.Text.Json 下不生效, 因此未记录会得到 0(已知偏差).
    /// </remarks>
    /// <param name="obj">模型设置</param>
    /// <param name="groupName">动作组名</param>
    /// <param name="index">组内索引</param>
    /// <returns>淡入时间, 不存在时为 -1</returns>
    public static float GetMotionFadeInTimeValue(
        this ModelSettingObj obj,
        string groupName,
        int index
    )
    {
        var motion = FindMotion(obj, groupName, index);

        return motion?.FadeInTime ?? -1.0f;
    }

    /// <summary>
    /// 获取指定动作的淡出时间(对应官方 CubismModelSettingJson::GetMotionFadeOutTimeValue).
    /// 负值表示不覆盖 motion3.json 中的值.
    /// </summary>
    /// <param name="obj">模型设置</param>
    /// <param name="groupName">动作组名</param>
    /// <param name="index">组内索引</param>
    /// <returns>淡出时间, 不存在时为 -1</returns>
    public static float GetMotionFadeOutTimeValue(
        this ModelSettingObj obj,
        string groupName,
        int index
    )
    {
        var motion = FindMotion(obj, groupName, index);

        return motion?.FadeOutTime ?? -1.0f;
    }

    /// <summary>
    /// 获取组内的动作列表(内部使用).
    /// </summary>
    private static List<ModelSettingObj.FileReference.Motion>? FindMotionList(
        ModelSettingObj obj,
        string groupName
    )
    {
        var motions = obj.FileReferences?.Motions;
        if (motions == null || string.IsNullOrEmpty(groupName))
        {
            return null;
        }

        return motions.TryGetValue(groupName, out var list) ? list : null;
    }

    /// <summary>
    /// 获取组内的动作(内部使用).
    /// </summary>
    private static ModelSettingObj.FileReference.Motion? FindMotion(
        ModelSettingObj obj,
        string groupName,
        int index
    )
    {
        var list = FindMotionList(obj, groupName);

        if (list == null || index < 0 || index >= list.Count)
        {
            return null;
        }

        return list[index];
    }
}
