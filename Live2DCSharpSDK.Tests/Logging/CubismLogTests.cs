using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Tests.Context;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Logging;

/// <summary>
/// <see cref="CubismLog"/> 的上下文隔离行为.
/// 通过不调 Core 的测试钩子设置 CubismContext.Current, 验证两个上下文的日志级别与输出互不串扰,
/// 以及没有当前上下文时回落到 CubismContext.Default. 所有用例都用 try/finally 恢复现场.
/// </summary>
[TestClass]
public sealed class CubismLogTests
{
    /// <summary>
    /// 测试用的空日志函数, 避免任何路径回落到 Core 日志回调.
    /// </summary>
    private static readonly LogFunction s_noopLog = static _ => { };

    /// <summary>
    /// 有当前上下文时, 日志级别读写都落在该上下文, 两个上下文互不影响.
    /// </summary>
    [TestMethod]
    public void LogLevel_WithCurrent_IsolatedBetweenContexts()
    {
        CubismContext? previous = CubismContextCurrentHook.GetCurrent();
        CubismContext warningContext = CreateContext(LogLevel.Warning);
        CubismContext debugContext = CreateContext(LogLevel.Debug);
        try
        {
            CubismContextCurrentHook.SetCurrent(warningContext);
            Assert.AreSame(warningContext, CubismContext.Current);
            Assert.AreEqual(LogLevel.Warning, CubismLog.LogLevel);

            CubismLog.LogLevel = LogLevel.Error;

            Assert.AreEqual(LogLevel.Error, warningContext.LogLevel);
            Assert.AreEqual(LogLevel.Debug, debugContext.LogLevel);

            CubismContextCurrentHook.SetCurrent(debugContext);
            Assert.AreEqual(LogLevel.Debug, CubismLog.LogLevel);

            CubismLog.LogLevel = LogLevel.Verbose;

            Assert.AreEqual(LogLevel.Verbose, debugContext.LogLevel);
            Assert.AreEqual(LogLevel.Error, warningContext.LogLevel);
        }
        finally
        {
            CubismContextCurrentHook.SetCurrent(previous);
            warningContext.Dispose();
            debugContext.Dispose();
        }
    }

    /// <summary>
    /// 有当前上下文时, SetLogOutput 只写入该上下文.
    /// </summary>
    [TestMethod]
    public void SetLogOutput_WithCurrent_IsolatedBetweenContexts()
    {
        CubismContext? previous = CubismContextCurrentHook.GetCurrent();
        CubismContext firstContext = CreateContext(LogLevel.Info);
        CubismContext secondContext = CreateContext(LogLevel.Info);
        List<string> firstMessages = [];
        List<string> secondMessages = [];
        LogFunction firstOutput = firstMessages.Add;
        LogFunction secondOutput = secondMessages.Add;
        try
        {
            CubismContextCurrentHook.SetCurrent(firstContext);
            CubismLog.SetLogOutput(firstOutput);

            Assert.AreSame(firstOutput, firstContext.LogOutput);
            Assert.AreNotSame(firstOutput, secondContext.LogOutput);

            CubismContextCurrentHook.SetCurrent(secondContext);
            CubismLog.SetLogOutput(secondOutput);

            Assert.AreSame(secondOutput, secondContext.LogOutput);
            Assert.AreSame(firstOutput, firstContext.LogOutput);
        }
        finally
        {
            CubismContextCurrentHook.SetCurrent(previous);
            firstContext.Dispose();
            secondContext.Dispose();
        }
    }

