using Live2DCSharpSDK.App;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.App;

/// <summary>
/// A10: <see cref="ModelResourceLocator"/> 的解析契约.
///
/// 覆盖环境变量优先 (含无效值回退), 向上最多 5 层的相对搜索 (第 5 层命中, 第 6 层不命中),
/// 尾分隔符, 相对起点与绝对路径结果, 以及非法起点与根目录起点不抛异常.
/// 每个修改环境变量的用例都在 finally 中恢复原值.
/// 全部用例只做目录与文件存在性判断, 不加载 Core, 属于 A 档.
/// </summary>
[TestClass]
public sealed class ModelResourceLocatorTests
{
    // 相对搜索的层数: 起点自身算第 0 层, 因此最多检查 MaxParentSearchDepth + 1 个位置.
    // 下面把 SDK 夹具分别放在 "起点上方 5 层" 与 "起点上方 6 层", 固定这个边界.

    /// <summary>
    /// 起点上方第 5 层 (含) 的 SDK 夹具能被找到, 且返回绝对路径.
    /// </summary>
    [TestMethod]
    public void FindSdkSamplesResources_FindsFixtureAtMaxDepth()
    {
        using TemporaryDirectory temp = new();
        string fixture = CreateSdkSamplesFixture(temp);

        // start 位于 root/a/b/c/d/e, 从 start 向上第 5 层正好是 root.
        string start = temp.CreateDirectory("a", "b", "c", "d", "e");

        string? found = ModelResourceLocator.FindSdkSamplesResources(start);
        if (found is null)
        {
            Assert.Fail("The fixture 5 levels above the start directory must be found.");
            return;
        }

        Assert.AreEqual(Path.GetFullPath(fixture), Path.GetFullPath(found));
        Assert.IsTrue(Path.IsPathRooted(found), "The locator must return an absolute path.");
    }

    /// <summary>
    /// 起点上方第 6 层的 SDK 夹具不会被找到 (搜索层数上限).
    /// </summary>
    [TestMethod]
    public void FindSdkSamplesResources_StopsAtMaxDepth()
    {
        using TemporaryDirectory temp = new();
        string fixture = CreateSdkSamplesFixture(temp);

        // start 位于 root/a/b/c/d/e/f, 从 start 向上第 6 层才是 root.
        string start = temp.CreateDirectory("a", "b", "c", "d", "e", "f");

        Assert.IsTrue(
            Directory.Exists(fixture),
            "The fixture must exist, otherwise this test proves nothing."
        );
        Assert.IsNull(
            ModelResourceLocator.FindSdkSamplesResources(start),
            "A fixture 6 levels above the start directory must not be found."
        );
    }

    /// <summary>
    /// 起点带尾分隔符时搜索结果与不带时一致.
    /// </summary>
    /// <remarks>
    /// 修复前把带尾分隔符的路径直接交给 Directory.GetParent: 它会回到同一层,
    /// 搜索层数因此少一层, 恰好位于第 5 层的夹具就会被漏掉.
    /// </remarks>
    [TestMethod]
    public void FindSdkSamplesResources_IgnoresTrailingSeparator()
    {
        using TemporaryDirectory temp = new();
        string fixture = CreateSdkSamplesFixture(temp);
        string start = temp.CreateDirectory("a", "b", "c", "d", "e");

        string? found = ModelResourceLocator.FindSdkSamplesResources(
            start + Path.DirectorySeparatorChar
        );
        if (found is null)
        {
            Assert.Fail("A trailing directory separator must not shift the search depth.");
            return;
        }

        Assert.AreEqual(Path.GetFullPath(fixture), Path.GetFullPath(found));
    }

    /// <summary>
    /// 相对起点会被规范化为绝对路径, 返回值也是绝对路径.
    /// </summary>
    [TestMethod]
    public void FindSdkSamplesResources_ResolvesRelativeStartToAbsolutePath()
    {
        using TemporaryDirectory temp = new();
        string fixture = CreateSdkSamplesFixture(temp);
        _ = temp.CreateDirectory("child");

        string previousDirectory = Environment.CurrentDirectory;
        string? found;
        try
        {
            // 临时切换当前目录以构造相对路径; finally 中恢复, 避免影响其它用例.
            Environment.CurrentDirectory = temp.Root;
            found = ModelResourceLocator.FindSdkSamplesResources("child");
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
        }

        if (found is null)
        {
            Assert.Fail("A relative start directory must be resolved to an absolute path.");
            return;
        }

        Assert.IsTrue(Path.IsPathRooted(found), "The locator must return an absolute path.");
        Assert.AreEqual(Path.GetFullPath(fixture), Path.GetFullPath(found));
    }

