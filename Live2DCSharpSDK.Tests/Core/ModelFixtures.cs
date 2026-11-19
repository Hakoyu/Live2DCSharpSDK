using System.Globalization;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Core;

/// <summary>
/// B 档模型夹具的元数据与完整性校验.
///
/// moc3 本体不入库, 夹具根目录由 <see cref="GlobalStateFixture.ModelsRootVariable"/> 指定;
/// 这里读取随测试输出复制的 TestData/models.sha256, 在加载模型之前校验 SHA-256.
/// 环境变量缺失或哈希不符时用 <see cref="Assert.Fail(string)"/> 给出明确消息,
/// 而不是让用例静默跳过 (见 PLAN-06 §2 / §3).
/// </summary>
internal static class ModelFixtures
{
    /// <summary>
    /// Haru 样例模型的相对路径 (相对夹具根目录).
    /// </summary>
    public const string HaruRelativePath = "Haru/Haru.moc3";

    /// <summary>
    /// Ren 样例模型的相对路径 (相对夹具根目录).
    /// </summary>
    public const string RenRelativePath = "Ren/Ren.moc3";

    private const string MetadataDirectoryName = "TestData";
    private const string MetadataFileName = "models.sha256";

    /// <summary>
    /// 读取指定相对路径的夹具条目, 并校验文件存在与 SHA-256.
    /// </summary>
    /// <param name="relativePath">相对夹具根目录的路径 (以 '/' 分隔)</param>
    /// <returns>校验通过的夹具条目</returns>
    /// <exception cref="ArgumentException">路径为空时抛出</exception>
    public static ModelFixtureEntry Require(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("The fixture path must not be empty.", nameof(relativePath));
        }

        IReadOnlyDictionary<string, ModelFixtureEntry> entries = LoadEntries();
        entries.TryGetValue(relativePath, out ModelFixtureEntry? entry);
        Assert.IsNotNull(entry, $"The fixture metadata does not contain an entry for '{relativePath}'.");

