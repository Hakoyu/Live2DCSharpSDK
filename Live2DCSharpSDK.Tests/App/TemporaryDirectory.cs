namespace Live2DCSharpSDK.Tests.App;

/// <summary>
/// 测试用的临时目录.
///
/// 创建在本机临时目录下的唯一子目录, 释放时递归删除.
/// 用例通过 using 持有它, 保证断言失败时也会清理 (环境变量与文件系统都属于全局状态).
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    /// <summary>
    /// 创建临时目录.
    /// </summary>
    public TemporaryDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "live2d-unittest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>
    /// 临时目录的绝对路径.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// 在临时目录下按顺序创建多层子目录.
    /// </summary>
    /// <param name="segments">相对临时目录的目录名</param>
    /// <returns>最深层目录的绝对路径</returns>
    public string CreateDirectory(params string[] segments)
    {
        string current = Root;
        foreach (string segment in segments)
        {
            current = Path.Combine(current, segment);
            Directory.CreateDirectory(current);
        }

        return current;
    }

    /// <summary>
    /// 在指定目录下写入一个占位文件 (定位器只关心文件是否存在).
    /// </summary>
    /// <param name="directory">文件所在目录</param>
    /// <param name="fileName">文件名</param>
    /// <returns>文件的绝对路径</returns>
    public string CreateFile(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "{}");
        return path;
    }

    /// <summary>
    /// 递归删除临时目录; 清理失败不改变测试结论.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
        catch (IOException)
        {
            // 文件被短暂占用时忽略: 临时目录由操作系统回收.
        }
        catch (UnauthorizedAccessException)
        {
            // 同上, 清理失败不得掩盖断言结果.
        }
    }
}
