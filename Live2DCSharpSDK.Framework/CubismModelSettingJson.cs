namespace Live2DCSharpSDK.Framework;

public static class CubismModelSettingJson
{
    // JSON keys
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

    // Motions
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

    // Layout
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

    public static bool GetLayoutMap(this ModelSettingObj obj, Dictionary<string, float> outLayoutMap)
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
    /// 指定したグループのモーション数を取得する（官方 CubismModelSettingJson::GetMotionCount）。
    /// </summary>
    /// <param name="obj">モデル設定</param>
    /// <param name="groupName">モーショングループ名</param>
    /// <returns>モーション数。グループが無い場合は 0</returns>
    public static int GetMotionCount(this ModelSettingObj obj, string groupName)
    {
        var list = FindMotionList(obj, groupName);

        return list?.Count ?? 0;
    }

    /// <summary>
    /// 指定したモーションのファイル名を取得する（官方 CubismModelSettingJson::GetMotionFileName）。
    /// </summary>
    /// <param name="obj">モデル設定</param>
    /// <param name="groupName">モーショングループ名</param>
    /// <param name="index">グループ内のインデックス</param>
    /// <returns>motion3.json のファイル名（model3.json 記載のまま）。存在しない場合は null</returns>
    public static string? GetMotionFileName(this ModelSettingObj obj, string groupName, int index)
    {
        return FindMotion(obj, groupName, index)?.File;
    }

    /// <summary>
    /// 指定したモーションのフェードイン時間を取得する（官方 CubismModelSettingJson::GetMotionFadeInTimeValue）。
    /// 負値は「motion3.json 側の値を上書きしない」を意味する。
    /// </summary>
    /// <remarks>
    /// 官方は未記載の場合 -1 を返すが、本地 ModelSettingObj は [DefaultValue(-1f)] が
    /// System.Text.Json では効かないため未記載は 0 になる（既知の偏差。報告参照）。
    /// </remarks>
    /// <param name="obj">モデル設定</param>
    /// <param name="groupName">モーショングループ名</param>
    /// <param name="index">グループ内のインデックス</param>
    /// <returns>フェードイン時間。存在しない場合は -1</returns>
    public static float GetMotionFadeInTimeValue(this ModelSettingObj obj, string groupName, int index)
    {
        var motion = FindMotion(obj, groupName, index);

        return motion?.FadeInTime ?? -1.0f;
    }

    /// <summary>
    /// 指定したモーションのフェードアウト時間を取得する（官方 CubismModelSettingJson::GetMotionFadeOutTimeValue）。
    /// 負値は「motion3.json 側の値を上書きしない」を意味する。
    /// </summary>
    /// <param name="obj">モデル設定</param>
    /// <param name="groupName">モーショングループ名</param>
    /// <param name="index">グループ内のインデックス</param>
    /// <returns>フェードアウト時間。存在しない場合は -1</returns>
    public static float GetMotionFadeOutTimeValue(this ModelSettingObj obj, string groupName, int index)
    {
        var motion = FindMotion(obj, groupName, index);

        return motion?.FadeOutTime ?? -1.0f;
    }

    /// <summary>
    /// グループ内のモーションリストを取得する（内部用）。
    /// </summary>
    private static List<ModelSettingObj.FileReference.Motion>? FindMotionList(ModelSettingObj obj, string groupName)
    {
        var motions = obj.FileReferences?.Motions;
        if (motions == null || string.IsNullOrEmpty(groupName))
        {
            return null;
        }

        return motions.TryGetValue(groupName, out var list) ? list : null;
    }

    /// <summary>
    /// グループ内のモーションを取得する（内部用）。
    /// </summary>
    private static ModelSettingObj.FileReference.Motion? FindMotion(ModelSettingObj obj, string groupName, int index)
    {
        var list = FindMotionList(obj, groupName);

        if (list == null || index < 0 || index >= list.Count)
        {
            return null;
        }

        return list[index];
    }
}
