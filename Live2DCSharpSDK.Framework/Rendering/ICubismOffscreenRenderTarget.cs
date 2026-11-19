namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// 管理 Offscreen 用渲染目标的类
/// (官方 Framework/src/Rendering/ICubismOffscreenRenderTarget.hpp:19-141).
/// 把 C++ 的 CRTP (T = 继承目标的子类自身, U = 渲染目标类型)
/// 用自引用泛型约束模拟.
/// </summary>
/// <typeparam name="TSelf">继承目标的子类自身</typeparam>
/// <typeparam name="TRenderTarget">渲染目标的类型</typeparam>
public abstract class ICubismOffscreenRenderTarget<TSelf, TRenderTarget>
    where TSelf : ICubismOffscreenRenderTarget<TSelf, TRenderTarget>
    where TRenderTarget : CubismRenderTarget
{
    /// <summary>
    /// 渲染目标 (官方 _renderTarget)
    /// </summary>
    protected TRenderTarget? _renderTarget;

    /// <summary>
    /// _offscreens 的 Index (官方 _offscreenIndex, 初始值 -1)
    /// </summary>
    private int _offscreenIndex = -1;

    /// <summary>
    /// 父级 Offscreen 渲染目标 (官方 _parentRenderTarget)
    /// </summary>
    private TSelf? _parentRenderTarget;

    /// <summary>
    /// 之前的 Offscreen 渲染目标 (官方 _oldOffscreen)
    /// </summary>
    private TSelf? _oldOffscreen;

    /// <summary>
    /// 设置 Offscreen 的 Index
    /// </summary>
    /// <param name="offscreenIndex">Offscreen 的 Index</param>
    public void SetOffscreenIndex(int offscreenIndex)
    {
        _offscreenIndex = offscreenIndex;
    }

    /// <summary>
    /// 获取 Offscreen 的 Index
    /// </summary>
    public int GetOffscreenIndex()
    {
        return _offscreenIndex;
    }

    /// <summary>
    /// 设置之前的 Offscreen 渲染目标
    /// </summary>
    /// <param name="oldOffscreen">之前的 Offscreen 渲染目标</param>
    public void SetOldOffscreen(TSelf? oldOffscreen)
    {
        _oldOffscreen = oldOffscreen;
    }

    /// <summary>
    /// 获取之前的 Offscreen 渲染目标
    /// </summary>
    public TSelf? GetOldOffscreen()
    {
        return _oldOffscreen;
    }

    /// <summary>
    /// 设置父级 Offscreen 渲染目标
    /// </summary>
    /// <param name="parentRenderTarget">父级 Offscreen 渲染目标</param>
    public void SetParentPartOffscreen(TSelf? parentRenderTarget)
    {
        _parentRenderTarget = parentRenderTarget;
    }

    /// <summary>
    /// 获取父级 Offscreen 渲染目标
    /// </summary>
    public TSelf? GetParentPartOffscreen()
    {
        return _parentRenderTarget;
    }

    /// <summary>
    /// 获取渲染目标
    /// </summary>
    public TRenderTarget? GetRenderTarget()
    {
        return _renderTarget;
    }
}
