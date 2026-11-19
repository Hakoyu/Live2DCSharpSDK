using Live2DCSharpSDK.Framework;

namespace Live2DCSharpSDK.App;

/// <summary>
/// 解析模型资源(model3.json / moc3 / 纹理)所在的位置.
/// </summary>
/// <remarks>
/// 解析顺序(全部基于相对路径推导, 不保留任何本机绝对路径):
/// 1. 环境变量 <c>LIVE2D_MODEL_DIR</c>(以及 <c>LIVE2D_MODEL_NAME</c>) — 显式指定最优先.
/// 2. 从 <see cref="AppContext.BaseDirectory"/> 向上最多追溯 <see cref="MaxParentSearchDepth"/> 层父目录,
///    查找官方 SDK 包中的 <c>CubismSdkForNative-5-r.5/Samples/Resources</c>(一旦找到即采用).
/// 3. 可执行文件旁边的 <c>Resources</c>(面向发布包).
///
/// 任何位置都找不到时也不抛出异常. 在 <see cref="CubismLog.Warning"/> 中列出尝试过的路径并返回 false,
/// 调用方(宿主)继续以空场景启动.
/// 即使上级目录不存在, <see cref="Directory.Exists(string)"/> 也只是返回 false,
/// <see cref="Directory.GetParent(string)"/> 只是按路径文本返回父目录, 因此搜索过程中不会产生 IOException
/// (保险起见仍捕获异常并终止搜索).
/// </remarks>
public static class ModelResourceLocator
{
    /// <summary>
    /// 指定模型目录的环境变量名
    /// </summary>
    public const string ModelDirEnvName = "LIVE2D_MODEL_DIR";

    /// <summary>
    /// 指定模型名的环境变量名
    /// </summary>
    public const string ModelNameEnvName = "LIVE2D_MODEL_NAME";

    /// <summary>
    /// 模型名的默认值(与官方示例相同的 Haru)
    /// </summary>
    public const string DefaultModelName = "Haru";

    /// <summary>
    /// 官方 SDK 包的目录名(相对搜索的标记)
    /// </summary>
    public const string SdkPackageDirectoryName = "CubismSdkForNative-5-r.5";

    /// <summary>
    /// 从 <see cref="AppContext.BaseDirectory"/> 向父方向追溯的最大层数.
    /// 0 表示 BaseDirectory 自身, 合计检查 <see cref="MaxParentSearchDepth"/> + 1 个位置.
    /// </summary>
    public const int MaxParentSearchDepth = 5;

    /// <summary>
    /// SDK 包内示例模型的存放位置(相对路径)
    /// </summary>
    public static string SdkSamplesRelativePath => Path.Combine(SdkPackageDirectoryName, "Samples", "Resources");

    /// <summary>
    /// 可执行文件旁边的 <c>Resources</c> 目录(面向发布包)
    /// </summary>
    public static string LocalResourcesDirectory => Path.Combine(AppContext.BaseDirectory, "Resources");

    /// <summary>
    /// 从指定目录向父方向查找官方 SDK 包中的 <c>Samples/Resources</c>.
    /// </summary>
    /// <param name="startDirectory">
    /// 搜索起始目录. 为 null 或空白时使用 <see cref="AppContext.BaseDirectory"/>.
    /// </param>
    /// <returns>找到的绝对路径. 未找到时为 null.</returns>
    public static string? FindSdkSamplesResources(string? startDirectory = null)
        => FindSdkSamplesResourcesCore(startDirectory, null);

