using System.Text.Json.Nodes;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Effect;

/// <summary>
/// 进行部件不透明度的管理与设置.
/// </summary>
public class CubismPose
{
    public const float Epsilon = 0.001f;
    public const float DefaultFadeInSeconds = 0.5f;

    // Pose.json 的标签
    public const string FadeIn = "FadeInTime";
    public const string Link = "Link";
    public const string Groups = "Groups";
    public const string Id = "Id";

    /// <summary>
    /// 部件分组
    /// </summary>
    private readonly List<PartData> _partGroups = [];

    /// <summary>
    /// 各部件分组的个数
    /// </summary>
    private readonly List<int> _partGroupCounts = [];

    /// <summary>
    /// 渐变时间[秒]
    /// </summary>
    private readonly float _fadeTimeSeconds = DefaultFadeInSeconds;

    /// <summary>
    /// 上一次操作的模型
    /// </summary>
    private CubismModel? _lastModel;

    /// <summary>
    /// 所属的上下文实例, 用于解析部件 ID.
    /// </summary>
    private readonly CubismContext _context;

    /// <summary>
    /// 创建实例.
    /// </summary>
    /// <param name="pose3json">pose3.json 的数据</param>
    /// <param name="context">上下文实例</param>
    /// <exception cref="ArgumentNullException">context 为 null 时抛出</exception>
    public CubismPose(string pose3json, CubismContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(
                nameof(context),
                "CubismPose requires a context instance."
            );
        }

        _context = context;

        using var stream = File.Open(
            pose3json,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite
        );
        var json = JsonNode.Parse(stream)?.AsObject() ?? throw new Exception("Pose json is error");

        // 渐变时间的指定
        if (json.ContainsKey(FadeIn))
        {
            var item = json[FadeIn];
            _fadeTimeSeconds = item == null ? DefaultFadeInSeconds : (float)item;

            if (_fadeTimeSeconds < 0.0f)
            {
                _fadeTimeSeconds = DefaultFadeInSeconds;
            }
        }

        // 部件分组
        if (json[Groups] is not JsonArray poseListInfo)
            return;

