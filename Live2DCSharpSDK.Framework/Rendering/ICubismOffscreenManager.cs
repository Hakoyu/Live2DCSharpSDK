namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// Offscreen 用渲染目标管理器的非泛型基类.
/// 把 drawable 用 / offscreen 用的 2 套 manager 以不依赖类型参数的形式
/// 用于持有与枚举的公共类型(两套 manager 各持一份时使用).
/// </summary>
public abstract class CubismOffscreenManager
{
}

/// <summary>
/// 管理 Offscreen 用渲染目标的类
/// (官方 Framework/src/Rendering/ICubismOffscreenManager.hpp:19-334).
/// 按帧统计的使用中计数, 以及未使用渲染目标的
/// 复用与释放 (ReleaseStaleRenderTextures 的移动式回收算法) 的实现.
/// </summary>
/// <typeparam name="T">后端特有的渲染目标类型 (CubismRenderTarget 派生)</typeparam>
public abstract class ICubismOffscreenManager<T> : CubismOffscreenManager, IDisposable
    where T : CubismRenderTarget, new()
{
    /// <summary>
    /// Offscreen 用渲染目标的容器结构体 (官方 CubismRenderTargetContainer)
    /// </summary>
    private sealed class CubismRenderTargetContainer
    {
        /// <summary>
        /// 渲染目标本体
        /// </summary>
        public T RenderTarget = null!;
        /// <summary>
        /// 是否使用中 (官方的构造函数中初始化为 true)
        /// </summary>
        public bool IsUsed = true;
    }

    /// <summary>
    /// Offscreen 绘制用渲染目标的容器列表
    /// </summary>
    private readonly List<CubismRenderTargetContainer> _offscreenRenderTargetList = [];
    /// <summary>
    /// 上一次活动渲染目标的最大数量
    /// </summary>
    private uint _previousActiveRenderTextureMaxCount;
    /// <summary>
    /// 当前活动渲染目标的数量
    /// </summary>
    private uint _currentActiveRenderTextureCount;
    /// <summary>
    /// 是否在本帧中被重置
    /// </summary>
    private bool _hasResetThisFrame;

    /// <summary>
    /// 绘制模型之前调用的帧开始处理
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
    /// 模型绘制结束后调用的帧结束处理
    /// </summary>
    public void EndFrameProcess()
    {
        _hasResetThisFrame = false;
    }

    /// <summary>
    /// 获取渲染目标列表的尺寸
    /// </summary>
    public uint GetOffscreenRenderTargetListSize()
    {
        return (uint)_offscreenRenderTargetList.Count;
    }

    /// <summary>
    /// 获取渲染目标的使用状态
    /// </summary>
    /// <param name="renderTarget">渲染目标</param>
    /// <returns>使用中为 true, 未使用时为 false</returns>
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
    /// 结束渲染目标的使用
    /// </summary>
    /// <param name="renderTarget">渲染目标</param>
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
    /// 结束全部渲染目标的使用
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
    /// 释放未被使用的渲染目标 (官方 ReleaseStaleRenderTextures)
    /// </summary>
    public void ReleaseStaleRenderTextures()
    {
        int listSize = _offscreenRenderTargetList.Count;
        if (_hasResetThisFrame || listSize == 0)
        {
            // 使用数量发生变化时不释放
            return;
        }

        // 释放未使用的位置并调整到上一次的最大数量
        uint findPos = 0;
        uint resize = _previousActiveRenderTextureMaxCount;
        for (int i = listSize; _previousActiveRenderTextureMaxCount < i; --i)
        {
            int index = i - 1;
            if (_offscreenRenderTargetList[index].IsUsed)
            {
                // 寻找空闲位置并移动
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
                    // 如果找不到空闲位置, 则以当前尺寸进行调整
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
    /// 释放全部渲染目标
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
    /// 获取上一次活动渲染目标的最大数量
    /// </summary>
    public uint GetPreviousActiveRenderTextureCount()
    {
        return _previousActiveRenderTextureMaxCount;
    }

    /// <summary>
    /// 获取当前活动渲染目标的数量
    /// </summary>
    public uint GetCurrentActiveRenderTextureCount()
    {
        return _currentActiveRenderTextureCount;
    }

    /// <summary>
    /// 更新渲染目标的最大数量
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
    /// 获取未使用的渲染目标
    /// </summary>
    /// <returns>未使用的渲染目标. 没有时为 null</returns>
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
    /// 创建渲染目标 (相当于官方 CSM_NEW T())
    /// </summary>
    /// <returns>新的渲染目标</returns>
    protected T CreateOffscreenRenderTarget()
    {
        CubismRenderTargetContainer container = new() { RenderTarget = new T() };
        _offscreenRenderTargetList.Add(container);
        return container.RenderTarget;
    }

    /// <summary>
    /// 释放全部渲染目标
    /// </summary>
    public void Dispose()
    {
        ReleaseAllRenderTextures();
        GC.SuppressFinalize(this);
    }
}
