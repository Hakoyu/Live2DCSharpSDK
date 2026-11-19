using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests;

/// <summary>
/// 测试程序集的全局状态夹具.
///
/// 本程序集不使用 <c>[assembly: Parallelize]</c>, MSTest 默认串行执行, 因此环境变量,
/// CubismContext.Current 与 Core 日志回调等全局状态不会互相干扰.
/// <see cref="AssemblyInit"/> 记录环境变量快照并校验夹具环境,
/// <see cref="AssemblyCleanup"/> 把环境变量恢复为快照值.
/// A 档 (纯托管) 只记录不强制; 需要真实 Core 的 B 档缺少环境时直接失败, 不静默跳过.
/// </summary>
[TestClass]
public static class GlobalStateFixture
{
    /// <summary>
    /// 模型夹具根目录的环境变量名 (moc3 不入库, 见 PLAN-06 §3).
    /// </summary>
    public const string ModelsRootVariable = "LIVE2D_TEST_MODELS";

    /// <summary>
    /// 分档环境变量名, 由 <c>tools/run-tests.sh</c> 写入 a / core / all.
    /// </summary>
    public const string TierVariable = "LIVE2D_TEST_TIER";

    /// <summary>
    /// B 档 (需要 Live2DCubismCore.dll 与 moc3 夹具) 的分档名.
    /// </summary>
    public const string CoreTier = "core";

    private const string CoreLibraryFileName = "Live2DCubismCore.dll";

    private static string? s_modelsRootSnapshot;
    private static string? s_tierSnapshot;

    /// <summary>
    /// 模型夹具根目录; 环境变量未设置或目录不存在时为 null.
    /// </summary>
    public static string? ModelsRoot { get; private set; }

    /// <summary>
    /// Core 原生库的完整路径; 输出目录与 runtimes 子目录都没有时, 或平台不是 Windows x64 时为 null.
    /// </summary>
    public static string? CoreLibraryPath { get; private set; }

    /// <summary>
    /// Core 原生库能否在当前进程加载 (只做加载探测, 随即释放, 不调用任何 Core API).
    /// </summary>
    public static bool IsCoreLibraryAvailable { get; private set; }

    /// <summary>
    /// 本次运行的分档, 取自 <see cref="TierVariable"/>, 未设置时为 all.
    /// </summary>
    public static string Tier { get; private set; } = "all";

    /// <summary>
    /// 本次运行是否为需要 Core 的 B 档.
    /// </summary>
    public static bool IsCoreTier => string.Equals(Tier, CoreTier, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 记录环境变量快照, 校验夹具环境并输出环境摘要.
    /// </summary>
    /// <param name="context">MSTest 提供的测试上下文, 用于输出环境摘要</param>
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        s_modelsRootSnapshot = Environment.GetEnvironmentVariable(ModelsRootVariable);
        s_tierSnapshot = Environment.GetEnvironmentVariable(TierVariable);
        Tier = s_tierSnapshot ?? "all";

        ModelsRoot = ResolveModelsRoot(s_modelsRootSnapshot);
        CoreLibraryPath = ResolveCoreLibraryPath();
        IsCoreLibraryAvailable = CoreLibraryPath is not null && TryProbeCoreLibrary(CoreLibraryPath);

        context.WriteLine($"[GlobalStateFixture] tier: {Tier}");
        context.WriteLine($"[GlobalStateFixture] {ModelsRootVariable}: {ModelsRoot ?? "<unset>"}");
        context.WriteLine($"[GlobalStateFixture] {CoreLibraryFileName}: {CoreLibraryPath ?? "<not found>"}");
        context.WriteLine($"[GlobalStateFixture] core loadable: {IsCoreLibraryAvailable}");

        if (IsCoreTier)
        {
            RequireCoreEnvironment();
        }
    }

