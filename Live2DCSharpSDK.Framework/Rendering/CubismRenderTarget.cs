namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// 离屏绘制用渲染目标的抽象基类.
/// 后端特有的创建与绘制 API 由派生类
/// (CubismRenderTarget_OpenGLES2 / CubismRenderTarget_Vulkan) 持有,
/// 这里只放置 <see cref="ICubismOffscreenManager{T}"/> 共通使用的
/// "销毁" 与 "尺寸查询".
/// 官方对应: OpenGL/CubismRenderTarget_OpenGLES2.hpp, Vulkan/CubismRenderTarget_Vulkan.hpp
/// </summary>
public abstract class CubismRenderTarget : IDisposable
{
    /// <summary>
    /// 当前是否有效 (官方 IsValid)
    /// </summary>
    public abstract bool IsValid();

    /// <summary>
    /// 获取缓冲区宽度 (官方 GetBufferWidth)
    /// </summary>
    public abstract uint GetBufferWidth();

    /// <summary>
    /// 获取缓冲区高度 (官方 GetBufferHeight)
    /// </summary>
    public abstract uint GetBufferHeight();

    /// <summary>
    /// 删除渲染目标 (官方 DestroyRenderTarget)
    /// </summary>
    public abstract void DestroyRenderTarget();

    /// <summary>
    /// 释放渲染目标
    /// </summary>
    public void Dispose()
    {
        DestroyRenderTarget();
        GC.SuppressFinalize(this);
    }
}
