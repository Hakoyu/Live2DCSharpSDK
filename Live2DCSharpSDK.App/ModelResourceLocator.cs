using Live2DCSharpSDK.Framework;

namespace Live2DCSharpSDK.App;

/// <summary>
/// モデルリソース（model3.json / moc3 / テクスチャ）の場所を解決する。
/// </summary>
/// <remarks>
/// 解決順（v6: 相对推导。本仓库不再保有任何本机绝对路径）:
/// 1. 環境変数 <c>LIVE2D_MODEL_DIR</c>（+ <c>LIVE2D_MODEL_NAME</c>）— 明示指定を最優先。
/// 2. <see cref="AppContext.BaseDirectory"/> から親ディレクトリへ最大 <see cref="MaxParentSearchDepth"/> 階層たどり、
///    公式 SDK パッケージの <c>CubismSdkForNative-5-r.5/Samples/Resources</c> を探す（見つかった時点で採用）。
/// 3. 実行ファイル隣の <c>Resources</c>（配布物を想定）。
///
/// どこにも見つからない場合も例外は投げない。<see cref="CubismLog.Warning"/> に試行したパスを列挙して false を返し、
/// 呼び出し側（ホスト）は空のシーンで起動を続ける。
/// 上位ディレクトリが存在しない場合でも <see cref="Directory.Exists(string)"/> は false を返し、
/// <see cref="Directory.GetParent(string)"/> は語彙的に親を返すだけなので、探索中に IOException は発生しない
/// （念のため例外は捕捉して探索を打ち切る）。
/// </remarks>
public static class ModelResourceLocator
{
    /// <summary>
    /// モデルディレクトリを指定する環境変数名
    /// </summary>
    public const string ModelDirEnvName = "LIVE2D_MODEL_DIR";

    /// <summary>
    /// モデル名を指定する環境変数名
    /// </summary>
    public const string ModelNameEnvName = "LIVE2D_MODEL_NAME";

    /// <summary>
    /// モデル名の既定値（公式サンプルと同じ Haru）
    /// </summary>
    public const string DefaultModelName = "Haru";

    /// <summary>
    /// 公式 SDK パッケージのディレクトリ名（相対探索の目印）
    /// </summary>
    public const string SdkPackageDirectoryName = "CubismSdkForNative-5-r.5";

    /// <summary>
    /// <see cref="AppContext.BaseDirectory"/> から親方向へたどる最大階層数。
    /// 0 が BaseDirectory 自身で、合計 <see cref="MaxParentSearchDepth"/> + 1 箇所を調べる。
    /// </summary>
    public const int MaxParentSearchDepth = 5;

    /// <summary>
    /// SDK パッケージ内のサンプルモデル格納先（相対パス）
    /// </summary>
    public static string SdkSamplesRelativePath => Path.Combine(SdkPackageDirectoryName, "Samples", "Resources");

    /// <summary>
    /// 実行ファイル隣の <c>Resources</c> ディレクトリ（配布物を想定）
    /// </summary>
    public static string LocalResourcesDirectory => Path.Combine(AppContext.BaseDirectory, "Resources");

    /// <summary>
    /// 公式 SDK パッケージの <c>Samples/Resources</c> を、指定ディレクトリから親方向へ探す。
    /// </summary>
    /// <param name="startDirectory">
    /// 探索開始ディレクトリ。null/空白 の場合は <see cref="AppContext.BaseDirectory"/>。
    /// </param>
    /// <returns>見つかった絶対パス。見つからなければ null。</returns>
    public static string? FindSdkSamplesResources(string? startDirectory = null)
        => FindSdkSamplesResourcesCore(startDirectory, null);

    /// <summary>
    /// モデルの場所を解決する。見つからない場合も例外は投げない（false + Warning）。
    /// </summary>
    /// <param name="modelName">モデル名。null/空 の場合は環境変数 LIVE2D_MODEL_NAME → 既定値 Haru</param>
    /// <param name="dir">解決したモデルディレクトリ（見つからない場合は最後に試した候補）</param>
    /// <param name="name">解決したモデル名</param>
    /// <returns>モデルが見つかったら true</returns>
    public static bool TryResolve(string? modelName, out string dir, out string name)
    {
        name = ResolveName(modelName);
        List<string> attempted = [];

        // 1. 環境変数（明示指定があれば最優先。誤指定時は相対探索へフォールバックする）
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

        // 2. BaseDirectory から親方向への SDK 相対探索
        string? sdkSamples = FindSdkSamplesResourcesCore(null, attempted);
        if (sdkSamples is not null && IsModelPresent(sdkSamples, name))
        {
            dir = sdkSamples;
            CubismLog.Info($"[Live2D App]モデルを SDK 相対探索で解決: dir=\"{dir}\", name=\"{name}\"");
            return true;
        }

        // 3. 実行ファイル隣の Resources（配布物）
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
    /// モデル名を決める（引数 → 環境変数 → 既定値）。
    /// </summary>
    /// <param name="modelName">引数のモデル名</param>
    /// <returns>使用するモデル名</returns>
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
    /// 相対探索の本体。<paramref name="attempted"/> を渡すと、試行した候補パスを順に追記する。
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
    /// 探索起点を絶対パスへ正規化する。不正なパスは Warning を出して null を返す（例外にしない）。
    /// </summary>
    private static string? NormalizeDirectory(string? directory)
    {
        string path = string.IsNullOrWhiteSpace(directory) ? AppContext.BaseDirectory : directory;

        try
        {
            // 相対指定も受け付けるが、探索は絶対パスで行う（GetParent の終端判定を確実にするため）。
            // 末尾のディレクトリ区切りは落とす: AppContext.BaseDirectory は "…\net10.0\" のように
            // 末尾区切り付きで、そのまま GetParent すると同じ階層がもう一度返り探索段数が 1 層損なわれる。
            // ルート（C:\ / \\server\share）は TrimEndingDirectorySeparator がそのまま維持する。
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            CubismLog.Warning($"[Live2D App]モデル探索の起点 \"{path}\" が不正です: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 親ディレクトリを返す。ルートに達した場合や取得できない場合は null（例外にしない）。
    /// </summary>
    private static string? GetParentDirectory(string directory)
    {
        try
        {
            // 存在しないパスでも語彙的に親を返す。ルート（C:\ / \\server\share）では null。
            return Directory.GetParent(directory)?.FullName;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            CubismLog.Warning($"[Live2D App]親ディレクトリの取得に失敗しました \"{directory}\": {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 指定ディレクトリにモデルが存在するか判定する。
    /// LAppLive2DManager.LoadModel が受け付ける 3 形態（ファイル / .model3.json / 同名サブディレクトリ）を確認する。
    /// </summary>
    /// <param name="dir">モデルディレクトリ</param>
    /// <param name="name">モデル名</param>
    /// <returns>存在すれば true</returns>
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