    /// <summary>
    /// 解析模型的位置. 找不到时也不抛出异常(false + Warning).
    /// </summary>
    /// <param name="modelName">模型名. 为 null 或空时依次取环境变量 LIVE2D_MODEL_NAME, 默认值 Haru</param>
    /// <param name="dir">解析出的模型目录(找不到时为最后尝试的候选)</param>
    /// <param name="name">解析出的模型名</param>
    /// <returns>找到模型时为 true</returns>
    public static bool TryResolve(string? modelName, out string dir, out string name)
    {
        name = ResolveName(modelName);
        List<string> attempted = [];

        // 1. 环境变量(有显式指定时最优先, 指定有误时回退到相对搜索)
        string? envDir = Environment.GetEnvironmentVariable(ModelDirEnvName);
        if (!string.IsNullOrWhiteSpace(envDir))
        {
            attempted.Add($"{ModelDirEnvName}=\"{envDir}\"");
            if (IsModelPresent(envDir, name))
            {
                dir = envDir;
                CubismLog.Info($"[Live2D App]モデルを環境変数から解決: dir=\"{dir}\", name=\"{name}\"");
                return true;
            }

            CubismLog.Warning($"[Live2D App]{ModelDirEnvName}=\"{envDir}\" にモデル '{name}' が見つからないため、相対探索を試します。");
        }

        // 2. 从 BaseDirectory 向父方向的 SDK 相对搜索
        string? sdkSamples = FindSdkSamplesResourcesCore(null, attempted);
        if (sdkSamples is not null && IsModelPresent(sdkSamples, name))
        {
            dir = sdkSamples;
            CubismLog.Info($"[Live2D App]モデルを SDK 相対探索で解決: dir=\"{dir}\", name=\"{name}\"");
            return true;
        }

        // 3. 可执行文件旁边的 Resources(发布包)
        string localResources = LocalResourcesDirectory;
        attempted.Add(localResources);
        if (IsModelPresent(localResources, name))
        {
            dir = localResources;
            CubismLog.Info($"[Live2D App]モデルを実行ファイル隣の Resources から解決: dir=\"{dir}\", name=\"{name}\"");
            return true;
        }

        dir = attempted[^1];
        CubismLog.Warning(
            $"[Live2D App]モデル '{name}' が見つかりません。試行したパス: [{string.Join(" | ", attempted)}]。" +
            "空のシーンで起動します。");
        return false;
    }

    /// <summary>
    /// 决定模型名(参数 → 环境变量 → 默认值).
    /// </summary>
    /// <param name="modelName">参数传入的模型名</param>
    /// <returns>实际使用的模型名</returns>
    private static string ResolveName(string? modelName)
    {
        if (!string.IsNullOrWhiteSpace(modelName))
        {
            return modelName;
        }

        string? envName = Environment.GetEnvironmentVariable(ModelNameEnvName);
        return string.IsNullOrWhiteSpace(envName) ? DefaultModelName : envName;
    }

    /// <summary>
    /// 相对搜索的主体实现. 传入 <paramref name="attempted"/> 时会依次追加尝试过的候选路径.
    /// </summary>
    private static string? FindSdkSamplesResourcesCore(string? startDirectory, List<string>? attempted)
    {
        string? current = NormalizeDirectory(startDirectory);
        if (current is null)
        {
            return null;
        }

        for (int depth = 0; depth <= MaxParentSearchDepth && !string.IsNullOrEmpty(current); depth++)
        {
            string candidate = Path.Combine(current, SdkSamplesRelativePath);
            attempted?.Add(candidate);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = GetParentDirectory(current);
        }

        return null;
    }

    /// <summary>
    /// 把搜索起点规范化为绝对路径. 非法路径输出 Warning 并返回 null(不抛异常).
    /// </summary>
    private static string? NormalizeDirectory(string? directory)
    {
        string path = string.IsNullOrWhiteSpace(directory) ? AppContext.BaseDirectory : directory;

        try
        {
            // 也接受相对路径指定, 但搜索以绝对路径进行(以确保 GetParent 的终点判定可靠).
            // 去掉末尾的目录分隔符: AppContext.BaseDirectory 形如 "…\net10.0\" 那样
            // 带末尾分隔符, 直接调用 GetParent 会再返回同一层, 使搜索层数少一层.
            // 根路径(C:\ / \\server\share)会被 TrimEndingDirectorySeparator 原样保留.
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            CubismLog.Warning($"[Live2D App]モデル探索の起点 \"{path}\" が不正です: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 返回父目录. 到达根路径或无法取得时返回 null(不抛异常).
    /// </summary>
    private static string? GetParentDirectory(string directory)
    {
        try
        {
            // 即使路径不存在也按文本返回父目录. 根路径(C:\ / \\server\share)返回 null.
            return Directory.GetParent(directory)?.FullName;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            CubismLog.Warning($"[Live2D App]親ディレクトリの取得に失敗しました \"{directory}\": {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 判定指定目录中是否存在模型.
    /// 检查 LAppLive2DManager.LoadModel 接受的 3 种形态(文件 / .model3.json / 同名子目录).
    /// </summary>
    /// <param name="dir">模型目录</param>
    /// <param name="name">模型名</param>
    /// <returns>存在时为 true</returns>
    private static bool IsModelPresent(string dir, string name)
    {
        if (!Directory.Exists(dir))
        {
            return false;
        }

        string direct = Path.Combine(dir, name);
        if (File.Exists(direct) || File.Exists(direct + ".model3.json"))
        {
            return true;
        }

        return File.Exists(Path.Combine(direct, name + ".model3.json"));
    }
}
