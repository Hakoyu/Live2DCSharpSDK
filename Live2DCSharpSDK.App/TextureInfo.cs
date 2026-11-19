namespace Live2DCSharpSDK.App;

/// <summary>
/// 图像信息结构体
/// </summary>
public abstract class TextureInfo
{
    public int Index;
    /// <summary>
    /// 纹理 ID
    /// </summary>
    public int Id;
    /// <summary>
    /// 宽度
    /// </summary>
    public int Width;
    /// <summary>
    /// 高度
    /// </summary>
    public int Height;
    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName;

    /// <summary>
    /// 释放图像
    /// </summary>
    public abstract void Dispose();
};