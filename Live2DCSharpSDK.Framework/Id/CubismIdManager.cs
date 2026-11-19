namespace Live2DCSharpSDK.Framework.Id;

/// <summary>
/// 管理 ID 名称.
/// </summary>
public class CubismIdManager
{
    /// <summary>
    /// 已注册的 ID 列表.
    /// </summary>
    private readonly List<string> _ids = [];

    /// <summary>
    /// 从列表注册 ID 名称.
    /// </summary>
    /// <param name="list">ID 名称列表</param>
    public void RegisterIds(List<string> list)
    {
        list.ForEach((item) =>
        {
            GetId(item);
        });
    }

    /// <summary>
    /// 根据 ID 名称获取 ID.
    /// 未注册的 ID 名称也会被注册.
    /// </summary>
    /// <param name="item">ID 名称</param>
    public string GetId(string item)
    {
        if (_ids.Contains(item))
            return item;

        _ids.Add(item);

        return item;
    }
}
