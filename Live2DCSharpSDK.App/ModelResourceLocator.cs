using Live2DCSharpSDK.Framework;

namespace Live2DCSharpSDK.App;

/// <summary>
/// モデルリソース（model3.json / moc3 / テクスチャ）の場所を解決する。
/// </summary>
/// <remarks>
/// 上流 sample（Coloryr 版の OpenTK / WPF デモ）は <c>"F:\\live2d\\Resources"</c> を直書きしていた。
/// 本仓库ではパスをパラメータ化し、環境変数で差し替えられるようにする
/// （ハードコードパスは「既知の候補」の 1 つとしてのみ残す。上流由来のため）。
///
/// 解決順:
/// 1. 環境変数 <c>LIVE2D_MODEL_DIR</c>（+ <c>LIVE2D_MODEL_NAME</c>）
/// 2. <see cref="CandidateDirectories"/> の既知候補
/// 3. 実行ファイル隣の <c>Resources</c>（配布物を想定）
///
/// 見つからない場合も例外は投げない。<see cref="CubismLog.Warning"/> を出して false を返し、
/// 呼び出し側（ホスト）は空のシーンで起動を続ける。
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
    /// 既知の候補ディレクトリ。
    /// 先頭は上流 sample が直書きしていたパス（本仓库では候補の 1 つに降格）。
    /// </summary>
    public static IReadOnlyList<string> CandidateDirectories { get; } =
    [
        @"F:\live2d\Resources",
        @"D:\AgentGameWorkSpace\CubismSdkForNative-5-r.5\Samples\Resources",
    ];

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

        string? envDir = Environment.GetEnvironmentVariable(ModelDirEnvName);
        if (!string.IsNullOrWhiteSpace(envDir))
        {
            if (IsModelPresent(envDir, name))
            {
                dir = envDir;
                return true;
            }

            // 環境変数は最優先だが、誤指定でウィンドウが開かなくなるのは避けたいので候補へフォールバックする。
            CubismLog.Warning($"[Live2D App]{ModelDirEnvName}=\"{envDir}\" にモデル '{name}' が見つからないため、既知の候補を試します。");
        }

        foreach (string candidate in CandidateDirectories)
        {
            if (IsModelPresent(candidate, name))
            {
                dir = candidate;
                return true;
            }
        }

        string baseDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        if (IsModelPresent(baseDir, name))
        {
            dir = baseDir;
            return true;
        }

        dir = string.IsNullOrWhiteSpace(envDir) ? CandidateDirectories[0] : envDir;
        CubismLog.Warning(
            $"[Live2D App]モデル '{name}' が見つかりません。" +
            $"{ModelDirEnvName}={envDir ?? "(未設定)"}, 候補=[{string.Join(", ", CandidateDirectories)}], baseDir=\"{baseDir}\"。" +
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