    /// <summary>
    /// 把环境变量恢复为 <see cref="AssemblyInit"/> 记录的快照值 (快照为 null 时删除变量).
    /// </summary>
    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        Environment.SetEnvironmentVariable(ModelsRootVariable, s_modelsRootSnapshot);
        Environment.SetEnvironmentVariable(TierVariable, s_tierSnapshot);
    }

    /// <summary>
    /// 取模型夹具根目录, 缺失时抛出明确消息, 供 B 档用例调用.
    /// </summary>
    /// <returns>模型夹具根目录</returns>
    /// <exception cref="InvalidOperationException">环境变量未设置或目录不存在时抛出</exception>
    public static string RequireModelsRoot()
    {
        return ModelsRoot ?? throw new InvalidOperationException(
            $"The environment variable {ModelsRootVariable} is not set or does not point to an existing directory.");
    }

    /// <summary>
    /// 取指定模型文件, 缺失时抛出明确消息, 供 B 档用例调用.
    /// </summary>
    /// <param name="modelDirectory">模型目录名, 例如 Haru</param>
    /// <param name="fileName">文件名, 例如 Haru.moc3</param>
    /// <returns>模型文件的完整路径</returns>
    /// <exception cref="InvalidOperationException">模型文件不存在时抛出</exception>
    public static string RequireModelFile(string modelDirectory, string fileName)
    {
        string path = Path.Combine(RequireModelsRoot(), modelDirectory, fileName);
        if (File.Exists(path) is false)
        {
            throw new InvalidOperationException($"The model file '{path}' does not exist.");
        }

        return path;
    }

    /// <summary>
    /// 取 Core 原生库路径, 不可用时抛出明确消息, 供 B 档用例调用.
    /// </summary>
    /// <returns>Core 原生库的完整路径</returns>
    /// <exception cref="InvalidOperationException">Core 原生库不可用时抛出</exception>
    public static string RequireCoreLibraryPath()
    {
        if (IsCoreLibraryAvailable is false || CoreLibraryPath is null)
        {
            throw new InvalidOperationException(
                $"{CoreLibraryFileName} is not loadable on this platform (path: {CoreLibraryPath ?? "<not found>"}).");
        }

        return CoreLibraryPath;
    }

    /// <summary>
    /// 以环境变量解析模型夹具根目录; 目录不存在时返回 null.
    /// </summary>
    /// <param name="value">环境变量值</param>
    /// <returns>存在的目录路径, 否则为 null</returns>
    private static string? ResolveModelsRoot(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Directory.Exists(value) ? value : null;
    }

    /// <summary>
    /// 在测试输出目录里定位 Core 原生库.
    /// 平台不是 Windows x64 时返回 null, 因为仓库里的原生库只提供 win-x64 版本 (见 PLAN-06 §2).
    /// </summary>
    /// <returns>原生库路径, 找不到时为 null</returns>
    private static string? ResolveCoreLibraryPath()
    {
        if (OperatingSystem.IsWindows() is false || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return null;
        }

        string baseDirectory = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDirectory, CoreLibraryFileName),
            Path.Combine(baseDirectory, "runtimes", "win-x64", "native", CoreLibraryFileName)
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// 探测 Core 原生库能否加载; 只做加载与释放, 不调用任何导出函数.
    /// </summary>
    /// <param name="path">原生库路径</param>
    /// <returns>可加载时为 true</returns>
    private static bool TryProbeCoreLibrary(string path)
    {
        try
        {
            if (NativeLibrary.TryLoad(path, out IntPtr handle) is false)
            {
                return false;
            }

            NativeLibrary.Free(handle);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// B 档的运行前校验: 平台, 模型夹具与 Core 原生库都必须是可用的.
    /// 用失败而不是跳过, 避免出现 "CI 绿但没跑" 的假象 (见 PLAN-06 §2).
    /// </summary>
    private static void RequireCoreEnvironment()
    {
        if (OperatingSystem.IsWindows() is false)
        {
            Assert.Fail($"The core tier requires Windows x64, but the current OS is {RuntimeInformation.OSDescription}.");
        }

        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            Assert.Fail($"The core tier requires an x64 process, but the current architecture is {RuntimeInformation.ProcessArchitecture}.");
        }

        if (ModelsRoot is null)
        {
            Assert.Fail($"The environment variable {ModelsRootVariable} is not set or does not point to an existing directory.");
        }

        if (IsCoreLibraryAvailable is false)
        {
            Assert.Fail($"{CoreLibraryFileName} could not be loaded from '{CoreLibraryPath ?? "<not found>"}'.");
        }

        RequireModelFile("Haru", "Haru.moc3");
        RequireModelFile("Ren", "Ren.moc3");
    }
}
