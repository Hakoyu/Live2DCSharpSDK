using System.Runtime.InteropServices;
using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// 实现 OpenGLES2 用绘制命令的类
/// (官方 OpenGL/CubismRenderer_OpenGLES2.hpp:179-559 / .cpp:472-1423). 
/// R5 中整合 Drawable 与 Offscreen 的绘制顺序 (双重排序数组), 
/// Drawable/Offscreen 两套 Clipping 管理器, 以及模型整体的
/// 渲染目标 (用于 blend 模式). 
/// </summary>
public class CubismRenderer_OpenGLES2 : CubismRenderer
{
    public const int ColorChannelCount = 4;   // 实验时 1 通道为 1, 仅 RGB 为 3, 含 Alpha 则为 4
    public const int ClippingMaskMaxCountOnDefault = 36;  // 通常的帧缓冲每张的 Mask 最大数
    public const int ClippingMaskMaxCountOnMultiRenderTexture = 32;   // 帧缓冲为 2 张以上时每张帧缓冲的 Mask 最大数

    private readonly OpenGLApi GL;

    /// <summary>
    /// 模型引用的纹理与渲染器绑定的纹理之间的映射
    /// </summary>
    private readonly Dictionary<int, int> _textures;
    /// <summary>
    /// 将绘制对象的索引按绘制顺序排列的列表 (Drawable + Offscreen 的连续编号)
    /// </summary>
    private readonly int[] _sortedObjectsIndexList;
    /// <summary>
    /// 将绘制对象的种类按绘制顺序排列的列表
    /// </summary>
    private readonly CsmEnum.DrawableObjectType[] _sortedObjectsTypeList;
    /// <summary>
    /// 保存 OpenGL 状态的对象
    /// </summary>
    private readonly CubismRendererProfile_OpenGLES2 _rendererProfile;
    /// <summary>
    /// Drawable 用裁剪遮罩管理对象.
    /// </summary>
    private CubismClippingManager_OpenGLES2? _drawableClippingManager;
    /// <summary>
    /// Offscreen 用裁剪遮罩管理对象.
    /// </summary>
    private CubismClippingManager_OpenGLES2? _offscreenClippingManager;
    /// <summary>
    /// 用于绘制到 Mask 纹理的 Clipping 上下文
    /// </summary>
    internal CubismClippingContext? ClippingContextBufferForMask { get; set; }
    /// <summary>
    /// 用于 Drawable 在画面上绘制的 Clipping 上下文
    /// </summary>
    internal CubismClippingContext? ClippingContextBufferForDrawable { get; set; }
    /// <summary>
    /// 用于 Offscreen 在画面上绘制的 Clipping 上下文
    /// </summary>
    internal CubismClippingContext? ClippingContextBufferForOffscreen { get; set; }

    internal CubismShader_OpenGLES2 Shader;

    /// <summary>
    /// 模型整体绘制目标的帧缓冲(blend 模式用).
    /// </summary>
    private readonly List<CubismRenderTarget_OpenGLES2> _modelRenderTargets = [];
    /// <summary>
    /// Drawable 的遮罩绘制用帧缓冲.
    /// </summary>
    private readonly List<CubismRenderTarget_OpenGLES2> _drawableMasks = [];
    /// <summary>
    /// Offscreen 功能的遮罩绘制用帧缓冲.
    /// </summary>
    private readonly List<CubismRenderTarget_OpenGLES2> _offscreenMasks = [];
    /// <summary>
    /// 模型的离屏列表.
    /// </summary>
    private readonly List<CubismOffscreenRenderTarget_OpenGLES2> _offscreenList = [];

    /// <summary>
    /// 当前的帧缓冲对象
    /// </summary>
    private int _currentFBO;
    /// <summary>
    /// 当前的 Offscreen 帧缓冲
    /// </summary>
    private CubismOffscreenRenderTarget_OpenGLES2? _currentOffscreen;
    /// <summary>
    /// 模型绘制的根帧缓冲
    /// </summary>
    private int _modelRootFBO;
    /// <summary>
    /// 是否已 Dispose (防止重复释放. 避免 GL 名称复用事故)
    /// </summary>
    private bool _disposed;

    // ---- 帧缓冲创建失败的诊断日志只输出一次的标志(防止每帧刷屏) ----
    /// <summary>
    /// 是否已警告模型合成用渲染目标不可用
    /// </summary>
    private bool _warnedModelRenderTargetUnavailable;
    /// <summary>
    /// 是否已警告离屏用渲染目标不可用
    /// </summary>
    private bool _warnedOffscreenRenderTargetUnavailable;
    /// <summary>
    /// 是否已警告拷贝目标渲染目标不可用
    /// </summary>
    private bool _warnedCopyRenderTargetUnavailable;
    /// <summary>
    /// 是否已警告 Drawable 用遮罩缓冲不可用
    /// </summary>
    private bool _warnedDrawableMaskBufferUnavailable;
    /// <summary>
    /// 是否已警告 Offscreen 用遮罩缓冲不可用
    /// </summary>
    private bool _warnedOffscreenMaskBufferUnavailable;

    internal int VertexArray { get; private set; }
    internal int VertexBuffer { get; private set; }
    internal int IndexBuffer { get; private set; }
    /// <summary>
    /// 画面整体四边形用的顶点缓冲 (Copy / Offscreen 合成用)
    /// </summary>
    private int _quadVertexBuffer;
    /// <summary>
    /// 画面整体四边形用的索引缓冲
    /// </summary>
    private int _quadIndexBuffer;

    /// <summary>
    /// 复用的视口缓冲 (渲染器为单线程使用, 避免每帧分配).
    /// </summary>
    private readonly int[] _viewportBuffer = new int[4];

    /// <summary>
    /// 复用的高精度遮罩视口缓冲 (渲染器为单线程使用, 避免每次绘制分配).
    /// </summary>
    private readonly int[] _highPrecisionMaskViewportBuffer = new int[4];

    /// <summary>
    /// 用于绘制目标合成的四边形 (位置 + UV 交错)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct VBO
    {
        public float ver0;
        public float ver1;
        public float uv0;
        public float uv1;
    }

    /// <summary>
    /// 画面整体四边形的顶点 (普通 UV)
    /// </summary>
    private static readonly VBO[] QuadVertices =
    [
        new() { ver0 = -1.0f, ver1 = -1.0f, uv0 = 0.0f, uv1 = 0.0f },
        new() { ver0 = 1.0f, ver1 = -1.0f, uv0 = 1.0f, uv1 = 0.0f },
        new() { ver0 = -1.0f, ver1 = 1.0f, uv0 = 0.0f, uv1 = 1.0f },
        new() { ver0 = 1.0f, ver1 = 1.0f, uv0 = 1.0f, uv1 = 1.0f },
    ];

    /// <summary>
    /// 画面整体四边形的顶点 (上下翻转 UV, 用于 Offscreen 合成)
    /// </summary>
    private static readonly VBO[] QuadVerticesReverseUv =
    [
        new() { ver0 = -1.0f, ver1 = -1.0f, uv0 = 0.0f, uv1 = 1.0f },
        new() { ver0 = 1.0f, ver1 = -1.0f, uv0 = 1.0f, uv1 = 1.0f },
        new() { ver0 = -1.0f, ver1 = 1.0f, uv0 = 0.0f, uv1 = 0.0f },
        new() { ver0 = 1.0f, ver1 = 1.0f, uv0 = 1.0f, uv1 = 0.0f },
    ];

