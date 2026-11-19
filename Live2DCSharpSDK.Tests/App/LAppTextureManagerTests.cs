using System.Runtime.InteropServices;
using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;

namespace Live2DCSharpSDK.Tests.App;

/// <summary>
/// A12: <see cref="LAppTextureManager"/> 的解码失败契约.
///
/// 覆盖文件不存在, 内容不是图片, 空文件与损坏 PNG: 解码方法返回 null, 输出包含路径与原因的
/// Warning, 不抛空引用异常, 也不创建纹理; 同时固定占位纹理的 1x1 尺寸与索引, 以及解码失败
/// 不进入纹理缓存的约定. 全部用例只使用 SkiaSharp 与托管层, 不加载 Core, 属于 A 档.
/// </summary>
[TestClass]
public sealed class LAppTextureManagerTests
{
    /// <summary>
    /// 用例使用的纹理单元编号, 取非 0 值以便暴露索引被重置为 0 的问题.
    /// </summary>
    private const int TextureIndex = 2;

    /// <summary>
    /// 文件不存在时返回 null, 输出包含路径的 Warning, 且不创建纹理.
    /// </summary>
    [TestMethod]
    public void CreateTextureFromPngFile_WhenFileMissing_ReturnsNullAndWarns()
    {
        using TemporaryDirectory temp = new();
        string fileName = Path.Combine(temp.Root, "missing.png");
        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);

        using LogCaptureScope log = new();

        // 纹理管理器不读取 model, 这里传入 null 以避免加载真实模型.
        TextureInfo? info = manager.CreateTextureFromPngFile(null!, TextureIndex, fileName);