        foreach (var item in poseListInfo)
        {
            int idCount = item!.AsArray().Count;
            int groupCount = 0;

            for (int groupIndex = 0; groupIndex < idCount; ++groupIndex)
            {
                var partInfo = item[groupIndex]!;
                PartData partData = new()
                {
                    PartId = _context.IdManager.GetId(partInfo[Id]!.ToString()),
                };

                // 设置链接的部件
                if (partInfo[Link] != null)
                {
                    var linkListInfo = partInfo[Link]!;
                    int linkCount = linkListInfo.AsArray().Count;

                    for (int linkIndex = 0; linkIndex < linkCount; ++linkIndex)
                    {
                        partData.Link.Add(
                            new()
                            {
                                PartId = _context.IdManager.GetId(
                                    linkListInfo[linkIndex]!.ToString()
                                ),
                            }
                        );
                    }
                }

                _partGroups.Add(partData);

                ++groupCount;
            }

            _partGroupCounts.Add(groupCount);
        }
    }

    /// <summary>
    /// 更新模型参数.
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    public void UpdateParameters(CubismModel model, float deltaTimeSeconds)
    {
        // 与上一次的模型不同时需要初始化
        if (model.Model != _lastModel?.Model)
        {
            // 参数索引的初始化
            Reset(model);
        }

        _lastModel = model;

        // 从设置中修改时间时, 经过时间可能变为负数, 此时按经过时间 0 处理.
        if (deltaTimeSeconds < 0.0f)
        {
            deltaTimeSeconds = 0.0f;
        }

        int beginIndex = 0;

        foreach (var item in _partGroupCounts)
        {
            DoFade(model, deltaTimeSeconds, beginIndex, item);

            beginIndex += item;
        }

        CopyPartOpacities(model);
    }

    /// <summary>
    /// 初始化显示.
    ///
    /// 初始不透明度不为 0 的参数, 将不透明度设为 1.
    /// </summary>
    /// <param name="model">目标模型</param>
    public void Reset(CubismModel model)
    {
        int beginIndex = 0;

        foreach (var item in _partGroupCounts)
        {
            for (int j = beginIndex; j < beginIndex + item; ++j)
            {
                _partGroups[j].Initialize(model);

                int partsIndex = _partGroups[j].PartIndex;
                int paramIndex = _partGroups[j].ParameterIndex;

                if (partsIndex < 0)
                {
                    continue;
                }

                model.SetPartOpacity(partsIndex, j == beginIndex ? 1.0f : 0.0f);
                model.SetParameterValue(paramIndex, j == beginIndex ? 1.0f : 0.0f);

                for (int k = 0; k < _partGroups[j].Link.Count; ++k)
                {
                    _partGroups[j].Link[k].Initialize(model);
                }
            }

            beginIndex += item;
        }
    }

    /// <summary>
    /// 拷贝部件的不透明度, 并设置到链接的部件上.
    /// </summary>
    /// <param name="model">目标模型</param>
    private void CopyPartOpacities(CubismModel model)
    {
        foreach (var item in _partGroups)
        {
            if (item.Link.Count == 0)
            {
                continue; // 没有联动的参数
            }

            int partIndex = item.PartIndex;
            float opacity = model.GetPartOpacity(partIndex);

            foreach (var item1 in item.Link)
            {
                int linkPartIndex = item1.PartIndex;

                if (linkPartIndex < 0)
                {
                    continue;
                }

                model.SetPartOpacity(linkPartIndex, opacity);
            }
        }
    }

    /// <summary>
    /// 进行部件渐变操作.
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    /// <param name="beginIndex">进行渐变操作的部件分组的起始索引</param>
    /// <param name="partGroupCount">进行渐变操作的部件分组的个数</param>
    private void DoFade(
        CubismModel model,
        float deltaTimeSeconds,
        int beginIndex,
        int partGroupCount
    )
    {
        int visiblePartIndex = -1;
        float newOpacity = 1.0f;

        float Phi = 0.5f;
        float BackOpacityThreshold = 0.15f;

        // 获取当前处于显示状态的部件
        for (int i = beginIndex; i < beginIndex + partGroupCount; ++i)
        {
            int partIndex = _partGroups[i].PartIndex;
            int paramIndex = _partGroups[i].ParameterIndex;

            if (model.GetParameterValue(paramIndex) > Epsilon)
            {
                if (visiblePartIndex >= 0)
                {
                    break;
                }

                visiblePartIndex = i;
                if (_fadeTimeSeconds == 0.0f)
                {
                    newOpacity = 1.0f;
                    continue;
                }
                newOpacity = model.GetPartOpacity(partIndex);

                // 计算新的不透明度
                newOpacity += deltaTimeSeconds / _fadeTimeSeconds;

                if (newOpacity > 1.0f)
                {
                    newOpacity = 1.0f;
                }
            }
        }

        if (visiblePartIndex < 0)
        {
            visiblePartIndex = 0;
            newOpacity = 1.0f;
        }

        //  设置显示部件, 非显示部件的不透明度
        for (int i = beginIndex; i < beginIndex + partGroupCount; ++i)
        {
            int partsIndex = _partGroups[i].PartIndex;

            //  显示部件的设置
            if (visiblePartIndex == i)
            {
                model.SetPartOpacity(partsIndex, newOpacity); // 先设置
            }
            // 非显示部件的设置
            else
            {
                float opacity = model.GetPartOpacity(partsIndex);
                float a1; // 通过计算求得的不透明度

                if (newOpacity < Phi)
                {
                    a1 = newOpacity * (Phi - 1) / Phi + 1.0f; // 通过 (0,1),(phi,phi) 的直线方程
                }
                else
                {
                    a1 = (1 - newOpacity) * Phi / (1.0f - Phi); // 通过 (1,0),(phi,phi) 的直线方程
                }

                // 限制背景可见比例的情形
                float backOpacity = (1.0f - a1) * (1.0f - newOpacity);

                if (backOpacity > BackOpacityThreshold)
                {
                    a1 = 1.0f - BackOpacityThreshold / (1.0f - newOpacity);
                }

                if (opacity > a1)
                {
                    opacity = a1; // 若大于计算结果的不透明度 (即更浓), 则提高不透明度
                }

                model.SetPartOpacity(partsIndex, opacity);
            }
        }
    }
}