    /// <summary>
    /// 画面整体四边形的索引 (官方 ModelRenderTargetIndexArray)
    /// </summary>
    private static readonly ushort[] QuadIndices = [0, 1, 2, 2, 1, 3];

    internal VBO[] vbo = new VBO[512];

    private readonly LAppDelegate _lapp;

    /// <summary>
    /// Tegra 处理器对应. 按扩展方式绘制启用/禁用的开关
    /// </summary>
    public void SetExtShaderMode(bool extMode, bool extPAMode = false)
    {
        Shader.SetExtShaderMode(extMode, extPAMode);
        Shader.ReleaseShaderProgram();
    }

    /// <summary>
    /// 构造函数(本地由宿主创建渲染器并传给模型).
    /// 在此执行相当于官方 Initialize(model, maskBufferCount) 的初始化. 
    /// </summary>
    public unsafe CubismRenderer_OpenGLES2(OpenGLApi gl, LAppDelegate lapp, CubismModel model, int maskBufferCount = 1) : base(model)
    {
        GL = gl;
        CubismRenderTarget_OpenGLES2.SetDefaultApi(gl);
        Shader = new(gl);
        _lapp = lapp;
        _rendererProfile = new(gl);
        _textures = new Dictionary<int, int>(32);

        VertexArray = GL.GenVertexArray();
        VertexBuffer = GL.GenBuffer();
        IndexBuffer = GL.GenBuffer();
        _quadVertexBuffer = GL.GenBuffer();
        _quadIndexBuffer = GL.GenBuffer();

        // 小于 1 时修正为 1
        if (maskBufferCount < 1)
        {
            maskBufferCount = 1;
            CubismLog.Warning("[Live2D OpenGL]The number of render textures must be an integer greater than or equal to 1. Set the number of render textures to 1.");
        }

        //------------ 模型整体的渲染目标(blend 模式用) ------------
        _modelRenderTargets.Clear();
        if (model.IsBlendModeEnabled())
        {
            // Offscreen 的创建
            // 本地不使用 texture barrier, 因此始终创建 2 张
            // (官方为 CanUseTextureBarrier() ? 1 : 2, .cpp:573)
            // 下标 0 为绘制目标
            // 下标 1 用于 Copy
            for (int i = 0; i < 2; ++i)
            {
                _modelRenderTargets.Add(new CubismRenderTarget_OpenGLES2(GL));
            }
        }

        //------------ Drawable 用裁剪遮罩 ------------
        if (model.IsUsingMasking())
        {
            _drawableClippingManager = new CubismClippingManager_OpenGLES2(GL)
            {
                Type = CsmEnum.DrawableObjectType.Drawable
            };
            _drawableClippingManager.Initialize(model, maskBufferCount);

            _drawableMasks.Clear();
            for (int i = 0; i < maskBufferCount; ++i)
            {
                var mask = new CubismRenderTarget_OpenGLES2(GL);
                mask.CreateRenderTarget((int)_drawableClippingManager.ClippingMaskBufferSize.X, (int)_drawableClippingManager.ClippingMaskBufferSize.Y);
                _drawableMasks.Add(mask);
            }
        }

        //------------ Offscreen 用裁剪遮罩 ------------
        if (model.IsUsingMaskingForOffscreen())
        {
            _offscreenClippingManager = new CubismClippingManager_OpenGLES2(GL)
            {
                Type = CsmEnum.DrawableObjectType.Offscreen
            };
            _offscreenClippingManager.Initialize(model, maskBufferCount);

            _offscreenMasks.Clear();
            for (int i = 0; i < maskBufferCount; ++i)
            {
                var mask = new CubismRenderTarget_OpenGLES2(GL);
                mask.CreateRenderTarget((int)_offscreenClippingManager.ClippingMaskBufferSize.X, (int)_offscreenClippingManager.ClippingMaskBufferSize.Y);
                _offscreenMasks.Add(mask);
            }
        }

        //------------ 绘制顺序排序数组(Drawable + Offscreen) ------------
        int offscreenCount = model.GetOffscreenCount();
        int drawableCount = model.GetDrawableCount();
        _sortedObjectsIndexList = new int[drawableCount + offscreenCount];
        _sortedObjectsTypeList = new CsmEnum.DrawableObjectType[drawableCount + offscreenCount];

        //------------ 离屏列表 ------------
        if (offscreenCount > 0)
        {
            for (int offscreenIndex = 0; offscreenIndex < offscreenCount; ++offscreenIndex)
            {
                var renderTarget = new CubismOffscreenRenderTarget_OpenGLES2();
                renderTarget.SetOffscreenIndex(offscreenIndex);
                _offscreenList.Add(renderTarget);
            }

            // 在全部 Offscreen 注册完成后再执行
            SetupParentOffscreens(model, offscreenCount);
        }

        // blend 模式下的遮罩精度提升(ApplyBlendModeMaskPrecision)
        ApplyBlendModeMaskPrecision(model.IsBlendModeEnabled());

        // [与官方的差异] 官方在 Initialize 时生成全部 494 个 Shader
        // (CubismRenderer_OpenGLES2.cpp:638-642), 本地延迟到首次使用时才生成.
    }

    /// <summary>
    /// 查找并设置 Offscreen 的父级 (官方 SetupParentOffscreens, .cpp:644-678). 
    /// </summary>
    private unsafe void SetupParentOffscreens(CubismModel model, int offscreenCount)
    {
        int* ownerIndices = model.GetOffscreenOwnerIndices();

        for (int offscreenIndex = 0; offscreenIndex < offscreenCount; ++offscreenIndex)
        {
            CubismOffscreenRenderTarget_OpenGLES2? parentOffscreen = null;
            int ownerIndex = ownerIndices[offscreenIndex];
            int parentIndex = model.GetPartParentPartIndex(ownerIndex);

            // 查找父级的 Offscreen
            while (parentIndex != CubismModel.CubismNoIndex_Parent)
            {
                for (int i = 0; i < offscreenCount; ++i)
                {
                    if (ownerIndices[_offscreenList[i].GetOffscreenIndex()] != parentIndex)
                    {
                        continue; // Offscreen 的索引与父级不一致时跳过
                    }

                    parentOffscreen = _offscreenList[i];
                    break;
                }

                if (parentOffscreen != null)
                {
                    break; // 找到父级的 Offscreen 时跳出循环
                }

                parentIndex = model.GetPartParentPartIndex(parentIndex);
            }

            // 设置父级的 Offscreen
            _offscreenList[offscreenIndex].SetParentPartOffscreen(parentOffscreen);
        }
    }

    /// <summary>
    /// OpenGL 纹理的绑定处理
    /// </summary>
    /// <param name="modelTextureNo">要设置的模型纹理编号</param>
    /// <param name="glTextureNo">OpenGL 纹理编号</param>
    public void BindTexture(int modelTextureNo, int glTextureNo)
    {
        _textures[modelTextureNo] = glTextureNo;
    }

