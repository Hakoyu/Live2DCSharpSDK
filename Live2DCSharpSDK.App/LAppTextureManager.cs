using Live2DCSharpSDK.Framework;
using SkiaSharp;

namespace Live2DCSharpSDK.App;

/// <summary>
/// 负责图像读取与管理的类.
/// </summary>
public class LAppTextureManager(LAppDelegate lapp)
{
    /// <summary>
    /// 占位纹理的边长(像素). 解码失败时用它占住纹理单元, 保证索引不变.
    /// </summary>
    private const int PlaceholderTextureSize = 1;

    private readonly List<TextureInfo> _textures = [];

    /// <summary>
    /// 读取图像
    /// </summary>
    /// <param name="model">要绑定纹理的模型</param>
    /// <param name="index">纹理单元编号, 必须与 model3.json 的 Textures 数组下标一致</param>
    /// <param name="fileName">要读取的图像文件路径名</param>
    /// <returns>
    /// 图像信息. 文件不存在, 内容无法解码或格式不受支持时返回 null,
    /// 并输出包含文件路径与原因的 Warning.
    /// </returns>
    public unsafe TextureInfo? CreateTextureFromPngFile(LAppModel model, int index, string fileName)
    {
        //search loaded texture already.
        var item = _textures.FirstOrDefault(a => a.FileName == fileName);
        if (item is not null)
        {
            return item;
        }

        if (File.Exists(fileName) is false)
        {
            CubismLog.Warning(
                "[Live2D App]Texture file not found (index {0}): {1}",
                index,
                fileName
            );
            return null;
        }

        // SKBitmap.DecodeBounds 失败时返回 SKImageInfo.Empty (结构体, 不是 null), 宽高为 0.
        // 用空信息调用 Decode 拿不到像素, 因此先判空返回.
        var info1 = SKBitmap.DecodeBounds(fileName);
        if (info1.IsEmpty)
        {
            CubismLog.Warning(
                "[Live2D App]Failed to read texture size, not a supported image (index {0}): {1}",
                index,
                fileName
            );
            return null;
        }

        info1.ColorType = SKColorType.Rgba8888;
        using var image = SKBitmap.Decode(fileName, info1);
        if (image is null)
        {
            CubismLog.Warning(
                "[Live2D App]Failed to decode texture image (index {0}): {1}",
                index,
                fileName
            );
            return null;
        }

        if (image.Width <= 0 || image.Height <= 0)
        {
            CubismLog.Warning(
                "[Live2D App]Decoded texture has an invalid size {0}x{1} (index {2}): {3}",
                image.Width,
                image.Height,
                index,
                fileName
            );
            return null;
        }

        var pixels = image.GetPixels();
        if (pixels == IntPtr.Zero)
        {
            CubismLog.Warning(
                "[Live2D App]Decoded texture has no pixel buffer (index {0}): {1}",
                index,
                fileName
            );
            return null;
        }

        // 生成供 OpenGL 使用的纹理
        var info = lapp.CreateTexture(model, index, image.Width, image.Height, pixels);
        info.FileName = fileName;
        info.Width = image.Width;
        info.Index = index;
        info.Height = image.Height;

        _textures.Add(info);

        return info;
    }

    /// <summary>
    /// 生成 1x1 的全透明占位纹理.
    /// 解码失败时用它占住纹理单元, 保证 Textures 的下标与 model3.json 的纹理单元编号一一对应.
    /// </summary>
    /// <param name="model">要绑定纹理的模型</param>
    /// <param name="index">纹理单元编号</param>
    /// <param name="fileName">解码失败的图像路径, 仅用于日志与标识</param>
    /// <returns>占位纹理</returns>
    public unsafe TextureInfo CreatePlaceholderTexture(LAppModel model, int index, string fileName)
    {
        // 1x1 RGBA(0, 0, 0, 0) 全透明像素. 上传完成后不再引用该地址, 因此用栈上变量即可.
        uint transparentPixel = 0;
        uint* pixels = &transparentPixel;

        CubismLog.Warning(
            "[Live2D App]Use a 1x1 placeholder texture (index {0}): {1}",
            index,
            fileName
        );

        var info = lapp.CreateTexture(
            model,
            index,
            PlaceholderTextureSize,
            PlaceholderTextureSize,
            (nint)pixels
        );
        info.FileName = fileName;
        info.Index = index;
        info.Width = PlaceholderTextureSize;
        info.Height = PlaceholderTextureSize;

        // 占位纹理不进入缓存: 缓存按文件名复用, 会把占位纹理当成真实纹理返回给其它模型,
        // 而且不会为新模型重新绑定纹理单元.
        return info;
    }

    /// <summary>
    /// 读取一张纹理, 解码失败时用 1x1 占位纹理顶替.
    /// 纹理单元编号由调用方给定, 失败时也必须占用同一个编号, 否则后续纹理整体前移.
    /// </summary>
    /// <param name="model">要绑定纹理的模型</param>
    /// <param name="index">纹理单元编号, 必须与 model3.json 的 Textures 数组下标一致</param>
    /// <param name="fileName">要读取的图像文件路径名</param>
    /// <returns>解码成功时为真实纹理, 失败时为 1x1 占位纹理</returns>
    public TextureInfo CreateTextureOrPlaceholder(LAppModel model, int index, string fileName)
    {
        var texture = CreateTextureFromPngFile(model, index, fileName);
        if (texture is not null)
        {
            return texture;
        }

        return CreatePlaceholderTexture(model, index, fileName);
    }

    /// <summary>
    /// 释放指定纹理 ID 的图像
    /// </summary>
    /// <param name="textureId">要释放的纹理 ID</param>
    public void ReleaseTexture(TextureInfo info)
    {
        info.Dispose();
        _textures.Remove(info);
    }
}
