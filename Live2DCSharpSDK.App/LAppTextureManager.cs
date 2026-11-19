using SkiaSharp;

namespace Live2DCSharpSDK.App;

/// <summary>
/// 负责图像读取与管理的类.
/// </summary>
public class LAppTextureManager(LAppDelegate lapp)
{
    private readonly List<TextureInfo> _textures = [];

    /// <summary>
    /// 读取图像
    /// </summary>
    /// <param name="fileName">要读取的图像文件路径名</param>
    /// <returns>图像信息. 读取失败时返回 NULL</returns>
    public unsafe TextureInfo CreateTextureFromPngFile(LAppModel model, int index, string fileName)
    {
        //search loaded texture already.
        var item = _textures.FirstOrDefault(a => a.FileName == fileName);
        if (item != null)
        {
            return item;
        }
        var info1 = SKBitmap.DecodeBounds(fileName);
        info1.ColorType = SKColorType.Rgba8888;
        using var image = SKBitmap.Decode(fileName, info1);

        // 生成供 OpenGL 使用的纹理
        var info = lapp.CreateTexture(model, index, image.Width, image.Height, image.GetPixels());
        info.FileName = fileName;
        info.Width = image.Width;
        info.Index = index;
        info.Height = image.Height;

        _textures.Add(info);

        return info;
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