        Assert.IsNull(info);
        Assert.AreEqual(
            0,
            lapp.CreateTextureCalls.Count,
            "A missing file must not create a texture."
        );
        AssertSingleWarning(log, fileName, "index 2");
    }

    /// <summary>
    /// 内容是文本的假 .png 文件返回 null, 输出包含路径的 Warning, 且不创建纹理.
    /// </summary>
    [TestMethod]
    public void CreateTextureFromPngFile_WhenContentIsNotImage_ReturnsNullAndWarns()
    {
        using TemporaryDirectory temp = new();
        string fileName = Path.Combine(temp.Root, "text.png");
        File.WriteAllText(fileName, "this is not an image");
        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);

        using LogCaptureScope log = new();

        TextureInfo? info = manager.CreateTextureFromPngFile(null!, TextureIndex, fileName);

        Assert.IsNull(info);
        Assert.AreEqual(0, lapp.CreateTextureCalls.Count, "A text file must not create a texture.");
        AssertSingleWarning(log, fileName, "index 2");
    }

    /// <summary>
    /// 0 字节的空文件返回 null, 输出包含路径的 Warning, 且不创建纹理.
    /// </summary>
    [TestMethod]
    public void CreateTextureFromPngFile_WhenFileIsEmpty_ReturnsNullAndWarns()
    {
        using TemporaryDirectory temp = new();
        string fileName = Path.Combine(temp.Root, "empty.png");
        File.WriteAllBytes(fileName, []);
        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);

        using LogCaptureScope log = new();

        TextureInfo? info = manager.CreateTextureFromPngFile(null!, TextureIndex, fileName);

        Assert.IsNull(info);
        Assert.AreEqual(
            0,
            lapp.CreateTextureCalls.Count,
            "An empty file must not create a texture."
        );
        AssertSingleWarning(log, fileName, "index 2");
    }

    /// <summary>
    /// 只有 PNG 签名的损坏文件不会抛异常: 要么返回 null 并输出 Warning, 要么解出纹理.
    /// 这条用例守住 "损坏纹理不得让模型加载崩溃" 的底线.
    /// </summary>
    [TestMethod]
    public void CreateTextureFromPngFile_WhenPngIsTruncated_DoesNotThrow()
    {
        using TemporaryDirectory temp = new();
        string fileName = CreatePng(temp, "truncated.png", 2, 3);
        byte[] pngBytes = File.ReadAllBytes(fileName);
        File.WriteAllBytes(fileName, pngBytes[..8]);

        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);

        using LogCaptureScope log = new();

        TextureInfo? info = manager.CreateTextureFromPngFile(null!, TextureIndex, fileName);

        Assert.IsNull(info);
        AssertSingleWarning(log, fileName, "index 2");
        Assert.AreEqual(0, lapp.CreateTextureCalls.Count);
    }

    /// <summary>
    /// 正常 PNG 解码后返回与文件一致的尺寸与索引, 并把像素指针交给 CreateTexture.
    /// </summary>
    [TestMethod]
    public void CreateTextureFromPngFile_WhenPngIsValid_ReturnsDecodedTexture()
    {
        using TemporaryDirectory temp = new();
        string fileName = CreatePng(temp, "valid.png", 2, 3);
        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);

        using LogCaptureScope log = new();

        TextureInfo? info = manager.CreateTextureFromPngFile(null!, TextureIndex, fileName);
        if (info is null)
        {
            Assert.Fail("A valid PNG must be decoded.");
            return;
        }

        Assert.AreEqual(TextureIndex, info.Index);
        Assert.AreEqual(2, info.Width);
        Assert.AreEqual(3, info.Height);
        Assert.AreEqual(fileName, info.FileName);
        Assert.AreEqual(0, log.Messages.Count, string.Join(" | ", log.Messages));

        Assert.AreEqual(1, lapp.CreateTextureCalls.Count);
        TextureCall call = lapp.CreateTextureCalls[0];
        Assert.AreEqual(TextureIndex, call.Index);
        Assert.AreEqual(2, call.Width);
        Assert.AreEqual(3, call.Height);
        Assert.IsTrue(call.HasPixelBuffer, "The decoded bitmap must expose a pixel buffer.");
        Assert.AreNotEqual(0, call.FirstPixel, "The first pixel must come from the decoded image.");
    }

    /// <summary>
    /// 同一模型与纹理单元第二次读取命中缓存, 不再创建第二张纹理.
    /// </summary>
    [TestMethod]
    public void CreateTextureFromPngFile_WhenSameModelAndIndexTwice_ReturnsCachedTexture()
    {
        using TemporaryDirectory temp = new();
        string fileName = CreatePng(temp, "cached.png", 2, 3);
        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);

        using LogCaptureScope log = new();

        TextureInfo? first = manager.CreateTextureFromPngFile(null!, TextureIndex, fileName);
        TextureInfo? second = manager.CreateTextureFromPngFile(null!, TextureIndex, fileName);

        Assert.IsNotNull(first);
        Assert.AreSame(first, second, "The same file must reuse the loaded texture.");
        Assert.AreEqual(1, lapp.CreateTextureCalls.Count);
        Assert.AreEqual(TextureIndex, first.Index, "The cached texture keeps its original index.");
    }

    /// <summary>
    /// 同一文件绑定到不同纹理单元时分别创建, 避免把第一个单元的绑定复用到第二个单元.
    /// </summary>
    [TestMethod]
    public void CreateTextureFromPngFile_WhenSameFileUsesDifferentIndex_CreatesTexturePerIndex()
    {
        using TemporaryDirectory temp = new();
        string fileName = CreatePng(temp, "shared-index.png", 2, 3);
        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);

        TextureInfo? first = manager.CreateTextureFromPngFile(null!, TextureIndex, fileName);
        TextureInfo? second = manager.CreateTextureFromPngFile(null!, TextureIndex + 1, fileName);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(2, lapp.CreateTextureCalls.Count);
        Assert.AreEqual(TextureIndex, lapp.CreateTextureCalls[0].Index);
        Assert.AreEqual(TextureIndex + 1, lapp.CreateTextureCalls[1].Index);
    }

    /// <summary>
    /// 同一文件被不同模型使用时分别创建, 避免共享后端纹理所属模型不一致.
    /// </summary>
    [TestMethod]
    public void CreateTextureFromPngFile_WhenSameFileUsesDifferentModel_CreatesTexturePerModel()
    {
        using TemporaryDirectory temp = new();
        string fileName = CreatePng(temp, "shared-model.png", 2, 3);
        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);
        LAppModel firstModel = CreateTestModel();
        LAppModel secondModel = CreateTestModel();

        TextureInfo? first = manager.CreateTextureFromPngFile(firstModel, TextureIndex, fileName);
        TextureInfo? second = manager.CreateTextureFromPngFile(secondModel, TextureIndex, fileName);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(2, lapp.CreateTextureCalls.Count);
    }

    /// <summary>
    /// 解码失败不进入缓存: 每次调用都返回 null 并重新输出 Warning.
    /// </summary>
    [TestMethod]
    public void CreateTextureFromPngFile_WhenDecodeFails_IsNotCached()
    {
        using TemporaryDirectory temp = new();
        string fileName = Path.Combine(temp.Root, "missing.png");
        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);

        using LogCaptureScope log = new();

        Assert.IsNull(manager.CreateTextureFromPngFile(null!, TextureIndex, fileName));
        Assert.IsNull(manager.CreateTextureFromPngFile(null!, TextureIndex, fileName));

        Assert.AreEqual(2, log.Messages.Count, string.Join(" | ", log.Messages));
        Assert.AreEqual(0, lapp.CreateTextureCalls.Count);
    }

    /// <summary>
    /// 占位纹理是 1x1 全透明像素, 索引与请求一致, 并输出 Warning.
    /// </summary>
    [TestMethod]
    public void CreatePlaceholderTexture_UsesOnePixelAndKeepsIndex()
    {
        const string fileName = "missing-texture.png";
        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);

        using LogCaptureScope log = new();

        TextureInfo info = manager.CreatePlaceholderTexture(null!, TextureIndex, fileName);

        Assert.AreEqual(TextureIndex, info.Index);
        Assert.AreEqual(1, info.Width);
        Assert.AreEqual(1, info.Height);
        Assert.AreEqual(fileName, info.FileName);

        Assert.AreEqual(1, lapp.CreateTextureCalls.Count);
        TextureCall call = lapp.CreateTextureCalls[0];
        Assert.AreEqual(TextureIndex, call.Index);
        Assert.AreEqual(1, call.Width);
        Assert.AreEqual(1, call.Height);
        Assert.IsTrue(call.HasPixelBuffer, "The placeholder must provide a pixel.");
        Assert.AreEqual(0, call.FirstPixel, "The placeholder pixel must be fully transparent.");
        AssertSingleWarning(log, "placeholder", "index 2", fileName);
    }

    /// <summary>
    /// 组合方法在解码失败时返回 1x1 占位纹理, 索引保持请求值, 并输出两条 Warning.
    /// </summary>
    [TestMethod]
    public void CreateTextureOrPlaceholder_WhenDecodeFails_ReturnsPlaceholderWithSameIndex()
    {
        using TemporaryDirectory temp = new();
        string fileName = Path.Combine(temp.Root, "missing.png");
        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);

        using LogCaptureScope log = new();

        TextureInfo placeholder = manager.CreateTextureOrPlaceholder(null!, TextureIndex, fileName);

        Assert.AreEqual(TextureIndex, placeholder.Index);
        Assert.AreEqual(1, placeholder.Width);
        Assert.AreEqual(1, placeholder.Height);
        Assert.AreEqual(fileName, placeholder.FileName);

        Assert.AreEqual(1, lapp.CreateTextureCalls.Count);
        Assert.AreEqual(TextureIndex, lapp.CreateTextureCalls[0].Index);

        // 一条说明解码失败的原因, 一条说明用占位纹理顶替, 都包含文件路径.
        Assert.AreEqual(2, log.Messages.Count, string.Join(" | ", log.Messages));
        Assert.IsTrue(
            log.Messages.All(message => message.Contains(fileName, StringComparison.Ordinal)),
            string.Join(" | ", log.Messages)
        );
    }

    /// <summary>
    /// 组合方法在解码成功时直接返回真实纹理, 不创建占位纹理.
    /// </summary>
    [TestMethod]
    public void CreateTextureOrPlaceholder_WhenDecodeSucceeds_ReturnsRealTexture()
    {
        using TemporaryDirectory temp = new();
        string fileName = CreatePng(temp, "valid.png", 2, 3);
        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);

        using LogCaptureScope log = new();

        TextureInfo texture = manager.CreateTextureOrPlaceholder(null!, TextureIndex, fileName);

        Assert.AreEqual(TextureIndex, texture.Index);
        Assert.AreEqual(2, texture.Width);
        Assert.AreEqual(3, texture.Height);
        Assert.AreEqual(1, lapp.CreateTextureCalls.Count);
        Assert.AreEqual(0, log.Messages.Count, string.Join(" | ", log.Messages));
    }

    /// <summary>
    /// 释放纹理会调用 Dispose, 并从缓存移除, 之后可以重新读取同一文件.
    /// </summary>
    [TestMethod]
    public void ReleaseTexture_DisposesAndRemovesFromCache()
    {
        using TemporaryDirectory temp = new();
        string fileName = CreatePng(temp, "release.png", 2, 3);
        TestLAppDelegate lapp = new();
        LAppTextureManager manager = new(lapp);

        using LogCaptureScope log = new();

        TextureInfo? first = manager.CreateTextureFromPngFile(null!, TextureIndex, fileName);
        if (first is null)
        {
            Assert.Fail("A valid PNG must be decoded.");
            return;
        }

        manager.ReleaseTexture(first);

        TestTextureInfo released = (TestTextureInfo)first;
        Assert.IsTrue(released.Disposed, "ReleaseTexture must dispose the texture info.");

        TextureInfo? second = manager.CreateTextureFromPngFile(null!, TextureIndex, fileName);
        Assert.IsNotNull(second);
        Assert.AreNotSame(first, second, "A released texture must not stay in the cache.");
        Assert.AreEqual(2, lapp.CreateTextureCalls.Count);
    }

    /// <summary>
    /// 断言作用域里只有一条 Warning, 且包含所有指定片段.
    /// </summary>
    /// <param name="log">日志捕获作用域</param>
    /// <param name="fragments">日志中必须出现的片段</param>
    private static void AssertSingleWarning(LogCaptureScope log, params string[] fragments)
    {
        Assert.AreEqual(1, log.Messages.Count, string.Join(" | ", log.Messages));

        foreach (string fragment in fragments)
        {
            Assert.IsTrue(
                log.Messages[0].Contains(fragment, StringComparison.Ordinal),
                $"The warning must contain '{fragment}', actual: {log.Messages[0]}"
            );
        }
    }

    /// <summary>
    /// 在临时目录里生成一张纯色 PNG.
    /// </summary>
    /// <param name="temp">临时目录</param>
    /// <param name="name">文件名</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <returns>PNG 文件的绝对路径</returns>
    private static string CreatePng(TemporaryDirectory temp, string name, int width, int height)
    {
        string path = Path.Combine(temp.Root, name);
        using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(new SKColor(0x12, 0x34, 0x56, 0xFF));
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>
    /// 创建不触发 Core 或图形后端的模型引用, 仅用于区分纹理缓存所有者.
    /// </summary>
    /// <returns>未初始化的模型实例</returns>
    private static LAppModel CreateTestModel()
    {
        return (LAppModel)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                typeof(LAppModel)
            );
    }

    /// <summary>
    /// 一次纹理创建的参数快照.
    /// </summary>
    private sealed class TextureCall(
        int index,
        int width,
        int height,
        bool hasPixelBuffer,
        int firstPixel
    )
    {
        /// <summary>
        /// 纹理单元编号.
        /// </summary>
        public int Index { get; } = index;

        /// <summary>
        /// 纹理宽度.
        /// </summary>
        public int Width { get; } = width;

        /// <summary>
        /// 纹理高度.
        /// </summary>
        public int Height { get; } = height;

        /// <summary>
        /// 是否提供了像素指针.
        /// </summary>
        public bool HasPixelBuffer { get; } = hasPixelBuffer;

        /// <summary>
        /// 首像素的值, 像素指针为空时为 0.
        /// </summary>
        public int FirstPixel { get; } = firstPixel;
    }

    /// <summary>
    /// 测试用 delegate: CreateTexture 只记录参数, 不接触任何图形后端.
    /// </summary>
    private sealed class TestLAppDelegate : LAppDelegate
    {
        /// <summary>
        /// 按调用顺序记录的纹理创建参数.
        /// </summary>
        public List<TextureCall> CreateTextureCalls { get; } = [];

        public override void OnUpdatePre() { }

        public override void GetWindowSize(out int width, out int height)
        {
            width = 0;
            height = 0;
        }

        public override CubismRenderer CreateRenderer(CubismModel model)
        {
            throw new NotSupportedException("The texture manager tests never create a renderer.");
        }

        public override TextureInfo CreateTexture(
            LAppModel model,
            int index,
            int width,
            int height,
            IntPtr data
        )
        {
            // 像素指针只在调用期间有效, 因此立刻读取首像素.
            bool hasPixelBuffer = data != IntPtr.Zero;
            int firstPixel = hasPixelBuffer ? Marshal.ReadInt32(data) : 0;
            CreateTextureCalls.Add(new(index, width, height, hasPixelBuffer, firstPixel));
            return new TestTextureInfo();
        }

        public override bool RunPre() => false;

        public override void RunPost() { }
    }

    /// <summary>
    /// 记录释放状态的纹理信息.
    /// </summary>
    private sealed class TestTextureInfo : TextureInfo
    {
        /// <summary>
        /// 是否已被释放.
        /// </summary>
        public bool Disposed { get; private set; }

        public override void Dispose()
        {
            Disposed = true;
        }
    }
}