    /// <summary>
    /// 获取绑定到 OpenGL 的纹理列表
    /// </summary>
    public Dictionary<int, int> GetBindedTextures()
    {
        return _textures;
    }

    /// <summary>
    /// 设置绘制对象 Clipping Mask 缓冲的尺寸
    /// (对应官方 SetDrawableClippingMaskBufferSize).
    /// </summary>
    public void SetDrawableClippingMaskBufferSize(float width, float height)
    {
        if (_drawableClippingManager == null)
        {
            return;
        }

        // 在销毁实例前保存渲染纹理的张数
        int renderTextureCount = _drawableClippingManager.RenderTextureCount;

        // 为变更 RenderTarget 的尺寸而销毁并重建实例
        _drawableClippingManager.Dispose();
        _drawableClippingManager = new CubismClippingManager_OpenGLES2(GL)
        {
            Type = CsmEnum.DrawableObjectType.Drawable
        };

        _drawableClippingManager.SetClippingMaskBufferSize(width, height);

        _drawableClippingManager.Initialize(Model, renderTextureCount);
    }

    /// <summary>
    /// 设置 Offscreen 的 Clipping Mask 缓冲的尺寸
    /// (对应官方 SetOffscreenClippingMaskBufferSize).
    /// </summary>
    public void SetOffscreenClippingMaskBufferSize(float width, float height)
    {
        if (_offscreenClippingManager == null)
        {
            return;
        }

        int renderTextureCount = _offscreenClippingManager.RenderTextureCount;

        _offscreenClippingManager.Dispose();
        _offscreenClippingManager = new CubismClippingManager_OpenGLES2(GL)
        {
            Type = CsmEnum.DrawableObjectType.Offscreen
        };

        _offscreenClippingManager.SetClippingMaskBufferSize(width, height);

        _offscreenClippingManager.Initialize(Model, renderTextureCount);
    }

    /// <summary>
    /// 设置 Clipping Mask 缓冲的尺寸 (旧 API 兼容. 用于 Drawable)
    /// </summary>
    public void SetClippingMaskBufferSize(float width, float height)
    {
        SetDrawableClippingMaskBufferSize(width, height);
    }

    /// <summary>
    /// 获取绘制对象的渲染纹理数量.
    /// </summary>
    public int GetDrawableRenderTextureCount()
    {
        return _drawableClippingManager?.RenderTextureCount ?? 0;
    }

    /// <summary>
    /// 获取离屏的渲染纹理数量.
    /// </summary>
    public int GetOffscreenRenderTextureCount()
    {
        return _offscreenClippingManager?.RenderTextureCount ?? 0;
    }

    /// <summary>
    /// 获取绘制对象的裁剪遮罩缓冲尺寸.
    /// </summary>
    public System.Numerics.Vector2 GetDrawableClippingMaskBufferSize()
    {
        return _drawableClippingManager?.ClippingMaskBufferSize ?? default;
    }

    /// <summary>
    /// 获取离屏的裁剪遮罩缓冲尺寸.
    /// </summary>
    public System.Numerics.Vector2 GetOffscreenClippingMaskBufferSize()
    {
        return _offscreenClippingManager?.ClippingMaskBufferSize ?? default;
    }

    /// <summary>
    /// 获取绘制对象的裁剪遮罩缓冲(对应官方 GetDrawableMaskBuffer).
    /// </summary>
    public CubismRenderTarget_OpenGLES2 GetDrawableMaskBuffer(int index)
    {
        return _drawableMasks[index];
    }

    /// <summary>
    /// 获取离屏的裁剪遮罩缓冲(对应官方 GetOffscreenMaskBuffer).
    /// </summary>
    public CubismRenderTarget_OpenGLES2 GetOffscreenMaskBuffer(int index)
    {
        return _offscreenMasks[index];
    }

    /// <summary>
    /// 获取 Clipping Mask 缓冲 (旧 API 兼容. 用于 Drawable)
    /// </summary>
    public CubismRenderTarget_OpenGLES2 GetMaskBuffer(int index)
    {
        return GetDrawableMaskBuffer(index);
    }

    /// <summary>
    /// 判定遮罩用渲染目标能否用于绘制.
    /// 创建失败的 RT 由于 RenderTexture == 0, IsValid() 会返回 false. 
    /// 对该判定为 false 的缓冲, 不得执行 Viewport / Clear / Mask 绘制
    /// (执行会破坏当前绑定的 FBO, 即画面或父 FBO). 
    /// </summary>
    /// <param name="type">Drawable 用 / Offscreen 用的区别</param>
    /// <param name="index">Mask 缓冲的索引</param>
    /// <returns>可用时为 true</returns>
    internal bool IsMaskBufferUsable(CsmEnum.DrawableObjectType type, int index)
    {
        List<CubismRenderTarget_OpenGLES2> masks = type == CsmEnum.DrawableObjectType.Offscreen
            ? _offscreenMasks
            : _drawableMasks;

        return index >= 0 && index < masks.Count && masks[index].IsValid();
    }

    /// <summary>
    /// 只输出一次遮罩用渲染目标不可用的警告.
    /// 实际的 Viewport / Clear / Mask 绘制跳过由
    /// <see cref="CubismClippingManager_OpenGLES2.SetupClippingContext"/> 与
    /// 高精度 Mask 路径 (DrawDrawable / DrawOffscreen) 侧执行. 
    /// </summary>
    /// <param name="type">Drawable 用 / Offscreen 用的区别</param>
    internal void WarnMaskBufferUnavailable(CsmEnum.DrawableObjectType type)
    {
        if (type == CsmEnum.DrawableObjectType.Offscreen)
        {
            if (_warnedOffscreenMaskBufferUnavailable)
            {
                return;
            }

            _warnedOffscreenMaskBufferUnavailable = true;
            CubismLog.Warning(
                "[Live2D OpenGL]Offscreen clipping mask render target is not available ({0}x{1}). " +
                "Skip generating clipping masks for offscreens.",
                (int)(_offscreenClippingManager?.ClippingMaskBufferSize.X ?? 0.0f),
                (int)(_offscreenClippingManager?.ClippingMaskBufferSize.Y ?? 0.0f));
            return;
        }

        if (_warnedDrawableMaskBufferUnavailable)
        {
            return;
        }

        _warnedDrawableMaskBufferUnavailable = true;
        CubismLog.Warning(
            "[Live2D OpenGL]Drawable clipping mask render target is not available ({0}x{1}). " +
            "Skip generating clipping masks for drawables.",
            (int)(_drawableClippingManager?.ClippingMaskBufferSize.X ?? 0.0f),
            (int)(_drawableClippingManager?.ClippingMaskBufferSize.Y ?? 0.0f));
    }

    /// <summary>
    /// 检查遮罩用渲染目标是否创建成功, 失败时只输出一次警告.
    /// </summary>
    /// <param name="type">Drawable 用 / Offscreen 用的区别</param>
    private void ValidateMaskBuffers(CsmEnum.DrawableObjectType type)
    {
        List<CubismRenderTarget_OpenGLES2> masks = type == CsmEnum.DrawableObjectType.Offscreen
            ? _offscreenMasks
            : _drawableMasks;

        for (int i = 0; i < masks.Count; ++i)
        {
            if (!masks[i].IsValid())
            {
                WarnMaskBufferUnavailable(type);
                return;
            }
        }
    }

