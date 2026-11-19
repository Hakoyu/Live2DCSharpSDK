namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// オフスクリーン用のレンダーターゲットを管理するクラス
/// （官方 Framework/src/Rendering/ICubismOffscreenRenderTarget.hpp:19-141）。
/// C++ の CRTP（T = 継承先の子クラス自身、U = レンダーターゲット型）を
/// 自引用ジェネリック制約で模擬する（PLAN D-07）。
/// </summary>
/// <typeparam name="TSelf">継承先の子クラス自身</typeparam>
/// <typeparam name="TRenderTarget">レンダーターゲットの型</typeparam>
public abstract class ICubismOffscreenRenderTarget<TSelf, TRenderTarget>
    where TSelf : ICubismOffscreenRenderTarget<TSelf, TRenderTarget>
    where TRenderTarget : CubismRenderTarget
{
    /// <summary>
    /// レンダーターゲット（官方 _renderTarget）
    /// </summary>
    protected TRenderTarget? _renderTarget;
    /// <summary>
    /// _offscreens の Index（官方 _offscreenIndex、初期値 -1）
    /// </summary>
    private int _offscreenIndex = -1;
    /// <summary>
    /// 親のオフスクリーンレンダーターゲット（官方 _parentRenderTarget）
    /// </summary>
    private TSelf? _parentRenderTarget;
    /// <summary>
    /// 以前のオフスクリーンレンダーターゲット（官方 _oldOffscreen）
    /// </summary>
    private TSelf? _oldOffscreen;

    /// <summary>
    /// オフスクリーンの Index を設定する
    /// </summary>
    /// <param name="offscreenIndex">オフスクリーンの Index</param>
    public void SetOffscreenIndex(int offscreenIndex)
    {
        _offscreenIndex = offscreenIndex;
    }

    /// <summary>
    /// オフスクリーンの Index を取得する
    /// </summary>
    public int GetOffscreenIndex()
    {
        return _offscreenIndex;
    }

    /// <summary>
    /// 以前のオフスクリーンレンダーターゲットを設定する
    /// </summary>
    /// <param name="oldOffscreen">以前のオフスクリーンレンダーターゲット</param>
    public void SetOldOffscreen(TSelf? oldOffscreen)
    {
        _oldOffscreen = oldOffscreen;
    }

    /// <summary>
    /// 以前のオフスクリーンレンダーターゲットを取得する
    /// </summary>
    public TSelf? GetOldOffscreen()
    {
        return _oldOffscreen;
    }

    /// <summary>
    /// 親のオフスクリーンレンダーターゲットを設定する
    /// </summary>
    /// <param name="parentRenderTarget">親のオフスクリーンレンダーターゲット</param>
    public void SetParentPartOffscreen(TSelf? parentRenderTarget)
    {
        _parentRenderTarget = parentRenderTarget;
    }

    /// <summary>
    /// 親のオフスクリーンレンダーターゲットを取得する
    /// </summary>
    public TSelf? GetParentPartOffscreen()
    {
        return _parentRenderTarget;
    }

    /// <summary>
    /// レンダーターゲットを取得する
    /// </summary>
    public TRenderTarget? GetRenderTarget()
    {
        return _renderTarget;
    }
}
