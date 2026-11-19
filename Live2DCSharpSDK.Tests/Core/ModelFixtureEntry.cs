namespace Live2DCSharpSDK.Tests.Core;

/// <summary>
/// 单条模型夹具元数据 (TestData/models.sha256 的一行).
/// 记录 moc3 的相对路径, SHA-256 与解析后的黄金计数, 供 B 档用例比对.
/// </summary>
internal sealed class ModelFixtureEntry
{
    /// <summary>
    /// 创建一条夹具元数据.
    /// </summary>
    /// <param name="relativePath">相对夹具根目录的路径 (以 '/' 分隔)</param>
    /// <param name="fullPath">夹具文件的完整路径</param>
    /// <param name="sha256">记录的小写 SHA-256</param>
    /// <param name="mocVersion">记录的 .moc3 版本</param>
    /// <param name="drawableCount">记录的 Drawable 数量</param>
    /// <param name="partCount">记录的 Part 数量</param>
    /// <param name="parameterCount">记录的 Parameter 数量</param>
    /// <param name="offscreenCount">记录的 Offscreen 数量</param>
    public ModelFixtureEntry(
        string relativePath,
        string fullPath,
        string sha256,
        uint mocVersion,
        int drawableCount,
        int partCount,
        int parameterCount,
        int offscreenCount)
    {
        RelativePath = relativePath;
        FullPath = fullPath;
        Sha256 = sha256;
        MocVersion = mocVersion;
        DrawableCount = drawableCount;
        PartCount = partCount;
        ParameterCount = parameterCount;
        OffscreenCount = offscreenCount;
    }

    /// <summary>
    /// 相对夹具根目录的路径 (以 '/' 分隔).
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// 夹具文件的完整路径.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// 记录的 SHA-256 (小写十六进制).
    /// </summary>
    public string Sha256 { get; }

    /// <summary>
    /// 记录的 .moc3 版本.
    /// </summary>
    public uint MocVersion { get; }

    /// <summary>
    /// 记录的 Drawable 数量.
    /// </summary>
    public int DrawableCount { get; }

    /// <summary>
    /// 记录的 Part 数量.
    /// </summary>
    public int PartCount { get; }

    /// <summary>
    /// 记录的 Parameter 数量.
    /// </summary>
    public int ParameterCount { get; }

    /// <summary>
    /// 记录的 Offscreen 数量.
    /// </summary>
    public int OffscreenCount { get; }
}