    /// <summary>
    /// 获取当前的离屏帧缓冲.
    /// </summary>
    public CubismOffscreenRenderTarget_OpenGLES2? GetCurrentOffscreen()
    {
        return _currentOffscreen;
    }

    public bool IsGeneratingMask()
    {
        return ClippingContextBufferForMask != null;
    }

    /// <summary>
    /// 拷贝离屏缓冲(对应官方 CopyOffscreenRenderTarget).
    /// </summary>
    public CubismRenderTarget_OpenGLES2? CopyOffscreenRenderTarget()
    {
        return _modelRenderTargets.Count == 0 ? null : CopyRenderTarget(_modelRenderTargets[0]);
    }

    /// <summary>
    /// 拷贝任意缓冲(对应官方 CopyRenderTarget).
    /// 本地不使用 texture barrier, 因此始终用 glBlitFramebuffer 复制
    /// (相当于官方 CanUseTextureBarrier()==false 的路径). 
    /// </summary>
    public CubismRenderTarget_OpenGLES2? CopyRenderTarget(CubismRenderTarget_OpenGLES2 srcBuffer)
    {
        if (_modelRenderTargets.Count < 2)
        {
            return srcBuffer;
        }

        // 拷贝源创建失败时不执行拷贝(调用方按 null 合并为 0 处理).
        if (!srcBuffer.IsValid())
        {
            return null;
        }

        CubismRenderTarget_OpenGLES2 dst = _modelRenderTargets[1];
        if (dst.BufferWidth != srcBuffer.BufferWidth || dst.BufferHeight != srcBuffer.BufferHeight)
        {
            if (!dst.CreateRenderTarget(srcBuffer.BufferWidth, srcBuffer.BufferHeight))
            {
                WarnCopyRenderTargetUnavailable(srcBuffer.BufferWidth, srcBuffer.BufferHeight);
                return null;
            }
        }
        else if (!dst.IsValid())
        {
            WarnCopyRenderTargetUnavailable(srcBuffer.BufferWidth, srcBuffer.BufferHeight);
            return null;
        }

        CubismRenderTarget_OpenGLES2.CopyBuffer(srcBuffer, dst);

        return dst;
    }

    /// <summary>
    /// 只输出一次拷贝目标渲染目标创建失败的警告.
    /// </summary>
    private void WarnCopyRenderTargetUnavailable(int width, int height)
    {
        if (_warnedCopyRenderTargetUnavailable)
        {
            return;
        }

        _warnedCopyRenderTargetUnavailable = true;
        CubismLog.Warning(
            "[Live2D OpenGL]CopyRenderTarget: destination render target is not available ({0}x{1}). " +
            "Blend-mode texture copy is skipped.",
            width, height);
    }

    /// <summary>
    /// [覆盖]
    /// 绘制绘制对象 (美术网格). 
    /// </summary>
    /// <param name="model">绘制目标的模型</param>
    /// <param name="index">绘制目标的网格索引</param>
    internal unsafe void DrawMeshOpenGL(CubismModel model, int index)
    {
        if (_textures[model.GetDrawableTextureIndex(index)] == 0) return;    // 模型引用的纹理未绑定时跳过绘制

        // 背面绘制的启用/禁用
        if (IsCulling)
        {
            GL.Enable(GL.GL_CULL_FACE);
        }
        else
        {
            GL.Disable(GL.GL_CULL_FACE);
        }

        GL.FrontFace(GL.GL_CCW);    // Cubism SDK OpenGL 中 Mask 与美术网格都以 CCW 为正面

        var vertexCount = model.GetDrawableVertexCount(index);
        var vertexArray = model.GetDrawableVertices(index);
        var uvArray = (float*)model.GetDrawableVertexUvs(index);

        GL.BindVertexArray(VertexArray);

        if (vbo == null || vbo.Length < vertexCount)
        {
            vbo = new VBO[vertexCount];
        }

        for (int a = 0; a < vertexCount; a++)
        {
            vbo[a].ver0 = vertexArray[a * 2];
            vbo[a].ver1 = vertexArray[a * 2 + 1];
            vbo[a].uv0 = uvArray[a * 2];
            vbo[a].uv1 = uvArray[a * 2 + 1];
        }

        int indexCount = model.GetDrawableVertexIndexCount(index);
        ushort* indexArray = model.GetDrawableVertexIndices(index);

        GL.BindBuffer(GL.GL_ARRAY_BUFFER, VertexBuffer);
        fixed (void* p = vbo)
        {
            GL.BufferData(GL.GL_ARRAY_BUFFER, vertexCount * sizeof(VBO), new IntPtr(p), GL.GL_STATIC_DRAW);
        }

        GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, IndexBuffer);
        GL.BufferData(GL.GL_ELEMENT_ARRAY_BUFFER, indexCount * sizeof(ushort), new IntPtr(indexArray), GL.GL_STATIC_DRAW);

        if (IsGeneratingMask())  // 生成 Mask 时
        {
            Shader.SetupShaderProgramForMask(this, model, index);
        }
        else
        {
            Shader.SetupShaderProgramForDrawable(this, model, index);
        }

        // 绘制多边形网格
        GL.DrawElements(GL.GL_TRIANGLES, indexCount, GL.GL_UNSIGNED_SHORT, 0);