    /// <summary>
    /// 日志输出与级别过滤只作用于当前上下文, 另一个上下文收不到任何消息.
    /// </summary>
    [TestMethod]
    public void LogOutput_WithCurrent_DoesNotCrossTalk()
    {
        CubismContext? previous = CubismContextCurrentHook.GetCurrent();
        List<string> firstMessages = [];
        List<string> secondMessages = [];
        CubismContext firstContext = CreateContext(LogLevel.Warning, firstMessages.Add);
        CubismContext secondContext = CreateContext(LogLevel.Debug, secondMessages.Add);
        try
        {
            CubismContextCurrentHook.SetCurrent(firstContext);

            // Warning 级别下 Info 被过滤, 只有 Warning 会输出到第一个上下文的函数.
            CubismLog.Info("filtered-{0}", 1);
            CubismLog.Warning("warn-{0}", 1);

            Assert.AreEqual(1, firstMessages.Count);
            Assert.AreEqual("[CSM] [W] warn-1", firstMessages[0]);
            Assert.AreEqual(0, secondMessages.Count);

            CubismContextCurrentHook.SetCurrent(secondContext);

            // Debug 级别下 Info / Debug 输出, Verbose 被过滤, 且不会写回第一个上下文.
            CubismLog.Info("info-{0}", 2);
            CubismLog.Debug("debug-{0}", 3);
            CubismLog.Verbose("verbose-{0}", 4);

            Assert.AreEqual(2, secondMessages.Count);
            Assert.AreEqual("[CSM] [I] info-2", secondMessages[0]);
            Assert.AreEqual("[CSM] [D] debug-3", secondMessages[1]);
            Assert.AreEqual(1, firstMessages.Count);
            Assert.AreEqual("[CSM] [W] warn-1", firstMessages[0]);
        }
        finally
        {
            CubismContextCurrentHook.SetCurrent(previous);
            firstContext.Dispose();
            secondContext.Dispose();
        }
    }

    /// <summary>
    /// 没有当前上下文时, 日志级别回落到 CubismContext.Default, 读写都指向它.
    /// </summary>
    [TestMethod]
    public void NoCurrent_LogLevelFallsBackToDefault()
    {
        CubismContext? previous = CubismContextCurrentHook.GetCurrent();
        LogLevel previousLevel = CubismContext.Default.LogLevel;
        try
        {
            CubismContextCurrentHook.SetCurrent(null);
            Assert.IsNull(CubismContext.Current);

            CubismContext.Default.LogLevel = LogLevel.Debug;
            Assert.AreEqual(LogLevel.Debug, CubismLog.LogLevel);

            CubismLog.LogLevel = LogLevel.Error;
            Assert.AreEqual(LogLevel.Error, CubismContext.Default.LogLevel);
        }
        finally
        {
            CubismContextCurrentHook.SetCurrent(previous);
            CubismContext.Default.LogLevel = previousLevel;
        }
    }

    /// <summary>
    /// 没有当前上下文时, 日志输出回落到 CubismContext.Default 的输出函数.
    /// </summary>
    [TestMethod]
    public void NoCurrent_LogOutputFallsBackToDefault()
    {
        CubismContext? previous = CubismContextCurrentHook.GetCurrent();
        LogLevel previousLevel = CubismContext.Default.LogLevel;
        LogFunction? previousOutput = CubismContext.Default.LogOutput;
        List<string> messages = [];
        try
        {
            CubismContextCurrentHook.SetCurrent(null);
            CubismContext.Default.LogLevel = LogLevel.Debug;
            LogFunction output = messages.Add;

            CubismLog.SetLogOutput(output);

            Assert.AreSame(output, CubismContext.Default.LogOutput);

            CubismLog.Info("default-{0}", 7);

            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("[CSM] [I] default-7", messages[0]);
        }
        finally
        {
            CubismContextCurrentHook.SetCurrent(previous);
            CubismContext.Default.LogOutput = previousOutput;
            CubismContext.Default.LogLevel = previousLevel;
        }
    }

    /// <summary>
    /// 创建一个绑定空分配器与指定日志级别的上下文.
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <returns>尚未启动的上下文实例</returns>
    private static CubismContext CreateContext(LogLevel level)
    {
        return CreateContext(level, s_noopLog);
    }

    /// <summary>
    /// 创建一个绑定空分配器, 指定日志级别与日志输出函数的上下文.
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="logFunction">日志输出函数</param>
    /// <returns>尚未启动的上下文实例</returns>
    private static CubismContext CreateContext(LogLevel level, LogFunction logFunction)
    {
        CubismOption option = new()
        {
            LogFunction = logFunction,
            LoggingLevel = level
        };

        return CubismContext.Create(new TestAllocator(), option);
    }
}
