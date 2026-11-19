using System.Reflection;
using Live2DCSharpSDK.Framework;

namespace Live2DCSharpSDK.Tests.Context;

/// <summary>
/// 不启动 Core 的 CubismContext.Current 测试钩子.
/// 产品代码只在 StartUp 里设置 Current, 而 A 档不能调用 StartUp (会注册进程级 Core 回调);
/// 这里直接读写 CubismContext 内部保存 Current 的 AsyncLocal, 从而在纯托管环境里验证日志隔离.
/// 调用方必须用 try/finally 把旧值写回 (见 PLAN-06 §6).
/// </summary>
internal static class CubismContextCurrentHook
{
    /// <summary>
    /// 读取当前执行上下文里的 CubismContext.Current.
    /// </summary>
    /// <returns>当前上下文, 未设置时为 null</returns>
    public static CubismContext? GetCurrent()
    {
        return GetStorage().Value;
    }

    /// <summary>
    /// 设置当前执行上下文里的 CubismContext.Current.
    /// AsyncLocal 的值只影响当前执行上下文, 不会影响其它线程.
    /// </summary>
    /// <param name="context">要设置的上下文, null 表示没有当前上下文</param>
    public static void SetCurrent(CubismContext? context)
    {
        GetStorage().Value = context;
    }

    /// <summary>
    /// 定位保存 Current 的私有静态 AsyncLocal 字段.
    /// </summary>
    /// <returns>AsyncLocal 存储</returns>
    /// <exception cref="InvalidOperationException">字段不存在或不唯一时抛出</exception>
    private static AsyncLocal<CubismContext?> GetStorage()
    {
        FieldInfo[] fields = typeof(CubismContext)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(AsyncLocal<CubismContext?>))
            .ToArray();

        if (fields.Length != 1)
        {
            throw new InvalidOperationException(
                $"CubismContext is expected to have exactly one AsyncLocal<CubismContext?> field, but {fields.Length} were found."
            );
        }

        if (fields[0].GetValue(null) is not AsyncLocal<CubismContext?> storage)
        {
            throw new InvalidOperationException(
                "The AsyncLocal field of CubismContext has no value."
            );
        }

        return storage;
    }
}