        // 后处理
        GL.UseProgram(0);
        GL.BindBuffer(GL.GL_ARRAY_BUFFER, 0);
        GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, 0);
        GL.BindVertexArray(0);
        ClippingContextBufferForDrawable = null;
        ClippingContextBufferForMask = null;
    }

    /// <summary>
    /// 设置画面整体四边形的顶点属性 (Copy / Offscreen 合成用). 
    /// </summary>
    /// <param name="shaderSet">使用的 Shader 集合</param>
    /// <param name="reverseUv">是否上下翻转 UV (Offscreen 合成时为 true)</param>
    internal unsafe void SetRenderTargetVertexAttributes(CubismShaderSet shaderSet, bool reverseUv)
    {
        GL.BindVertexArray(VertexArray);

        GL.BindBuffer(GL.GL_ARRAY_BUFFER, _quadVertexBuffer);
        fixed (void* p = reverseUv ? QuadVerticesReverseUv : QuadVertices)
        {
            GL.BufferData(GL.GL_ARRAY_BUFFER, QuadVertices.Length * sizeof(VBO), new IntPtr(p), GL.GL_STATIC_DRAW);
        }

        GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, _quadIndexBuffer);
        fixed (void* p = QuadIndices)
        {
            GL.BufferData(GL.GL_ELEMENT_ARRAY_BUFFER, QuadIndices.Length * sizeof(ushort), new IntPtr(p), GL.GL_STATIC_DRAW);
        }

        GL.EnableVertexAttribArray(shaderSet.AttributePositionLocation);
        GL.VertexAttribPointer(shaderSet.AttributePositionLocation, 2, GL.GL_FLOAT, false, 4 * sizeof(float), 0);

        GL.EnableVertexAttribArray(shaderSet.AttributeTexCoordLocation);
        GL.VertexAttribPointer(shaderSet.AttributeTexCoordLocation, 2, GL.GL_FLOAT, false, 4 * sizeof(float), 2 * sizeof(float));
    }

    /// <summary>
    /// 绘制画面整体四边形. 
    /// </summary>
    internal void DrawRenderTargetQuad()
    {
        GL.DrawElements(GL.GL_TRIANGLES, QuadIndices.Length, GL.GL_UNSIGNED_SHORT, 0);
    }

    /// <summary>
    /// 绘制开始时的追加处理. 
    /// 在绘制模型前实现 Clipping Mask 所需的处理. 
    /// </summary>
    internal void PreDraw()
    {
        GL.Disable(GL.GL_SCISSOR_TEST);
        GL.Disable(GL.GL_STENCIL_TEST);
        GL.Disable(GL.GL_DEPTH_TEST);

        GL.Enable(GL.GL_BLEND);
        GL.ColorMask(true, true, true, true);

        if (GL.IsPhoneES2)
        {
            GL.BindVertexArrayOES(0);
        }

        GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, 0);
        GL.BindBuffer(GL.GL_ARRAY_BUFFER, 0); // 之前若有缓冲被绑定, 则需要丢弃

        // 各向异性过滤. 依平台的 OpenGL 可能不支持, 因此未设置时不进行设置
        if (Anisotropy > 0.0f)
        {
            for (int i = 0; i < _textures.Count; i++)
            {
                GL.BindTexture(GL.GL_TEXTURE_2D, _textures[i]);
                GL.TexParameterf(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAX_ANISOTROPY_EXT, Anisotropy);
            }
        }
    }

    /// <summary>
    /// 保存模型绘制前的 OpenGLES2 状态
    /// </summary>
    protected override void SaveProfile()
    {
        _rendererProfile.Save();
    }

    /// <summary>
    /// 恢复模型绘制前的 OpenGLES2 状态
    /// </summary>
    protected override void RestoreProfile()
    {
        _rendererProfile.Restore();
    }

    /// <summary>
    /// 模型绘制前的离屏设置(对应官方 BeforeDrawModelRenderTarget).
    /// </summary>
    protected override void BeforeDrawModelRenderTarget()
    {
        if (_modelRenderTargets.Count == 0)
        {
            return;
        }

        // 宿主调用 SetRenderTargetSize 之前尺寸为 0x0, 因此不创建.
        // 但尚未接入尺寸设置的宿主会以未设置状态进入, 因此用当前视口尺寸
        // 初始化的回退方案(与官方的差异; 接入 SetRenderTargetSize 之后仍然无害).
        if (_modelRenderTargetWidth == 0 || _modelRenderTargetHeight == 0)
        {
            GL.GetIntegerv(GL.GL_VIEWPORT, _viewportBuffer);
            if (_viewportBuffer[2] <= 0 || _viewportBuffer[3] <= 0)
            {
                return;
            }

            SetRenderTargetSize((uint)_viewportBuffer[2], (uint)_viewportBuffer[3]);
        }

        // Offscreen 缓冲尺寸不同时重建
        for (int i = 0; i < _modelRenderTargets.Count; ++i)
        {
            if (_modelRenderTargets[i].GetBufferWidth() != _modelRenderTargetWidth ||
                _modelRenderTargets[i].GetBufferHeight() != _modelRenderTargetHeight)
            {
                _modelRenderTargets[i].CreateRenderTarget((int)_modelRenderTargetWidth, (int)_modelRenderTargetHeight, 0);
            }
        }

        // 帧缓冲创建失败时放弃离屏绘制, 直接绘制到当前帧缓冲.
        //     (若保持 RenderTexture == 0 绘制, 会画到前一个 FBO 上, 导致绘制结果不确定)
        if (!_modelRenderTargets[0].IsValid())
        {
            if (!_warnedModelRenderTargetUnavailable)
            {
                _warnedModelRenderTargetUnavailable = true;
                CubismLog.Warning(
                    "[Live2D OpenGL]BeforeDrawModelRenderTarget: render target is not available ({0}x{1}). " +
                    "Fall back to drawing the model into the current framebuffer.",
                    _modelRenderTargetWidth, _modelRenderTargetHeight);
            }

            return;
        }

        // 开始绘制到另一个缓冲
        _modelRenderTargets[0].BeginDraw();
        GL.Viewport(0, 0, (int)_modelRenderTargetWidth, (int)_modelRenderTargetHeight);
        _modelRenderTargets[0].Clear(0.0f, 0.0f, 0.0f, 0.0f);
    }

    /// <summary>
    /// 模型绘制后的离屏设置(对应官方 AfterDrawModelRenderTarget).
    /// </summary>
    protected override void AfterDrawModelRenderTarget()
    {
        if (_modelRenderTargets.Count == 0 || !_modelRenderTargets[0].IsValid())
        {
            return;
        }

        // 绘制到原缓冲
        _modelRenderTargets[0].EndDraw();

        Shader.SetupShaderProgramForOffscreenRenderTarget(this);
        DrawRenderTargetQuad();

        GL.UseProgram(0);
    }

    /// <summary>
    /// 模型绘制的实现 (官方 DoDrawModel, .cpp:719-789). 
    /// </summary>
    protected override unsafe void DoDrawModel()
    {
        BeforeDrawModelRenderTarget();

        // 模型绘制前保存 FBO 与视口(复用成员缓冲, 不经过 Profile).
        GL.GetIntegerv(GL.GL_FRAMEBUFFER_BINDING, out int lastFBO);
        int[] lastViewport = _viewportBuffer;
        GL.GetIntegerv(GL.GL_VIEWPORT, lastViewport);

        //------------ Drawable 用 Clipping Mask 缓冲预处理方式的情况 ------------
        if (_drawableClippingManager != null)
        {
            PreDraw();

            // 尺寸不同时在此重新创建
            for (int i = 0; i < _drawableClippingManager.RenderTextureCount; ++i)
            {
                if (_drawableMasks[i].BufferWidth != (int)_drawableClippingManager.ClippingMaskBufferSize.X ||
                    _drawableMasks[i].BufferHeight != (int)_drawableClippingManager.ClippingMaskBufferSize.Y)
                {
                    _drawableMasks[i].CreateRenderTarget(
                        (int)_drawableClippingManager.ClippingMaskBufferSize.X, (int)_drawableClippingManager.ClippingMaskBufferSize.Y);
                }
            }

            // 遮罩用帧缓冲创建失败时只输出一次警告.
            //      实际的 Clear / Viewport / Mask 绘制跳过由各绘制路径侧执行. 
            ValidateMaskBuffers(CsmEnum.DrawableObjectType.Drawable);

            if (UseHighPrecisionMask)
            {
                _drawableClippingManager.SetupMatrixForHighPrecision(Model, false, CsmEnum.DrawableObjectType.Drawable);
            }
            else
            {
                _drawableClippingManager.SetupClippingContext(Model, this, lastFBO, lastViewport);
            }
        }

        //------------ Offscreen 用 Clipping Mask 缓冲预处理方式的情况 ------------
        if (_offscreenClippingManager != null)
        {
            PreDraw();

            for (int i = 0; i < _offscreenClippingManager.RenderTextureCount; ++i)
            {
                if (_offscreenMasks[i].BufferWidth != (int)_offscreenClippingManager.ClippingMaskBufferSize.X ||
                    _offscreenMasks[i].BufferHeight != (int)_offscreenClippingManager.ClippingMaskBufferSize.Y)
                {
                    _offscreenMasks[i].CreateRenderTarget(
                        (int)_offscreenClippingManager.ClippingMaskBufferSize.X, (int)_offscreenClippingManager.ClippingMaskBufferSize.Y);
                }
            }

            // 遮罩用帧缓冲创建失败时只输出一次警告.
            ValidateMaskBuffers(CsmEnum.DrawableObjectType.Offscreen);

            if (UseHighPrecisionMask)
            {
                _offscreenClippingManager.SetupMatrixForHighPrecision(Model, false, CsmEnum.DrawableObjectType.Offscreen, GetMvpMatrix());
            }
            else
            {
                _offscreenClippingManager.SetupClippingContext(Model, this, lastFBO, lastViewport);
            }
        }

        // 注意上述 Clipping 处理中也会调用一次 PreDraw!!
        PreDraw();

        if (GL.AlwaysClear)
        {
            var color = _lapp.BGColor;
            GL.ClearColor(color.R, color.G, color.B, color.A);
            GL.Clear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);
        }

        // 按模型的绘制顺序进行绘制
        DrawObjectLoop(lastFBO, lastViewport);

        AfterDrawModelRenderTarget();
    }

    /// <summary>
    /// 绘制绘制对象 (美术网格, Offscreen) 的循环处理
    /// (官方 DrawObjectLoop, .cpp:791-833). 
    /// 实测结论: renderOrder 的数组长度为 drawableCount + offscreenCount(Ren 模型为 198+24=222).
    /// </summary>
    private unsafe void DrawObjectLoop(int lastFBO, int[] lastViewport)
    {
        int drawableCount = Model.GetDrawableCount();
        int offscreenCount = Model.GetOffscreenCount();
        int totalCount = drawableCount + offscreenCount;
        int* renderOrder = Model.GetRenderOrders();

        _currentOffscreen = null;
        _currentFBO = lastFBO;
        _modelRootFBO = lastFBO;

        // 按绘制顺序对索引排序 (整合 Drawable 与 Offscreen)
        for (int i = 0; i < totalCount; ++i)
        {
            int order = renderOrder[i];

            if (i < drawableCount)
            {
                _sortedObjectsIndexList[order] = i;
                _sortedObjectsTypeList[order] = CsmEnum.DrawableObjectType.Drawable;
            }
            else
            {
                _sortedObjectsIndexList[order] = i - drawableCount;
                _sortedObjectsTypeList[order] = CsmEnum.DrawableObjectType.Offscreen;
            }
        }

        // 绘制
        for (int i = 0; i < totalCount; ++i)
        {
            int objectIndex = _sortedObjectsIndexList[i];
            CsmEnum.DrawableObjectType objectType = _sortedObjectsTypeList[i];

            RenderObject(objectIndex, objectType);
        }

        while (_currentOffscreen != null)
        {
            // 若还残留 Offscreen, 则向父 Offscreen 传播
            SubmitDrawToParentOffscreen(_currentOffscreen.GetOffscreenIndex(), CsmEnum.DrawableObjectType.Offscreen);
        }
    }

    /// <summary>
    /// 调用各对象的绘制处理 (官方 RenderObject, .cpp:835-852). 
    /// </summary>
    private void RenderObject(int objectIndex, CsmEnum.DrawableObjectType objectType)
    {
        switch (objectType)
        {
            case CsmEnum.DrawableObjectType.Drawable:
                DrawDrawable(objectIndex);
                break;
            case CsmEnum.DrawableObjectType.Offscreen:
                AddOffscreen(objectIndex);
                break;
            default:
                CubismLog.Error($"[Live2D OpenGL]Unknown drawable type: {objectType}");
                break;
        }
    }

    /// <summary>
    /// 绘制绘制对象 (美术网格) (官方 DrawDrawable, .cpp:854-930). 
    /// </summary>
    private unsafe void DrawDrawable(int drawableIndex)
    {
        // Drawable 不在显示状态时跳过处理
        if (!Model.GetDrawableDynamicFlagIsVisible(drawableIndex))
        {
            return;
        }

        SubmitDrawToParentOffscreen(drawableIndex, CsmEnum.DrawableObjectType.Drawable);

        // Clipping Mask
        CubismClippingContext? clipContext = _drawableClippingManager != null
            ? _drawableClippingManager.ClippingContextListForDraw[drawableIndex]
            : null;

        // 遮罩用渲染目标未创建成功时, 不执行 Viewport / Clear / 遮罩绘制.
        //      RenderTexture == 0 时执行会破坏当前绑定的 FBO (画面或父 FBO). 
        if (clipContext != null && UseHighPrecisionMask
            && IsMaskBufferUsable(CsmEnum.DrawableObjectType.Drawable, clipContext.BufferIndex)) // 需要写入 Mask
        {
            int[] preHighPrecisionMaskViewport = _highPrecisionMaskViewportBuffer;
            GL.GetIntegerv(GL.GL_VIEWPORT, preHighPrecisionMaskViewport);

            if (clipContext.IsUsing) // 本应写入
            {
                // 按与生成的 RenderTarget 相同的尺寸设置视口
                GL.Viewport(0, 0, (int)_drawableClippingManager!.ClippingMaskBufferSize.X, (int)_drawableClippingManager.ClippingMaskBufferSize.Y);

                PreDraw(); // 清除缓冲

                // ---------- Mask 绘制处理 ----------
                // 将用于 Mask 的 RenderTexture 设为 active
                GetDrawableMaskBuffer(clipContext.BufferIndex).BeginDraw(_currentFBO);

                // 清除 Mask
                // 1 为无效 (不被绘制) 区域, 0 为有效 (被绘制) 区域. 
                GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
                GL.Clear(GL.GL_COLOR_BUFFER_BIT);
            }

            {
                int clipDrawCount = clipContext.ClippingIdCount;
                for (int index = 0; index < clipDrawCount; index++)
                {
                    int clipDrawIndex = clipContext.ClippingIdList[index];

                    // 顶点信息未更新, 不可靠时跳过绘制
                    if (!Model.GetDrawableDynamicFlagVertexPositionsDidChange(clipDrawIndex))
                    {
                        continue;
                    }

                    IsCulling = Model.GetDrawableCulling(clipDrawIndex) != 0;

                    // 应用本次专用的变换后绘制
                    // 还需要切换通道(A,R,G,B)
                    ClippingContextBufferForMask = clipContext;

                    DrawMeshOpenGL(Model, clipDrawIndex);
                }
            }

            {
                // --- 后处理 ---
                GetDrawableMaskBuffer(clipContext.BufferIndex).EndDraw();
                ClippingContextBufferForMask = null;
                GL.Viewport(preHighPrecisionMaskViewport[0], preHighPrecisionMaskViewport[1],
                    preHighPrecisionMaskViewport[2], preHighPrecisionMaskViewport[3]);

                PreDraw(); // 清除缓冲
            }
        }
        else if (clipContext != null && UseHighPrecisionMask)
        {
            // 遮罩用渲染目标不可用, 跳过遮罩生成(不要把 Clear 落到屏幕或父 FBO 上).
            WarnMaskBufferUnavailable(CsmEnum.DrawableObjectType.Drawable);
        }

        // 设置 Clipping Mask
        ClippingContextBufferForDrawable = clipContext;

        IsCulling = Model.GetDrawableCulling(drawableIndex) != 0;

        DrawMeshOpenGL(Model, drawableIndex);
    }

    /// <summary>
    /// 尝试将 Offscreen 的绘制结果传播到父 Offscreen
    /// (官方 SubmitDrawToParentOffscreen, .cpp:932-984). 
    /// </summary>
    private unsafe void SubmitDrawToParentOffscreen(int objectIndex, CsmEnum.DrawableObjectType objectType)
    {
        if (_currentOffscreen == null || objectIndex == CubismModel.CubismNoIndex_Offscreen)
        {
            return;
        }

        int* ownerIndices = Model.GetOffscreenOwnerIndices();
        int currentOwnerIndex = ownerIndices[_currentOffscreen.GetOffscreenIndex()];

        // owner 不明时结束处理
        if (currentOwnerIndex == CubismModel.CubismNoIndex_Offscreen)
        {
            return;
        }

        int targetParentIndex;
        // 按绘制对象的类型取得父部件的索引
        switch (objectType)
        {
            case CsmEnum.DrawableObjectType.Drawable:
                targetParentIndex = Model.GetDrawableParentPartIndex(objectIndex);
                break;
            case CsmEnum.DrawableObjectType.Offscreen:
                targetParentIndex = Model.GetPartParentPartIndex(ownerIndices[objectIndex]);
                break;
            default:
                return;
        }

        // 沿层级向上追溯, 若存在当前 Offscreen 的 owner 部件则结束处理. 
        while (targetParentIndex != CubismModel.CubismNoIndex_Parent)
        {
            // 对象的父级与当前 owner 相同时结束处理
            if (targetParentIndex == currentOwnerIndex)
            {
                return;
            }

            targetParentIndex = Model.GetPartParentPartIndex(targetParentIndex);
        }

        // 调用方的绘制对象不是当前 Offscreen 的绘制目标. 
        // 也就是说, 按绘制顺序组的规格, 当前 Offscreen 的所有绘制目标都已完成绘制, 因此
        // 绘制当前 Offscreen. 
        DrawOffscreen(_currentOffscreen);

        // 若还能向父 Offscreen 传播则进行传播. 
        SubmitDrawToParentOffscreen(objectIndex, objectType);
    }

    /// <summary>
    /// 追加绘制对象 (Offscreen) (官方 AddOffscreen, .cpp:986-1041). 
    /// </summary>
    private unsafe void AddOffscreen(int offscreenIndex)
    {
        int* ownerIndices = Model.GetOffscreenOwnerIndices();

        // 追加将之前的 Offscreen 渲染目标传播到父级的处理
        if (_currentOffscreen != null && _currentOffscreen.GetOffscreenIndex() != offscreenIndex)
        {
            bool isParent = false;
            int ownerIndex = ownerIndices[offscreenIndex];
            int parentIndex = Model.GetPartParentPartIndex(ownerIndex);

            int currentOffscreenIndex = _currentOffscreen.GetOffscreenIndex();
            int currentOffscreenOwnerIndex = ownerIndices[currentOffscreenIndex];
            while (parentIndex != CubismModel.CubismNoIndex_Parent)
            {
                if (parentIndex == currentOffscreenOwnerIndex)
                {
                    isParent = true;
                    break;
                }
                parentIndex = Model.GetPartParentPartIndex(parentIndex);
            }

            if (!isParent)
            {
                // 若存在当前的 Offscreen 渲染目标, 则传播到父级
                SubmitDrawToParentOffscreen(offscreenIndex, CsmEnum.DrawableObjectType.Offscreen);
            }
        }

        CubismOffscreenRenderTarget_OpenGLES2 offscreen = _offscreenList[offscreenIndex];
        offscreen.SetOffscreenRenderTarget(_modelRenderTargetWidth, _modelRenderTargetHeight);

        // 帧缓冲创建失败时跳过该离屏的绘制.
        // 若保持 RenderTexture == 0 继续 BeginDraw / Clear / Draw, 会绘制到前一个 FBO (包括画面), 
        // 导致绘制结果不确定. 
        CubismRenderTarget_OpenGLES2? offscreenTarget = offscreen.GetRenderTarget();
        if (offscreenTarget == null || !offscreenTarget.IsValid())
        {
            // 必须归还 SetOffscreenRenderTarget 借出的池槽位.
            //      若不归还, slot 会保持 IsUsed 状态, 之后再也无法借用该 slot
            //      (= slot 永久占用. 使用数计数也不会恢复). 
            offscreen.StopUsingRenderTexture();

            if (!_warnedOffscreenRenderTargetUnavailable)
            {
                _warnedOffscreenRenderTargetUnavailable = true;
                CubismLog.Warning(
                    "[Live2D OpenGL]AddOffscreen: render target for offscreen {0} is not available ({1}x{2}). " +
                    "Skip drawing into this offscreen.",
                    offscreenIndex, _modelRenderTargetWidth, _modelRenderTargetHeight);
            }

            return;
        }

        // 获取之前的 Offscreen 渲染目标
        CubismOffscreenRenderTarget_OpenGLES2? oldOffscreen = offscreen.GetParentPartOffscreen();

        offscreen.SetOldOffscreen(oldOffscreen);

        int oldFBO = 0;
        if (oldOffscreen != null)
        {
            oldFBO = oldOffscreen.GetRenderTarget()?.RenderTexture ?? 0;
        }

        if (oldFBO == 0)
        {
            oldFBO = _modelRootFBO; // 使用根 FBO
        }

        // 开始绘制到另一个缓冲
        CubismRenderTarget_OpenGLES2 renderTarget = offscreenTarget;
        renderTarget.BeginDraw(oldFBO);
        GL.Viewport(0, 0, (int)_modelRenderTargetWidth, (int)_modelRenderTargetHeight);
        renderTarget.Clear(0.0f, 0.0f, 0.0f, 0.0f);

        // 设置当前的 Offscreen 渲染目标
        _currentOffscreen = offscreen;
        _currentFBO = renderTarget.RenderTexture;
    }

    /// <summary>
    /// 绘制绘制对象 (Offscreen) (官方 DrawOffscreen, .cpp:1043-1112). 
    /// </summary>
    private unsafe void DrawOffscreen(CubismOffscreenRenderTarget_OpenGLES2 currentOffscreen)
    {
        int offscreenIndex = currentOffscreen.GetOffscreenIndex();

        // 绘制目标(FBO)创建失败的离屏不参与合成.
        //      AddOffscreen 失败时尚未切换 FBO, 因此保持当前绘制目标不变, 
        //      只将状态回退一层到父级 (同时防止 GetRenderTarget() 为 null 时的异常). 
        if (currentOffscreen.GetRenderTarget() is not { } offscreenTarget || offscreenTarget.IsValid() is false)
        {
            _currentOffscreen = _currentOffscreen?.GetOldOffscreen();
            ClippingContextBufferForOffscreen = null;
            ClippingContextBufferForMask = null;
            return;
        }

        // Clipping Mask
        CubismClippingContext? clipContext = _offscreenClippingManager != null
            ? _offscreenClippingManager.ClippingContextListForOffscreen[offscreenIndex]
            : null;

        // 遮罩用渲染目标未创建成功时, 不执行 Viewport / Clear / 遮罩绘制.
        //      RenderTexture == 0 时执行会破坏当前绑定的 FBO (父 Offscreen). 
        if (clipContext != null && UseHighPrecisionMask
            && IsMaskBufferUsable(CsmEnum.DrawableObjectType.Offscreen, clipContext.BufferIndex)) // 需要写入 Mask
        {
            int[] preHighPrecisionMaskViewport = _highPrecisionMaskViewportBuffer;
            GL.GetIntegerv(GL.GL_VIEWPORT, preHighPrecisionMaskViewport);

            if (clipContext.IsUsing) // 本应写入
            {
                // 按与生成的 RenderTarget 相同的尺寸设置视口
                GL.Viewport(0, 0, (int)_offscreenClippingManager!.ClippingMaskBufferSize.X, (int)_offscreenClippingManager.ClippingMaskBufferSize.Y);

                PreDraw(); // 清除缓冲

                // ---------- Mask 绘制处理 ----------
                // 将用于 Mask 的 RenderTexture 设为 active
                GetOffscreenMaskBuffer(clipContext.BufferIndex).BeginDraw(_currentFBO);

                // 清除 Mask
                GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
                GL.Clear(GL.GL_COLOR_BUFFER_BIT);
            }

            {
                int clipDrawCount = clipContext.ClippingIdCount;
                for (int index = 0; index < clipDrawCount; index++)
                {
                    int clipDrawIndex = clipContext.ClippingIdList[index];

                    // 顶点信息未更新, 不可靠时跳过绘制
                    if (!Model.GetDrawableDynamicFlagVertexPositionsDidChange(clipDrawIndex))
                    {
                        continue;
                    }

                    IsCulling = Model.GetDrawableCulling(clipDrawIndex) != 0;

                    // 应用本次专用的变换后绘制
                    // 还需要切换通道(A,R,G,B)
                    ClippingContextBufferForMask = clipContext;

                    DrawMeshOpenGL(Model, clipDrawIndex);
                }
            }

            {
                // --- 后处理 ---
                GetOffscreenMaskBuffer(clipContext.BufferIndex).EndDraw();
                ClippingContextBufferForMask = null;
                GL.Viewport(preHighPrecisionMaskViewport[0], preHighPrecisionMaskViewport[1],
                    preHighPrecisionMaskViewport[2], preHighPrecisionMaskViewport[3]);

                PreDraw(); // 清除缓冲
            }
        }
        else if (clipContext != null && UseHighPrecisionMask)
        {
            // 遮罩用渲染目标不可用, 跳过遮罩生成(不要把 Clear 落到父 FBO 上).
            WarnMaskBufferUnavailable(CsmEnum.DrawableObjectType.Offscreen);
        }

        // 设置 Clipping Mask
        ClippingContextBufferForOffscreen = clipContext;

        IsCulling = Model.GetOffscreenCulling(offscreenIndex) != 0;

        DrawOffscreenOpenGL(Model, currentOffscreen);
    }

    /// <summary>
    /// 在画面上绘制 Offscreen (官方 DrawOffscreenOpenGL, .cpp:1166-1202). 
    /// </summary>
    private void DrawOffscreenOpenGL(CubismModel model, CubismOffscreenRenderTarget_OpenGLES2 offscreen)
    {
        // 背面绘制的启用/禁用
        if (IsCulling)
        {
            GL.Enable(GL.GL_CULL_FACE);
        }
        else
        {
            GL.Disable(GL.GL_CULL_FACE);
        }

        GL.FrontFace(GL.GL_CCW);    // Cubism SDK OpenGL 中 Mask 与美术网格都以 CCW 为正面

        var renderTarget = offscreen.GetRenderTarget()!;
        renderTarget.EndDraw();
        _currentOffscreen = _currentOffscreen?.GetOldOffscreen();
        _currentFBO = renderTarget.GetOldFBO();

        Shader.SetupShaderProgramForOffscreen(this, model, offscreen);

        // 绘制多边形网格
        DrawRenderTargetQuad();

        // 后处理
        offscreen.StopUsingRenderTexture();
        GL.UseProgram(0);
        ClippingContextBufferForOffscreen = null;
        ClippingContextBufferForMask = null;
    }

    public int GetBindedTextureId(int textureId)
    {
        return (_textures[textureId] != 0) ? _textures[textureId] : -1;
    }

    /// <summary>
    /// 释放渲染器持有的资源 (包含 §2.F 漏点①的修正). 
    /// </summary>
    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        // Shader 程序
        Shader.ReleaseShaderProgram();

        // Clipping 管理器 (含 Mask 缓冲)
        _drawableClippingManager?.Dispose();
        _drawableClippingManager = null;
        _offscreenClippingManager?.Dispose();
        _offscreenClippingManager = null;

        // 模型整体的渲染目标
        foreach (var target in _modelRenderTargets)
        {
            target.DestroyRenderTarget();
        }
        _modelRenderTargets.Clear();

        // Drawable / Offscreen 的 Mask
        foreach (var mask in _drawableMasks)
        {
            mask.DestroyRenderTarget();
        }
        _drawableMasks.Clear();

        foreach (var mask in _offscreenMasks)
        {
            mask.DestroyRenderTarget();
        }
        _offscreenMasks.Clear();

        // 离屏(只释放自有的渲染目标).
        // 官方析构函数(Framework/Rendering/OpenGL/CubismRenderer_OpenGLES2.cpp:494-525)只销毁
        // 自己持有的 _modelRenderTargets / _drawableMasks / _offscreenMasks,
        // 不触碰共享单例 CubismOffscreenManager_OpenGLES2 的渲染目标.
        // manager 的释放由应用层(官方 Samples/OpenGL/.../LAppLive2DManager.cpp:77 的 ReleaseInstance)负责.
        // 此处只将本 renderer 借用的 target 恢复为已结束使用状态
        // (其他 renderer 正在使用的 target 不在 StopUsingRenderTexture 的对象内, 因此不会误伤). 
        foreach (var offscreen in _offscreenList)
        {
            offscreen.StopUsingRenderTexture();
        }
        _offscreenList.Clear();

        // GL 对象 (§2.F 漏点①: 此前未被释放)
        GL.DeleteBuffer(VertexBuffer);
        GL.DeleteBuffer(IndexBuffer);
        GL.DeleteBuffer(_quadVertexBuffer);
        GL.DeleteBuffer(_quadIndexBuffer);
        GL.DeleteVertexArray(VertexArray);
    }
}