    /// <summary>
    /// 非法起点 (含 NUL 字符) 不抛异常, 只返回 null.
    /// </summary>
    [TestMethod]
    public void FindSdkSamplesResources_InvalidStartDirectoryDoesNotThrow()
    {
        using SilentLogScope logScope = new();

        // NUL 字符会让 Path.GetFullPath 抛 ArgumentException, 定位器必须吞掉它并返回 null.
        Assert.IsNull(ModelResourceLocator.FindSdkSamplesResources("invalid\0path"));
        Assert.IsNull(ModelResourceLocator.FindSdkSamplesResources("\0"));
    }

    /// <summary>
    /// 起点是文件系统根目录时, 向上搜索正常终止且不抛异常.
    /// </summary>
    [TestMethod]
    public void FindSdkSamplesResources_RootStartDirectoryDoesNotThrow()
    {
        using SilentLogScope logScope = new();

        string root = Path.GetPathRoot(Path.GetTempPath())
            ?? Path.DirectorySeparatorChar.ToString();

        // 根目录的父目录是 null: 这里只要求不抛异常, 不假设机器上是否存在 SDK 夹具.
        _ = ModelResourceLocator.FindSdkSamplesResources(root);
    }

    /// <summary>
    /// 环境变量指定的模型目录优先于相对搜索.
    /// </summary>
    [TestMethod]
    public void TryResolve_EnvironmentVariableTakesPriority()
    {
        using TemporaryDirectory temp = new();
        using SilentLogScope logScope = new();

        string modelDirectory = temp.CreateDirectory("Haru");
        temp.CreateFile(modelDirectory, "Haru.model3.json");

        string? previousDirectory = Environment.GetEnvironmentVariable(
            ModelResourceLocator.ModelDirEnvName
        );
        try
        {
            Environment.SetEnvironmentVariable(ModelResourceLocator.ModelDirEnvName, temp.Root);

            bool found = ModelResourceLocator.TryResolve("Haru", out string dir, out string name);

            Assert.IsTrue(found, "The model in the environment directory must be resolved.");
            Assert.AreEqual(temp.Root, dir);
            Assert.AreEqual("Haru", name);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ModelResourceLocator.ModelDirEnvName,
                previousDirectory
            );
        }
    }

    /// <summary>
    /// 环境变量指向不存在的目录时回退到相对搜索, 不抛异常.
    /// </summary>
    [TestMethod]
    public void TryResolve_InvalidEnvironmentDirectoryFallsBackWithoutThrowing()
    {
        using SilentLogScope logScope = new();

        string missingDirectory = Path.Combine(
            Path.GetTempPath(),
            "live2d-unittest-missing-" + Guid.NewGuid().ToString("N")
        );
        string modelName = "UnitTestMissingModel" + Guid.NewGuid().ToString("N");

        string? previousDirectory = Environment.GetEnvironmentVariable(
            ModelResourceLocator.ModelDirEnvName
        );
        string? previousName = Environment.GetEnvironmentVariable(
            ModelResourceLocator.ModelNameEnvName
        );
        try
        {
            Environment.SetEnvironmentVariable(
                ModelResourceLocator.ModelDirEnvName,
                missingDirectory
            );
            Environment.SetEnvironmentVariable(ModelResourceLocator.ModelNameEnvName, null);

            bool found = ModelResourceLocator.TryResolve(
                modelName,
                out string dir,
                out string name
            );

            // 唯一模型名保证相对搜索也不会命中: 验证不抛异常, 返回 false, 并保留最后尝试的候选路径.
            Assert.IsFalse(found);
            Assert.AreEqual(modelName, name);
            Assert.IsFalse(
                string.IsNullOrEmpty(dir),
                "TryResolve must report the last candidate path even when nothing is found."
            );
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ModelResourceLocator.ModelDirEnvName,
                previousDirectory
            );
            Environment.SetEnvironmentVariable(ModelResourceLocator.ModelNameEnvName, previousName);
        }
    }

    /// <summary>
    /// 参数为空时模型名取自环境变量 LIVE2D_MODEL_NAME.
    /// </summary>
    [TestMethod]
    public void TryResolve_ModelNameFromEnvironmentVariable()
    {
        using TemporaryDirectory temp = new();
        using SilentLogScope logScope = new();

        const string envModelName = "UnitTestEnvModel";
        string modelDirectory = temp.CreateDirectory(envModelName);
        temp.CreateFile(modelDirectory, envModelName + ".model3.json");

        string? previousDirectory = Environment.GetEnvironmentVariable(
            ModelResourceLocator.ModelDirEnvName
        );
        string? previousName = Environment.GetEnvironmentVariable(
            ModelResourceLocator.ModelNameEnvName
        );
        try
        {
            Environment.SetEnvironmentVariable(ModelResourceLocator.ModelDirEnvName, temp.Root);
            Environment.SetEnvironmentVariable(ModelResourceLocator.ModelNameEnvName, envModelName);

            bool found = ModelResourceLocator.TryResolve(null, out string dir, out string name);

            Assert.IsTrue(found, "The model name from the environment variable must be used.");
            Assert.AreEqual(envModelName, name);
            Assert.AreEqual(temp.Root, dir);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ModelResourceLocator.ModelDirEnvName,
                previousDirectory
            );
            Environment.SetEnvironmentVariable(ModelResourceLocator.ModelNameEnvName, previousName);
        }
    }

    /// <summary>
    /// 参数指定的模型名优先于环境变量 LIVE2D_MODEL_NAME.
    /// </summary>
    [TestMethod]
    public void TryResolve_ExplicitNameBeatsEnvironmentVariable()
    {
        using TemporaryDirectory temp = new();
        using SilentLogScope logScope = new();

        const string argumentModelName = "UnitTestArgumentModel";
        const string envModelName = "UnitTestEnvModel";
        string modelDirectory = temp.CreateDirectory(argumentModelName);
        temp.CreateFile(modelDirectory, argumentModelName + ".model3.json");

        string? previousDirectory = Environment.GetEnvironmentVariable(
            ModelResourceLocator.ModelDirEnvName
        );
        string? previousName = Environment.GetEnvironmentVariable(
            ModelResourceLocator.ModelNameEnvName
        );
        try
        {
            Environment.SetEnvironmentVariable(ModelResourceLocator.ModelDirEnvName, temp.Root);
            Environment.SetEnvironmentVariable(ModelResourceLocator.ModelNameEnvName, envModelName);

            bool found = ModelResourceLocator.TryResolve(
                argumentModelName,
                out string dir,
                out string name
            );

            Assert.IsTrue(found);
            Assert.AreEqual(
                argumentModelName,
                name,
                "The explicit model name must win over the environment variable."
            );
            Assert.AreEqual(temp.Root, dir);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ModelResourceLocator.ModelDirEnvName,
                previousDirectory
            );
            Environment.SetEnvironmentVariable(ModelResourceLocator.ModelNameEnvName, previousName);
        }
    }

    /// <summary>
    /// 参数与环境变量都为空时模型名回退到默认值 Haru.
    /// </summary>
    [TestMethod]
    public void TryResolve_DefaultModelNameIsHaru()
    {
        using SilentLogScope logScope = new();

        string? previousDirectory = Environment.GetEnvironmentVariable(
            ModelResourceLocator.ModelDirEnvName
        );
        string? previousName = Environment.GetEnvironmentVariable(
            ModelResourceLocator.ModelNameEnvName
        );
        try
        {
            Environment.SetEnvironmentVariable(ModelResourceLocator.ModelDirEnvName, null);
            Environment.SetEnvironmentVariable(ModelResourceLocator.ModelNameEnvName, null);

            // 能否找到模型取决于机器上的 SDK 样例, 这里只固定 "模型名缺省值是 Haru".
            _ = ModelResourceLocator.TryResolve(null, out _, out string name);

            Assert.AreEqual("Haru", name);
            Assert.AreEqual(ModelResourceLocator.DefaultModelName, name);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ModelResourceLocator.ModelDirEnvName,
                previousDirectory
            );
            Environment.SetEnvironmentVariable(ModelResourceLocator.ModelNameEnvName, previousName);
        }
    }

    /// <summary>
    /// 在临时目录下创建 SDK 样例目录的等价结构 (只建目录).
    /// </summary>
    /// <param name="temp">临时目录</param>
    /// <returns>Samples/Resources 的绝对路径</returns>
    private static string CreateSdkSamplesFixture(TemporaryDirectory temp)
    {
        return temp.CreateDirectory(
            ModelResourceLocator.SdkPackageDirectoryName,
            "Samples",
            "Resources"
        );
    }
}
