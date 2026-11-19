using System.Text.Json;

namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 进行用户数据的加载, 管理, 检索接口以及释放.
/// </summary>
public class CubismModelUserData
{
    public const string ArtMesh = "ArtMesh";
    public const string Meta = "Meta";
    public const string UserDataCount = "UserDataCount";
    public const string TotalUserDataSize = "TotalUserDataSize";
    public const string UserData = "UserData";
    public const string Target = "Target";
    public const string Id = "Id";
    public const string Value = "Value";

    /// <summary>
    /// 用户数据结构体数组
    /// </summary>
    private readonly List<CubismModelUserDataNode> _userDataNodes = [];
    /// <summary>
    /// 保持浏览列表
    /// </summary>
    public readonly List<CubismModelUserDataNode> ArtMeshUserDataNodes = [];

    /// <summary>
    /// 使用指定上下文解析 userdata3.json.
    /// </summary>
    /// <param name="context">所属上下文 (提供 ID 管理器)</param>
    /// <param name="data">已读入 userdata3.json 的缓冲区</param>
    /// <exception cref="ArgumentNullException">context 为 null 时抛出</exception>
    public CubismModelUserData(CubismContext context, string data)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context), "CubismModelUserData requires a context instance.");
        }

        using var stream = File.Open(data, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var obj = JsonSerializer.Deserialize(stream, CubismModelUserDataObjContext.Default.CubismModelUserDataObj)
            ?? throw new Exception("Load UserData error");

        string typeOfArtMesh = context.IdManager.GetId(ArtMesh);

        int nodeCount = obj.Meta.UserDataCount;

        for (int i = 0; i < nodeCount; i++)
        {
            var node = obj.UserData[i];
            CubismModelUserDataNode addNode = new()
            {
                TargetId = context.IdManager.GetId(node.Id),
                TargetType = context.IdManager.GetId(node.Target),
                Value = context.IdManager.GetId(node.Value)
            };
            _userDataNodes.Add(addNode);

            if (addNode.TargetType == typeOfArtMesh)
            {
                ArtMeshUserDataNodes.Add(addNode);
            }
        }
    }
}
