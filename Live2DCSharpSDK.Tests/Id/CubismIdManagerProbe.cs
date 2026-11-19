using System.Reflection;
using Live2DCSharpSDK.Framework.Id;

namespace Live2DCSharpSDK.Tests.Id;

/// <summary>
/// <see cref="CubismIdManager"/> 的白盒观察钩子.
/// 产品代码没有公开内部登记数量, 这里通过唯一的 List&lt;string&gt; 私有字段读取登记表;
/// 字段类型或数量变化时用例会以明确消息失败, 提醒同步更新这个钩子.
/// </summary>
internal static class CubismIdManagerProbe
{
    /// <summary>
    /// 读取内部登记表的副本.
    /// </summary>
    /// <param name="manager">被观察的 ID 管理器</param>
    /// <returns>内部登记表的副本, 顺序与登记顺序一致</returns>
    public static IReadOnlyList<string> GetRegisteredIds(CubismIdManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        if (ResolveIdsField().GetValue(manager) is not List<string> ids)
        {
            throw new InvalidOperationException(
                "The internal id field of CubismIdManager is not a List<string>."
            );
        }

        return ids.ToArray();
    }

    /// <summary>
    /// 读取内部登记数量.
    /// </summary>
    /// <param name="manager">被观察的 ID 管理器</param>
    /// <returns>已登记的数量</returns>
    public static int GetRegisteredCount(CubismIdManager manager)
    {
        return GetRegisteredIds(manager).Count;
    }

    /// <summary>
    /// 定位保存登记表的私有实例字段.
    /// </summary>
    /// <returns>登记表字段</returns>
    /// <exception cref="InvalidOperationException">字段不存在或不唯一时抛出</exception>
    private static FieldInfo ResolveIdsField()
    {
        FieldInfo[] fields = typeof(CubismIdManager)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(List<string>))
            .ToArray();

        if (fields.Length != 1)
        {
            throw new InvalidOperationException(
                $"CubismIdManager is expected to have exactly one List<string> field, but {fields.Length} were found."
            );
        }

        return fields[0];
    }
}