        AssertSha256(entry);
        return entry;
    }

    /// <summary>
    /// 读取夹具文件的全部字节, 文件不存在时用明确消息失败.
    /// </summary>
    /// <param name="entry">夹具条目</param>
    /// <returns>moc3 文件的全部字节</returns>
    public static byte[] ReadBytes(ModelFixtureEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Assert.IsTrue(
            File.Exists(entry.FullPath),
            $"The model file '{entry.FullPath}' does not exist. " +
            $"Check that {GlobalStateFixture.ModelsRootVariable} points to the Cubism SDK Samples/Resources directory.");

        return File.ReadAllBytes(entry.FullPath);
    }

    /// <summary>
    /// 校验夹具文件的 SHA-256 与元数据记录值一致.
    /// </summary>
    /// <param name="entry">夹具条目</param>
    public static void AssertSha256(ModelFixtureEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Assert.IsTrue(
            File.Exists(entry.FullPath),
            $"The model file '{entry.FullPath}' does not exist. " +
            $"Check that {GlobalStateFixture.ModelsRootVariable} points to the Cubism SDK Samples/Resources directory.");

        string actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(entry.FullPath)));
        if (string.Equals(entry.Sha256, actual, StringComparison.OrdinalIgnoreCase) is false)
        {
            Assert.Fail(
                $"SHA-256 mismatch for '{entry.FullPath}': recorded {entry.Sha256}, actual {actual}. " +
                "The fixture is not the model recorded in TestData/models.sha256.");
        }
    }

    /// <summary>
    /// 解析 TestData/models.sha256, 并解析出每个夹具文件的完整路径.
    /// </summary>
    /// <returns>以相对路径为键的夹具条目</returns>
    private static IReadOnlyDictionary<string, ModelFixtureEntry> LoadEntries()
    {
        string root = RequireModelsRoot();
        string metadataPath = Path.Combine(AppContext.BaseDirectory, MetadataDirectoryName, MetadataFileName);
        Assert.IsTrue(
            File.Exists(metadataPath),
            $"The fixture metadata file '{metadataPath}' does not exist. Build the test project so that TestData is copied to the output directory.");

        Dictionary<string, ModelFixtureEntry> entries = new(StringComparer.Ordinal);
        int lineNumber = 0;
        foreach (string rawLine in File.ReadLines(metadataPath))
        {
            lineNumber++;
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            ModelFixtureEntry entry = ParseEntry(line, metadataPath, lineNumber, root);
            if (entries.ContainsKey(entry.RelativePath))
            {
                Assert.Fail($"Duplicate fixture entry '{entry.RelativePath}' at {metadataPath}:{lineNumber}.");
            }

            entries.Add(entry.RelativePath, entry);
        }

        return entries;
    }

    /// <summary>
    /// 解析一行夹具元数据.
    /// </summary>
    /// <param name="line">去掉首尾空白的行</param>
    /// <param name="metadataPath">元数据文件路径, 仅用于错误消息</param>
    /// <param name="lineNumber">行号, 仅用于错误消息</param>
    /// <param name="root">夹具根目录</param>
    /// <returns>夹具条目</returns>
    private static ModelFixtureEntry ParseEntry(string line, string metadataPath, int lineNumber, string root)
    {
        string[] tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        Assert.IsTrue(tokens.Length > 1, $"Invalid fixture metadata at {metadataPath}:{lineNumber}: no key=value pairs.");

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        for (int index = 1; index < tokens.Length; index++)
        {
            int separator = tokens[index].IndexOf('=');
            if (separator <= 0 || separator == tokens[index].Length - 1)
            {
                Assert.Fail($"Invalid fixture metadata at {metadataPath}:{lineNumber}: '{tokens[index]}' is not a key=value pair.");
                continue;
            }

            values[tokens[index][..separator]] = tokens[index][(separator + 1)..];
        }

        string relativePath = tokens[0];
        string fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string sha256 = RequireValue(values, "sha256", metadataPath, lineNumber);
        uint mocVersion = ParseUInt32(values, "mocVersion", metadataPath, lineNumber);
        int drawableCount = ParseInt32(values, "drawables", metadataPath, lineNumber);
        int partCount = ParseInt32(values, "parts", metadataPath, lineNumber);
        int parameterCount = ParseInt32(values, "parameters", metadataPath, lineNumber);
        int offscreenCount = ParseInt32(values, "offscreens", metadataPath, lineNumber);

        return new ModelFixtureEntry(
            relativePath,
            fullPath,
            sha256,
            mocVersion,
            drawableCount,
            partCount,
            parameterCount,
            offscreenCount);
    }

    /// <summary>
    /// 取夹具根目录, 缺失时明确失败.
    /// </summary>
    /// <returns>存在的夹具根目录</returns>
    private static string RequireModelsRoot()
    {
        string? root = GlobalStateFixture.ModelsRoot;
        if (root is null)
        {
            Assert.Fail(
                $"The environment variable {GlobalStateFixture.ModelsRootVariable} is not set or does not point to an existing directory. " +
                "The core tier needs the moc3 fixtures from the Cubism SDK Samples/Resources directory (see TestData/README.md).");
        }

        return root!;
    }

    /// <summary>
    /// 取必填的字符串值.
    /// </summary>
    /// <param name="values">已解析的键值对</param>
    /// <param name="key">键名</param>
    /// <param name="metadataPath">元数据文件路径, 仅用于错误消息</param>
    /// <param name="lineNumber">行号, 仅用于错误消息</param>
    /// <returns>键对应的值</returns>
    private static string RequireValue(Dictionary<string, string> values, string key, string metadataPath, int lineNumber)
    {
        values.TryGetValue(key, out string? value);
        Assert.IsNotNull(value, $"Invalid fixture metadata at {metadataPath}:{lineNumber}: missing '{key}'.");

        return value;
    }

    /// <summary>
    /// 解析必填的无符号整数值.
    /// </summary>
    /// <param name="values">已解析的键值对</param>
    /// <param name="key">键名</param>
    /// <param name="metadataPath">元数据文件路径, 仅用于错误消息</param>
    /// <param name="lineNumber">行号, 仅用于错误消息</param>
    /// <returns>解析结果</returns>
    private static uint ParseUInt32(Dictionary<string, string> values, string key, string metadataPath, int lineNumber)
    {
        string value = RequireValue(values, key, metadataPath, lineNumber);
        if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint result) is false)
        {
            Assert.Fail($"Invalid fixture metadata at {metadataPath}:{lineNumber}: '{key}' must be an unsigned integer, but was '{value}'.");
        }

        return result;
    }

    /// <summary>
    /// 解析必填的整数值.
    /// </summary>
    /// <param name="values">已解析的键值对</param>
    /// <param name="key">键名</param>
    /// <param name="metadataPath">元数据文件路径, 仅用于错误消息</param>
    /// <param name="lineNumber">行号, 仅用于错误消息</param>
    /// <returns>解析结果</returns>
    private static int ParseInt32(Dictionary<string, string> values, string key, string metadataPath, int lineNumber)
    {
        string value = RequireValue(values, key, metadataPath, lineNumber);
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) is false)
        {
            Assert.Fail($"Invalid fixture metadata at {metadataPath}:{lineNumber}: '{key}' must be an integer, but was '{value}'.");
        }

        return result;
    }
}
