namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// オフスクリーン用レンダーターゲットマネージャの非ジェネリック基底。
/// drawable 用 / offscreen 用の 2 系統の manager を、型引数に依存しない形で
/// 保持・列挙するための共通型（P6 / P8 が 2 系統の manager を保持する際に利用）。
/// </summary>
public abstract class CubismOffscreenManager
{
}

/// <summary>
/// オフスクリーン用のレンダーターゲットを管理するクラス
/// （官方 Framework/src/Rendering/ICubismOffscreenManager.hpp:19-334）。
/// フレーム単位の使用中カウントと、未使用レンダーターゲットの
/// 再利用・解放（ReleaseStaleRenderTextures の移動式回收アルゴリズム）を実装する。
/// </summary>
/// <typeparam name="T">バックエンド固有のレンダーターゲット型（CubismRenderTarget 派生）</typeparam>
public abstract class ICubismOffscreenManager<T> : CubismOffscreenManager, IDisposable
    where T : CubismRenderTarget, new()
{
    /// <summary>
    /// オフスクリーン用のレンダーターゲットのコンテナ構造体（官方 CubismRenderTargetContainer）
    /// </summary>
    private sealed class CubismRenderTargetContainer
    {
        /// <summary>
        /// レンダーターゲット本体
        /// </summary>
        public T RenderTarget = null!;
        /// <summary>
        /// 使用中かどうか（官方コンストラクタでは true 初期化）
        /// </summary>
        public bool IsUsed = true;
    }

    /// <summary>
    /// オフスクリーン描画用レンダーターゲットのコンテナリスト
    /// </summary>
    private readonly List<CubismRenderTargetContainer> _offscreenRenderTargetList = [];
    /// <summary>
    /// 直前のアクティブなレンダーターゲットの最大数
    /// </summary>
    private uint _previousActiveRenderTextureMaxCount;
    /// <summary>
    /// 現在のアクティブなレンダーターゲットの数
    /// </summary>
    private uint _currentActiveRenderTextureCount;
    /// <summary>
    /// 今フレームでリセットされたかどうか
    /// </summary>
    private bool _hasResetThisFrame;

    /// <summary>
    /// モデルを描画する前に呼び出すフレーム開始時の処理
    /// </summary>
    public void BeginFrameProcess()
    {
        if (_hasResetThisFrame)
        {
            return;
        }

        _previousActiveRenderTextureMaxCount = 0;
        _hasResetThisFrame = true;
    }

    /// <summary>
    /// モデルの描画が終わった後に呼び出すフレーム終了時の処理
    /// </summary>
    public void EndFrameProcess()
    {
        _hasResetThisFrame = false;
    }

    /// <summary>
    /// レンダーターゲットのリストサイズを取得する
    /// </summary>
    public uint GetOffscreenRenderTargetListSize()
    {
        return (uint)_offscreenRenderTargetList.Count;
    }

    /// <summary>
    /// レンダーターゲットの使用状態を取得する
    /// </summary>
    /// <param name="renderTarget">レンダーターゲット</param>
    /// <returns>使用中は true、未使用の場合は false</returns>
    public bool GetUsingRenderTextureState(T renderTarget)
    {
        for (int i = 0; i < _offscreenRenderTargetList.Count; ++i)
        {
            if (ReferenceEquals(_offscreenRenderTargetList[i].RenderTarget, renderTarget))
            {
                return _offscreenRenderTargetList[i].IsUsed;
            }
        }

        return false;
    }

    /// <summary>
    /// レンダーターゲットの使用を終了する
    /// </summary>
    /// <param name="renderTarget">レンダーターゲット</param>
    public void StopUsingRenderTexture(T renderTarget)
    {
        for (int i = 0; i < _offscreenRenderTargetList.Count; ++i)
        {
            if (!ReferenceEquals(_offscreenRenderTargetList[i].RenderTarget, renderTarget))
            {
                continue;
            }

            if (!_offscreenRenderTargetList[i].IsUsed)
            {
                break;
            }

            _offscreenRenderTargetList[i].IsUsed = false;

            if (0 < _currentActiveRenderTextureCount)
            {
                --_currentActiveRenderTextureCount;
            }

            break;
        }
    }

    /// <summary>
    /// レンダーターゲットの使用を全て終了する
    /// </summary>
    public void StopUsingAllRenderTextures()
    {
        for (int i = 0; i < _offscreenRenderTargetList.Count; ++i)
        {
            _offscreenRenderTargetList[i].IsUsed = false;
        }

        _currentActiveRenderTextureCount = 0;
    }

    /// <summary>
    /// 使用されていないレンダーターゲットを開放する（官方 ReleaseStaleRenderTextures）
    /// </summary>
    public void ReleaseStaleRenderTextures()
    {
        int listSize = _offscreenRenderTargetList.Count;
        if (_hasResetThisFrame || listSize == 0)
        {
            // 使用する量が変化する場合は開放しない
            return;
        }

        // 未使用な場所を開放して直前の最大数までリサイズする
        uint findPos = 0;
        uint resize = _previousActiveRenderTextureMaxCount;
        for (int i = listSize; _previousActiveRenderTextureMaxCount < i; --i)
        {
            int index = i - 1;
            if (_offscreenRenderTargetList[index].IsUsed)
            {
                // 空いている場所探して移動させる
                bool isFind = false;
                for (; findPos < _previousActiveRenderTextureMaxCount; ++findPos)
                {
                    if (!_offscreenRenderTargetList[(int)findPos].IsUsed)
                    {
                        T renderTarget = _offscreenRenderTargetList[(int)findPos].RenderTarget;
                        _offscreenRenderTargetList[(int)findPos].RenderTarget = _offscreenRenderTargetList[index].RenderTarget;
                        _offscreenRenderTargetList[(int)findPos].IsUsed = true;
                        _offscreenRenderTargetList[index].RenderTarget = renderTarget;
                        _offscreenRenderTargetList[index].IsUsed = false;
                        isFind = true;
                        break;
                    }
                }

                if (!isFind)
                {
                    // 空いている場所が見つからなかったら現状のサイズでリサイズする
                    resize = (uint)i;
                    break;
                }
            }

            _offscreenRenderTargetList[index].RenderTarget.DestroyRenderTarget();
            _offscreenRenderTargetList[index].RenderTarget = null!;
        }

        if (resize < (uint)_offscreenRenderTargetList.Count)
        {
            _offscreenRenderTargetList.RemoveRange((int)resize, _offscreenRenderTargetList.Count - (int)resize);
        }
    }

    /// <summary>
    /// レンダーターゲットを全て開放する
    /// </summary>
    public void ReleaseAllRenderTextures()
    {
        for (int i = 0; i < _offscreenRenderTargetList.Count; ++i)
        {
            _offscreenRenderTargetList[i].RenderTarget.DestroyRenderTarget();
        }

        _offscreenRenderTargetList.Clear();
        _previousActiveRenderTextureMaxCount = 0;
        _currentActiveRenderTextureCount = 0;
    }

    /// <summary>
    /// 直前のアクティブなレンダーターゲットの最大数を取得する
    /// </summary>
    public uint GetPreviousActiveRenderTextureCount()
    {
        return _previousActiveRenderTextureMaxCount;
    }

    /// <summary>
    /// 現在のアクティブなレンダーターゲットの数を取得する
    /// </summary>
    public uint GetCurrentActiveRenderTextureCount()
    {
        return _currentActiveRenderTextureCount;
    }

    /// <summary>
    /// レンダーターゲットの最大数を更新する
    /// </summary>
    protected void UpdateRenderTargetCount()
    {
        ++_currentActiveRenderTextureCount;

        // 最大数更新
        _previousActiveRenderTextureMaxCount = _currentActiveRenderTextureCount > _previousActiveRenderTextureMaxCount
            ? _currentActiveRenderTextureCount
            : _previousActiveRenderTextureMaxCount;
    }

    /// <summary>
    /// 未使用のレンダーターゲットを取得する
    /// </summary>
    /// <returns>未使用のレンダーターゲット。なければ null</returns>
    protected T? GetUnusedOffscreenRenderTarget()
    {
        for (int i = 0; i < _offscreenRenderTargetList.Count; ++i)
        {
            if (!_offscreenRenderTargetList[i].IsUsed)
            {
                _offscreenRenderTargetList[i].IsUsed = true;
                return _offscreenRenderTargetList[i].RenderTarget;
            }
        }

        return null;
    }

    /// <summary>
    /// レンダーターゲットを作成する（官方 CSM_NEW T() 相当）
    /// </summary>
    /// <returns>新しいレンダーターゲット</returns>
    protected T CreateOffscreenRenderTarget()
    {
        CubismRenderTargetContainer container = new() { RenderTarget = new T() };
        _offscreenRenderTargetList.Add(container);
        return container.RenderTarget;
    }

    /// <summary>
    /// レンダーターゲットを全て解放する
    /// </summary>
    public void Dispose()
    {
        ReleaseAllRenderTextures();
        GC.SuppressFinalize(this);
    }
}
