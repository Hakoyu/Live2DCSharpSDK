using System.Numerics;
using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.Framework.Model;

public class CubismModel : IDisposable
{
    /// <summary>
    /// 不存在父级时的索引值
    /// </summary>
    public const int CubismNoIndex_Parent = -1;

    /// <summary>
    /// 引用的 Offscreen 不存在时的索引值
    /// </summary>
    public const int CubismNoIndex_Offscreen = -1;

    /// <summary>
    /// 网格顶点的偏移值
    /// </summary>
    public const int VertexOffset = 0;

    /// <summary>
    /// 网格顶点的步进值
    /// </summary>
    public const int VertexStep = 2;

    /// <summary>
    /// 用于管理纹理剔除设置的结构体 (官方 CubismModel::CullingData)
    /// </summary>
    public struct CullingData
    {
        /// <summary>
        /// 是否用 SDK 侧的设置覆盖
        /// </summary>
        public bool IsOverridden;

        /// <summary>
        /// 剔除信息 (0: 无效 / 1: 有效)
        /// </summary>
        public int IsCulling;

        /// <summary>
        /// 构造函数.
        /// </summary>
        public CullingData(bool isOverridden, int isCulling)
        {
            IsOverridden = isOverridden;
            IsCulling = isCulling;
        }
    }

    /// <summary>
    /// 用于管理参数重复设置覆盖的结构体 (官方 CubismModel::ParameterRepeatData)
    /// </summary>
    public struct ParameterRepeatData
    {
        /// <summary>
        /// 是否用 SDK 侧的设置覆盖
        /// </summary>
        public bool IsOverridden;

        /// <summary>
        /// 是否重复
        /// </summary>
        public bool IsParameterRepeated;

        /// <summary>
        /// 构造函数.
        /// </summary>
        public ParameterRepeatData(bool isOverridden, bool isParameterRepeated)
        {
            IsOverridden = isOverridden;
            IsParameterRepeated = isParameterRepeated;
        }
    }

    /// <summary>
    /// 模型句柄(Dispose 后为 <see cref="IntPtr.Zero"/>, 已释放的指针不再暴露给外部).
    /// </summary>
    public IntPtr Model { get; private set; }

    /// <summary>
    /// 是否已 Dispose(防止二次释放的幂等保护).
    /// </summary>
    private bool _disposed;

    public readonly List<string> ParameterIds = [];
    public readonly List<string> PartIds = [];
    public readonly List<string> DrawableIds = [];

    /// <summary>
    /// 不存在的部件的透明度列表
    /// </summary>
    protected readonly Dictionary<int, float> _notExistPartOpacities = [];

    /// <summary>
    /// 不存在的部件的 ID 列表
    /// </summary>
    protected readonly Dictionary<string, int> _notExistPartId = [];

    /// <summary>
    /// 不存在的参数的值列表
    /// </summary>
    protected readonly Dictionary<int, float> _notExistParameterValues = [];

    /// <summary>
    /// 不存在的参数的 ID 列表
    /// </summary>
    protected readonly Dictionary<string, int> _notExistParameterId = [];

    /// <summary>
    /// 保存的参数
    /// </summary>
    protected readonly List<float> _savedParameters = [];

    /// <summary>
    /// 参数的值列表
    /// </summary>
    protected unsafe float* _parameterValues;

    /// <summary>
    /// 参数的最大值列表
    /// </summary>
    protected unsafe float* _parameterMaximumValues;

    /// <summary>
    /// 参数的最小值列表
    /// </summary>
    protected unsafe float* _parameterMinimumValues;

    /// <summary>
    /// 部件的透明度列表
    /// </summary>
    protected unsafe float* _partOpacities;

    /// <summary>
    /// 模型的透明度
    /// </summary>
    protected float _modelOpacity;

    /// <summary>
    /// 部件层级列表 (5-r.5 中由 _partChildDrawables 替换. 禁止重新赋值: 乘算色类持有该引用)
    /// </summary>
    protected readonly List<CubismModelPartInfo> _partsHierarchy = [];

    /// <summary>
    /// 参数重复设置的列表 (为了原地更新结构体, 使用数组)
    /// </summary>
    protected ParameterRepeatData[] _userParameterRepeatDataList = [];

    /// <summary>
    /// Drawable 剔除设置的列表 (为了原地更新结构体, 使用数组)
    /// </summary>
    protected CullingData[] _userDrawableCullings = [];

    /// <summary>
    /// Offscreen 剔除设置的列表 (为了原地更新结构体, 使用数组)
    /// </summary>
    protected CullingData[] _userOffscreenCullings = [];

    /// <summary>
    /// 乘算色与屏幕色 (5-r.5 中分离为独立类)
    /// </summary>
    protected readonly CubismModelMultiplyAndScreenColor _overrideMultiplyAndScreenColors;

    /// <summary>
    /// 是否在 SDK 侧覆盖参数的重复设置 (官方 5-r.5 的初始值为 true)
    /// </summary>
    protected bool _isOverriddenParameterRepeat;

    /// <summary>
    /// 是否覆盖模型的全部剔除设置?
    /// </summary>
    protected bool _isOverriddenCullings;

    /// <summary>
    /// 是否使用混合模式
    /// </summary>
    protected bool _isBlendModeEnabled;

    /// <summary>
    /// 所属上下文 (提供 ID 管理器与内存分配器).
    /// </summary>
    public CubismContext Context { get; }

    /// <summary>
    /// 构造函数 (官方为 protected, 只能由 CubismMoc 生成. C# 由同一程序集中的 CubismMoc 生成)
    /// </summary>
    /// <param name="context">所属上下文 (提供 ID 管理器与内存分配器)</param>
    /// <param name="model">Core 生成的模型句柄</param>
    /// <exception cref="ArgumentNullException">context 为 null 时抛出</exception>
    protected internal unsafe CubismModel(CubismContext context, IntPtr model)
    {
        if (context is null)
        {
            throw new ArgumentNullException(
                nameof(context),
                "CubismModel requires a context instance."
            );
        }

        Context = context;
        Model = model;
        _modelOpacity = 1.0f;

        // 与官方在构造函数初始化列表中传入 this 和 _partsHierarchy 的做法等价.
        // 此时 _partsHierarchy 为空, 但引用会在 Initialize() 之后填充, 因此没有问题.
        _overrideMultiplyAndScreenColors = new CubismModelMultiplyAndScreenColor(
            this,
            _partsHierarchy
        );

        _isOverriddenParameterRepeat = true;
        _isOverriddenCullings = false;
        _isBlendModeEnabled = false;

        Initialize();
    }

    /// <summary>
    /// 释放模型(幂等, 同一实例多次 Dispose 不会二次释放).
    /// </summary>
    /// <remarks>
    /// 为了在释放前把 <see cref="Model"/> 置为 <see cref="IntPtr.Zero"/>, 之后的访问器
    /// 全部通过 "Model == IntPtr.Zero" 的守卫返回安全值, 不会触碰 Core 已释放的区域.
    /// 子对象 (<see cref="CubismModelMultiplyAndScreenColor"/> 等) 不持有
    /// 非托管指针, 只持有对模型的托管引用与边界检查, 因此不会成为野指针.
    /// </remarks>
    public virtual void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var model = Model;
        Model = IntPtr.Zero;

        if (model != IntPtr.Zero)
        {
            Context.Allocator.DeallocateAligned(model);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 模型的初始化 (官方 CubismModel::Initialize)
    /// </summary>
    protected unsafe void Initialize()
    {
        if (Model == IntPtr.Zero)
        {
            return;
        }

        _parameterValues = CubismCore.GetParameterValues(Model);
        _partOpacities = CubismCore.GetPartOpacities(Model);
        _parameterMaximumValues = CubismCore.GetParameterMaximumValues(Model);
        _parameterMinimumValues = CubismCore.GetParameterMinimumValues(Model);

        {
            var parameterIds = CubismCore.GetParameterIds(Model);
            var parameterCount = CubismCore.GetParameterCount(Model);

            _userParameterRepeatDataList = new ParameterRepeatData[parameterCount];

            for (int i = 0; i < parameterCount; ++i)
            {
                var str = new string(parameterIds[i]);
                ParameterIds.Add(Context.IdManager.GetId(str));

                _userParameterRepeatDataList[i] = new ParameterRepeatData(false, false);
            }
        }

        var partCount = CubismCore.GetPartCount(Model);
        var drawableCount = CubismCore.GetDrawableCount(Model);
        var offscreenCount = CubismCore.GetOffscreenCount(Model);

        // Part
        {
            var partIds = CubismCore.GetPartIds(Model);

            for (int i = 0; i < partCount; ++i)
            {
                var str = new string(partIds[i]);
                PartIds.Add(Context.IdManager.GetId(str));
            }
        }

        // Drawable
        {
            var drawableIds = CubismCore.GetDrawableIds(Model);

            _userDrawableCullings = new CullingData[drawableCount];

            for (int i = 0; i < drawableCount; ++i)
            {
                var str = new string(drawableIds[i]);
                DrawableIds.Add(Context.IdManager.GetId(str));
                _userDrawableCullings[i] = new CullingData(false, 0);
            }
        }

        // Offscreen
        _userOffscreenCullings = new CullingData[offscreenCount];
        for (int i = 0; i < offscreenCount; ++i)
        {
            _userOffscreenCullings[i] = new CullingData(false, 0);
        }

        // Multiply and Screen
        _overrideMultiplyAndScreenColors.Initialize(partCount, drawableCount, offscreenCount);

        // BlendMode
        InitializeBlendMode();

        // PartsHierarchy
        SetupPartsHierarchy();
    }

    /// <summary>
    /// 判定是否使用混合模式 (官方 CubismModel::InitializeBlendMode)
    /// </summary>
    protected unsafe void InitializeBlendMode()
    {
        if (Model == IntPtr.Zero)
        {
            return;
        }

        var drawableCount = CubismCore.GetDrawableCount(Model);

        // 如果存在 Offscreen, 或者 Drawable 的混合模式使用 ColorBlend, AlphaBlend, 则启用混合模式.
        if (CubismCore.GetOffscreenCount(Model) > 0)
        {
            _isBlendModeEnabled = true;
        }
        else
        {
            var blendMode = new CsmBlendMode();
            var blendModes = CubismCore.GetDrawableBlendModes(Model);
            for (int i = 0; i < drawableCount; ++i)
            {
                blendMode.SetBlendMode(blendModes[i]);
                var colorBlendType = blendMode.GetColorBlendType();
                var alphaBlendType = blendMode.GetAlphaBlendType();

                // 如果不是 NormalOver, AddCompatible, MultiplyCompatible, 则启用混合模式.
                if (
                    !(
                        colorBlendType == CsmEnum.CsmColorBlendType_Normal
                        && alphaBlendType == CsmEnum.CsmAlphaBlendType_Over
                    )
                    && colorBlendType != CsmEnum.CsmColorBlendType_AddCompatible
                    && colorBlendType != CsmEnum.CsmColorBlendType_MultiplyCompatible
                )
                {
                    _isBlendModeEnabled = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 构建部件层级 (官方 CubismModel::SetupPartsHierarchy)
    /// </summary>
    protected unsafe void SetupPartsHierarchy()
    {
        if (Model == IntPtr.Zero)
        {
            return;
        }

        _partsHierarchy.Clear();

        // 创建所有部件的部件信息管理结构体
        var partCount = CubismCore.GetPartCount(Model);
        for (int i = 0; i < partCount; ++i)
        {
            _partsHierarchy.Add(new CubismModelPartInfo());
        }

        // 逐个 Part 获取父部件, 并添加到父部件的子 object 列表
        for (int i = 0; i < partCount; ++i)
        {
            var parentPartIndex = GetPartParentPartIndex(i);

            if (parentPartIndex == CubismNoIndex_Parent)
            {
                continue;
            }

            for (int partIndex = 0; partIndex < _partsHierarchy.Count; ++partIndex)
            {
                if (partIndex == parentPartIndex)
                {
                    _partsHierarchy[partIndex]
                        .Objects.Add(
                            new CubismModelObjectInfo(i, CubismModelObjectType.ObjectType_Parts)
                        );
                    break;
                }
            }
        }

        // 逐个 Drawable 获取父部件, 并添加到父部件的子 object 列表
        var drawableCount = CubismCore.GetDrawableCount(Model);
        for (int i = 0; i < drawableCount; ++i)
        {
            var parentPartIndex = GetDrawableParentPartIndex(i);

            if (parentPartIndex == CubismNoIndex_Parent)
            {
                continue;
            }

            for (int partIndex = 0; partIndex < _partsHierarchy.Count; ++partIndex)
            {
                if (partIndex == parentPartIndex)
                {
                    _partsHierarchy[partIndex]
                        .Objects.Add(
                            new CubismModelObjectInfo(i, CubismModelObjectType.ObjectType_Drawable)
                        );
                    break;
                }
            }
        }

        // 至此模型的部件结构完成

        // 创建部件子绘制对象信息结构体
        for (int i = 0; i < _partsHierarchy.Count; ++i)
        {
            GetPartChildDrawObjects(i);
        }
    }

    /// <summary>
    /// 递归构建部件的子绘制对象信息 (官方 CubismModel::GetPartChildDrawObjects).
    /// 持有 Offscreen 的部件, 其 Offscreen 索引也包含在子信息中.
    /// </summary>
    /// <param name="partInfoIndex">部件信息的索引</param>
    public unsafe void GetPartChildDrawObjects(int partInfoIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return;
        }

        if (_partsHierarchy[partInfoIndex].GetChildObjectCount() < 1)
        {
            return;
        }

        var childDrawObjects = _partsHierarchy[partInfoIndex].ChildDrawObjects;

        // 如果 childDrawObjects 已经处理过则跳过
        if (
            childDrawObjects.DrawableIndices.Count != 0
            || childDrawObjects.OffscreenIndices.Count != 0
        )
        {
            return;
        }

        var objects = _partsHierarchy[partInfoIndex].Objects;

        for (int i = 0; i < objects.Count; ++i)
        {
            if (objects[i].ObjectType == CubismModelObjectType.ObjectType_Parts)
            {
                // 如果是子部件, 则递归获取子 objects
                GetPartChildDrawObjects(objects[i].ObjectIndex);

                // 获取子部件的子 Drawable, Offscreen
                var childToChildDrawObjects = _partsHierarchy[
                    objects[i].ObjectIndex
                ].ChildDrawObjects;

                for (int j = 0; j < childToChildDrawObjects.DrawableIndices.Count; ++j)
                {
                    // 将孙级 Drawable 添加到部件的子 Drawable
                    childDrawObjects.DrawableIndices.Add(
                        childToChildDrawObjects.DrawableIndices[j]
                    );
                }
                for (int j = 0; j < childToChildDrawObjects.OffscreenIndices.Count; ++j)
                {
                    // 将孙级 Offscreen 添加到部件的子 Offscreen
                    childDrawObjects.OffscreenIndices.Add(
                        childToChildDrawObjects.OffscreenIndices[j]
                    );
                }

                // 确认 Offscreen
                var offscreenIndex = CubismCore.GetPartOffscreenIndices(Model)[
                    objects[i].ObjectIndex
                ];
                if (offscreenIndex != CubismNoIndex_Offscreen)
                {
                    // 如果 Offscreen 存在, 则添加到部件的子 Offscreen
                    childDrawObjects.OffscreenIndices.Add(offscreenIndex);
                }
            }
            else if (objects[i].ObjectType == CubismModelObjectType.ObjectType_Drawable)
            {
                // 如果是 Drawable, 则添加到部件的子 Drawable
                childDrawObjects.DrawableIndices.Add(objects[i].ObjectIndex);
            }
        }
    }

    /// <summary>
    /// 获取部件层级 (官方 CubismModel::GetPartsHierarchy)
    /// </summary>
    public IReadOnlyList<CubismModelPartInfo> GetPartsHierarchy()
    {
        return _partsHierarchy;
    }

    /// <summary>
    /// 更新模型的参数.
    /// </summary>
    public void Update()
    {
        if (Model == IntPtr.Zero)
        {
            return;
        }

        // Update model.
        CubismCore.UpdateModel(Model);
        // Reset dynamic drawable flags.
        CubismCore.ResetDrawableDynamicFlags(Model);
    }

    /// <summary>
    /// 以 Pixel 为单位获取画布宽度
    /// </summary>
    /// <returns>画布宽度(pixel)</returns>
    public float GetCanvasWidthPixel()
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        CubismCore.ReadCanvasInfo(Model, out var tmpSizeInPixels, out _, out _);

        return tmpSizeInPixels.X;
    }

    /// <summary>
    /// 以 Pixel 为单位获取画布高度
    /// </summary>
    /// <returns>画布高度(pixel)</returns>
    public float GetCanvasHeightPixel()
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        CubismCore.ReadCanvasInfo(Model, out var tmpSizeInPixels, out _, out _);

        return tmpSizeInPixels.Y;
    }

    /// <summary>
    /// 获取 PixelsPerUnit.
    /// </summary>
    /// <returns>PixelsPerUnit</returns>
    public float GetPixelsPerUnit()
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        CubismCore.ReadCanvasInfo(Model, out _, out _, out var tmpPixelsPerUnit);

        return tmpPixelsPerUnit;
    }

    /// <summary>
    /// 以 Unit 为单位获取画布宽度
    /// </summary>
    /// <returns>画布宽度(Unit)</returns>
    public float GetCanvasWidth()
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        CubismCore.ReadCanvasInfo(Model, out var tmpSizeInPixels, out _, out var tmpPixelsPerUnit);

        return tmpSizeInPixels.X / tmpPixelsPerUnit;
    }

    /// <summary>
    /// 以 Unit 为单位获取画布高度
    /// </summary>
    /// <returns>画布高度(Unit)</returns>
    public float GetCanvasHeight()
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        CubismCore.ReadCanvasInfo(Model, out var tmpSizeInPixels, out _, out var tmpPixelsPerUnit);

        return tmpSizeInPixels.Y / tmpPixelsPerUnit;
    }

    /// <summary>
    /// 获取部件的索引.
    /// </summary>
    /// <param name="partId">部件的 ID</param>
    /// <returns>部件的索引</returns>
    public int GetPartIndex(string partId)
    {
        if (Model == IntPtr.Zero)
        {
            return -1;
        }

        int partIndex = PartIds.IndexOf(partId);
        if (partIndex != -1)
        {
            return partIndex;
        }

        int partCount = CubismCore.GetPartCount(Model);

        // 如果模型中不存在, 则检索不存在的部件 ID 列表, 并返回其索引
        if (_notExistPartId.TryGetValue(partId, out var item))
        {
            return item;
        }

        // 如果不在不存在的部件 ID 列表中, 则新增元素
        partIndex = partCount + _notExistPartId.Count;

        _notExistPartId.TryAdd(partId, partIndex);
        _notExistPartOpacities.Add(partIndex, 0);

        return partIndex;
    }

    /// <summary>
    /// 获取部件的 ID.
    /// </summary>
    /// <param name="partIndex">部件的 Index</param>
    /// <returns>部件的 ID</returns>
    public string GetPartId(int partIndex)
    {
        // 索引越界检测(旧实现条件写反, 合法索引反而抛异常).
        if (0 > partIndex || partIndex >= PartIds.Count)
        {
            throw new IndexOutOfRangeException("Out of PartIds size");
        }
        return PartIds[partIndex];
    }

    /// <summary>
    /// 获取部件的个数.
    /// </summary>
    /// <returns>部件的个数</returns>
    public int GetPartCount()
    {
        if (Model == IntPtr.Zero)
        {
            return 0;
        }

        return CubismCore.GetPartCount(Model);
    }

    /// <summary>
    /// 获取部件的父部件索引列表.
    /// </summary>
    /// <returns>部件的父部件索引列表</returns>
    public unsafe int* GetPartParentPartIndices()
    {
        if (Model == IntPtr.Zero)
        {
            return null;
        }

        return CubismCore.GetPartParentPartIndices(Model);
    }

    /// <summary>
    /// 获取部件的 Offscreen 索引列表.
    /// </summary>
    /// <returns>部件的 Offscreen 索引列表</returns>
    public unsafe int* GetPartOffscreenIndices()
    {
        if (Model == IntPtr.Zero)
        {
            return null;
        }

        return CubismCore.GetPartOffscreenIndices(Model);
    }

    /// <summary>
    /// 获取部件的父部件索引.
    /// </summary>
    /// <param name="partIndex">部件的索引</param>
    /// <returns>父部件的索引. 不存在时为 CubismNoIndex_Parent</returns>
    public unsafe int GetPartParentPartIndex(int partIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return CubismNoIndex_Parent;
        }

        return CubismCore.GetPartParentPartIndices(Model)[partIndex];
    }

    /// <summary>
    /// 设置部件的透明度.
    /// </summary>
    /// <param name="partId">部件的 ID</param>
    /// <param name="opacity">不透明度</param>
    public void SetPartOpacity(string partId, float opacity)
    {
        // 为了高速化提供了获取 PartIndex 的机制, 但外部设置时调用频率低, 因此不需要
        int index = GetPartIndex(partId);

        if (index < 0)
        {
            return; // 没有该部件, 因此跳过
        }

        SetPartOpacity(index, opacity);
    }

    /// <summary>
    /// 设置部件的透明度.
    /// </summary>
    /// <param name="partIndex">部件的索引</param>
    /// <param name="opacity">部件的透明度</param>
    public unsafe void SetPartOpacity(int partIndex, float opacity)
    {
        if (Model == IntPtr.Zero)
        {
            return;
        }

        if (_notExistPartOpacities.ContainsKey(partIndex))
        {
            _notExistPartOpacities[partIndex] = opacity;
            return;
        }

        // 索引范围检测
        if (0 > partIndex || partIndex >= GetPartCount())
        {
            throw new ArgumentException($"partIndex out of range");
        }

        _partOpacities[partIndex] = opacity;
    }

    /// <summary>
    /// 获取部件的透明度.
    /// </summary>
    /// <param name="partId">部件的 ID</param>
    /// <returns>部件的透明度</returns>
    public float GetPartOpacity(string partId)
    {
        // 为了高速化提供了获取 PartIndex 的机制, 但外部设置时调用频率低, 因此不需要
        int index = GetPartIndex(partId);

        if (index < 0)
        {
            return 0; // 没有该部件, 因此跳过
        }

        return GetPartOpacity(index);
    }

    /// <summary>
    /// 获取部件的透明度.
    /// </summary>
    /// <param name="partIndex">部件的索引</param>
    /// <returns>部件的透明度</returns>
    public unsafe float GetPartOpacity(int partIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        if (_notExistPartOpacities.TryGetValue(partIndex, out float value))
        {
            // 如果是模型中不存在的部件 ID, 则从不存在部件列表返回透明度
            return value;
        }

        // 索引范围检测
        if (0 > partIndex || partIndex >= GetPartCount())
        {
            throw new ArgumentException($"partIndex out of range");
        }

        return _partOpacities[partIndex];
    }

    /// <summary>
    /// 获取参数的索引.
    /// </summary>
    /// <param name="parameterId">参数的 ID</param>
    /// <returns>参数的索引</returns>
    public int GetParameterIndex(string parameterId)
    {
        if (Model == IntPtr.Zero)
        {
            return -1;
        }

        int parameterIndex = ParameterIds.IndexOf(parameterId);
        if (parameterIndex != -1)
        {
            return parameterIndex;
        }

        // 如果模型中不存在, 则在不存在的参数 ID 列表内检索, 并返回其索引
        if (_notExistParameterId.TryGetValue(parameterId, out var data))
        {
            return data;
        }

        // 如果不在不存在的参数 ID 列表中, 则新增元素
        parameterIndex = CubismCore.GetParameterCount(Model) + _notExistParameterId.Count;

        _notExistParameterId.TryAdd(parameterId, parameterIndex);
        _notExistParameterValues.Add(parameterIndex, 0);

        return parameterIndex;
    }

    /// <summary>
    /// 获取参数的个数.
    /// </summary>
    /// <returns>参数的个数</returns>
    public int GetParameterCount()
    {
        if (Model == IntPtr.Zero)
        {
            return 0;
        }

        return CubismCore.GetParameterCount(Model);
    }

    /// <summary>
    /// 获取参数的种类.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <returns>csmParameterType_Normal -> 通常参数
    /// csmParameterType_BlendShape -> 混合形状参数</returns>
    public unsafe int GetParameterType(int parameterIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 0;
        }

        return CubismCore.GetParameterTypes(Model)[parameterIndex];
    }

    /// <summary>
    /// 获取参数的最大值.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <returns>参数的最大值</returns>
    public unsafe float GetParameterMaximumValue(int parameterIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        return CubismCore.GetParameterMaximumValues(Model)[parameterIndex];
    }

    /// <summary>
    /// 获取参数的最小值.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <returns>参数的最小值</returns>
    public unsafe float GetParameterMinimumValue(int parameterIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        return CubismCore.GetParameterMinimumValues(Model)[parameterIndex];
    }

    /// <summary>
    /// 获取参数的默认值.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <returns> 参数的默认值</returns>
    public unsafe float GetParameterDefaultValue(int parameterIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        return CubismCore.GetParameterDefaultValues(Model)[parameterIndex];
    }

    /// <summary>
    /// 获取参数的值.
    /// </summary>
    /// <param name="parameterId">参数的 ID</param>
    /// <returns>参数的值</returns>
    public float GetParameterValue(string parameterId)
    {
        // 为了高速化提供了获取 ParameterIndex 的机制, 但外部设置时调用频率低, 因此不需要
        int parameterIndex = GetParameterIndex(parameterId);
        return GetParameterValue(parameterIndex);
    }

    /// <summary>
    /// 获取参数的值.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <returns>参数的值</returns>
    public unsafe float GetParameterValue(int parameterIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        if (_notExistParameterValues.TryGetValue(parameterIndex, out var item))
        {
            return item;
        }

        // 索引范围检测
        if (0 > parameterIndex || parameterIndex >= GetParameterCount())
        {
            throw new ArgumentException($"parameterIndex out of range");
        }

        return _parameterValues[parameterIndex];
    }

    /// <summary>
    /// 设置参数的值.
    /// </summary>
    /// <param name="parameterId">参数的 ID</param>
    /// <param name="value">参数的值</param>
    /// <param name="weight">权重</param>
    public void SetParameterValue(string parameterId, float value, float weight = 1.0f)
    {
        int index = GetParameterIndex(parameterId);
        SetParameterValue(index, value, weight);
    }

    /// <summary>
    /// 设置参数的值.
    /// 从 5-r.5 起, 存在重复设置时不限制在范围内, 而是循环 (IsRepeat / GetParameterRepeatValue).
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <param name="value">参数的值</param>
    /// <param name="weight">权重</param>
    public unsafe void SetParameterValue(int parameterIndex, float value, float weight = 1.0f)
    {
        if (Model == IntPtr.Zero)
        {
            return;
        }

        if (_notExistParameterValues.TryGetValue(parameterIndex, out float value1))
        {
            _notExistParameterValues[parameterIndex] =
                weight == 1 ? value : (value1 * (1 - weight)) + (value * weight);
            return;
        }

        // 索引范围检测
        if (0 > parameterIndex || parameterIndex >= GetParameterCount())
        {
            throw new ArgumentException($"parameterIndex out of range");
        }

        if (IsRepeat(parameterIndex))
        {
            value = GetParameterRepeatValue(parameterIndex, value);
        }
        else
        {
            value = GetParameterClampValue(parameterIndex, value);
        }

        _parameterValues[parameterIndex] =
            (weight == 1)
                ? value
                : (_parameterValues[parameterIndex] * (1 - weight)) + (value * weight);
    }

    /// <summary>
    /// 获取参数是否具有重复设置.
    /// 如果模型整体的覆盖标志, 或者参数单独的覆盖标志为真, 则
    /// 使用 SDK 侧的设置, 否则使用 Editor(Core) 的设置.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <returns>若重复则为 true</returns>
    public unsafe bool IsRepeat(int parameterIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        if (_notExistParameterValues.ContainsKey(parameterIndex))
        {
            return false;
        }

        // 索引范围检测
        if (0 > parameterIndex || parameterIndex >= GetParameterCount())
        {
            throw new ArgumentException($"parameterIndex out of range");
        }

        bool isRepeat;

        // 判定是否执行参数重复处理
        if (
            _isOverriddenParameterRepeat
            || _userParameterRepeatDataList[parameterIndex].IsOverridden
        )
        {
            // 使用 SDK 侧设置的重复信息
            isRepeat = _userParameterRepeatDataList[parameterIndex].IsParameterRepeated;
        }
        else
        {
            // 使用 Editor 中设置的重复信息
            isRepeat = CubismCore.GetParameterRepeats(Model)[parameterIndex] != 0;
        }

        return isRepeat;
    }

    /// <summary>
    /// 获取把参数的值循环到范围内的结果.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <param name="value">参数的值</param>
    /// <returns>循环后的值. 参数不存在时原样返回</returns>
    public unsafe float GetParameterRepeatValue(int parameterIndex, float value)
    {
        if (Model == IntPtr.Zero)
        {
            return value;
        }

        if (_notExistParameterValues.ContainsKey(parameterIndex))
        {
            return value;
        }

        // 索引范围检测
        if (0 > parameterIndex || parameterIndex >= GetParameterCount())
        {
            throw new ArgumentException($"parameterIndex out of range");
        }

        var maxValue = CubismCore.GetParameterMaximumValues(Model)[parameterIndex];
        var minValue = CubismCore.GetParameterMinimumValues(Model)[parameterIndex];
        var valueSize = maxValue - minValue;

        if (maxValue < value)
        {
            var overValue = CubismMath.ModF(value - maxValue, valueSize);
            value = !float.IsNaN(overValue) ? minValue + overValue : maxValue;
        }
        if (value < minValue)
        {
            var overValue = CubismMath.ModF(minValue - value, valueSize);
            value = !float.IsNaN(overValue) ? maxValue - overValue : minValue;
        }

        return value;
    }

    /// <summary>
    /// 获取把参数的值钳制到范围内的结果.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <param name="value">参数的值</param>
    /// <returns>钳制后的值. 参数不存在时原样返回</returns>
    public unsafe float GetParameterClampValue(int parameterIndex, float value)
    {
        if (Model == IntPtr.Zero)
        {
            return value;
        }

        if (_notExistParameterValues.ContainsKey(parameterIndex))
        {
            return value;
        }

        // 索引范围检测
        if (0 > parameterIndex || parameterIndex >= GetParameterCount())
        {
            throw new ArgumentException($"parameterIndex out of range");
        }

        var maxValue = CubismCore.GetParameterMaximumValues(Model)[parameterIndex];
        var minValue = CubismCore.GetParameterMinimumValues(Model)[parameterIndex];

        return CubismMath.ClampF(value, minValue, maxValue);
    }

    /// <summary>
    /// 获取参数的重复设置 (Core 的原始数据).
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <returns>若重复则为 true</returns>
    public unsafe bool GetParameterRepeats(int parameterIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        return CubismCore.GetParameterRepeats(Model)[parameterIndex] != 0;
    }

    /// <summary>
    /// 对参数的值做加法.
    /// </summary>
    /// <param name="parameterId">参数的 ID</param>
    /// <param name="value">要加上的值</param>
    /// <param name="weight">权重</param>
    public void AddParameterValue(string parameterId, float value, float weight = 1.0f)
    {
        int index = GetParameterIndex(parameterId);
        AddParameterValue(index, value, weight);
    }

    /// <summary>
    /// 对参数的值做加法.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <param name="value">要加上的值</param>
    /// <param name="weight">权重</param>
    public void AddParameterValue(int parameterIndex, float value, float weight = 1.0f)
    {
        if (Model == IntPtr.Zero)
        {
            return;
        }

        if (parameterIndex == -1)
            return;
        SetParameterValue(parameterIndex, (GetParameterValue(parameterIndex) + (value * weight)));
    }

    /// <summary>
    /// 对参数的值做乘法.
    /// </summary>
    /// <param name="parameterId">参数的 ID</param>
    /// <param name="value">要乘上的值</param>
    /// <param name="weight">权重</param>
    public void MultiplyParameterValue(string parameterId, float value, float weight = 1.0f)
    {
        int index = GetParameterIndex(parameterId);
        MultiplyParameterValue(index, value, weight);
    }

    /// <summary>
    /// 对参数的值做乘法.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <param name="value">要乘上的值</param>
    /// <param name="weight">权重</param>
    public void MultiplyParameterValue(int parameterIndex, float value, float weight = 1.0f)
    {
        if (Model == IntPtr.Zero)
        {
            return;
        }

        if (parameterIndex == -1)
            return;
        SetParameterValue(
            parameterIndex,
            GetParameterValue(parameterIndex) * (1.0f + (value - 1.0f) * weight)
        );
    }

    /// <summary>
    /// 获取 Drawable 的索引.
    /// </summary>
    /// <param name="drawableId">Drawable 的 ID</param>
    /// <returns>Drawable 的索引</returns>
    public int GetDrawableIndex(string drawableId)
    {
        return DrawableIds.IndexOf(drawableId);
    }

    /// <summary>
    /// 获取 Drawable 的个数.
    /// </summary>
    /// <returns>Drawable 的个数</returns>
    public int GetDrawableCount()
    {
        if (Model == IntPtr.Zero)
        {
            return 0;
        }

        return CubismCore.GetDrawableCount(Model);
    }

    /// <summary>
    /// 获取 Drawable 的 ID.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的 ID</returns>
    public string GetDrawableId(int drawableIndex)
    {
        // 索引越界检测(旧实现条件写反, 合法索引反而抛异常).
        if (0 > drawableIndex || drawableIndex >= DrawableIds.Count)
        {
            throw new IndexOutOfRangeException("Out of DrawableIds size");
        }
        return DrawableIds[drawableIndex];
    }

    /// <summary>
    /// 获取绘制顺序列表 (官方 5-r.5 中由 csmGetDrawableRenderOrders 改名为 csmGetRenderOrders).
    /// </summary>
    /// <returns>绘制顺序列表</returns>
    /// <remarks>
    /// 实测确认: 返回的数组长度为 drawableCount + offscreenCount(Ren 模型为 198 + 24 = 222).
    /// 如果只考虑 drawable 并用 drawableCount 循环, 会破坏 offscreen 的绘制顺序,
    /// 因此调用侧必须用 GetDrawableCount() + GetOffscreenCount() 进行遍历.
    /// </remarks>
    public unsafe int* GetRenderOrders()
    {
        if (Model == IntPtr.Zero)
        {
            return null;
        }

        return CubismCore.GetRenderOrders(Model);
    }

    /// <summary>
    /// 获取 Drawable 的纹理索引.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的纹理索引</returns>
    public unsafe int GetDrawableTextureIndex(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 0;
        }

        var textureIndices = CubismCore.GetDrawableTextureIndices(Model);
        return textureIndices[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的顶点索引个数.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的顶点索引个数</returns>
    public unsafe int GetDrawableVertexIndexCount(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 0;
        }

        return CubismCore.GetDrawableIndexCounts(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的顶点个数.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的顶点个数</returns>
    public unsafe int GetDrawableVertexCount(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 0;
        }

        return CubismCore.GetDrawableVertexCounts(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的顶点列表.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的顶点列表</returns>
    public unsafe float* GetDrawableVertices(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return null;
        }

        return (float*)GetDrawableVertexPositions(drawableIndex);
    }

    /// <summary>
    /// 获取 Drawable 的顶点索引列表.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的顶点索引列表</returns>
    public unsafe ushort* GetDrawableVertexIndices(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return null;
        }

        return CubismCore.GetDrawableIndices(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的顶点列表.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的顶点列表</returns>
    public unsafe Vector2* GetDrawableVertexPositions(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return null;
        }

        return CubismCore.GetDrawableVertexPositions(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的顶点 UV 列表.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的顶点 UV 列表</returns>
    public unsafe Vector2* GetDrawableVertexUvs(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return null;
        }

        return CubismCore.GetDrawableVertexUvs(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的透明度.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的透明度</returns>
    public unsafe float GetDrawableOpacity(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        return CubismCore.GetDrawableOpacities(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的乘算色.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的乘算色</returns>
    public unsafe Vector4 GetDrawableMultiplyColor(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return default;
        }

        return CubismCore.GetDrawableMultiplyColors(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的屏幕色.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的屏幕色</returns>
    public unsafe Vector4 GetDrawableScreenColor(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return default;
        }

        return CubismCore.GetDrawableScreenColors(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的父部件索引.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>drawable 的父部件索引</returns>
    public unsafe int GetDrawableParentPartIndex(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return CubismNoIndex_Parent;
        }

        return CubismCore.GetDrawableParentPartIndices(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的混合模式 (官方 5-r.5 中由 GetDrawableBlendMode 改名为 GetDrawableBlendModeType).
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的混合模式</returns>
    public unsafe CsmBlendMode GetDrawableBlendModeType(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return default;
        }

        var blendMode = new CsmBlendMode();

        // 5.2 以前的模型也可以使用
        var blendModes = CubismCore.GetDrawableBlendModes(Model);
        blendMode.SetBlendMode(blendModes[drawableIndex]);

        return blendMode;
    }

    /// <summary>
    /// 获取 Drawable 使用遮罩时的反转设置.
    /// 不使用遮罩时会被忽略
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的遮罩反转设置</returns>
    public unsafe bool GetDrawableInvertedMask(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        var constantFlags = CubismCore.GetDrawableConstantFlags(Model)[drawableIndex];
        return IsBitSet(constantFlags, CsmEnum.CsmIsInvertedMask);
    }

    /// <summary>
    /// 获取 Drawable 的显示信息.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>true    Drawable 显示
    /// false   Drawable 不显示</returns>
    public unsafe bool GetDrawableDynamicFlagIsVisible(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmIsVisible);
    }

    /// <summary>
    /// 获取 Drawable 的显示状态是否在最近的 CubismModel::Update 函数中发生变化.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>true    Drawable 的显示状态在最近的 CubismModel::Update 函数中发生了变化
    /// false   Drawable 的显示状态在最近的 CubismModel::Update 函数中没有变化</returns>
    public unsafe bool GetDrawableDynamicFlagVisibilityDidChange(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmVisibilityDidChange);
    }

    /// <summary>
    /// 获取 Drawable 的透明度是否在最近的 CubismModel::Update 函数中发生变化.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>true    Drawable 的透明度在最近的 CubismModel::Update 函数中发生了变化
    /// false   Drawable 的透明度在最近的 CubismModel::Update 函数中没有变化</returns>
    public unsafe bool GetDrawableDynamicFlagOpacityDidChange(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmOpacityDidChange);
    }

    /// <summary>
    /// 获取 Drawable 的 DrawOrder 是否在最近的 CubismModel::Update 函数中发生变化.
    /// DrawOrder 是在 ArtMesh 上指定的 0 到 1000 的信息
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>true    Drawable 的透明度在最近的 CubismModel::Update 函数中发生了变化
    /// false   Drawable 的透明度在最近的 CubismModel::Update 函数中没有变化</returns>
    public unsafe bool GetDrawableDynamicFlagDrawOrderDidChange(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmDrawOrderDidChange);
    }

    /// <summary>
    /// 获取 Drawable 的绘制顺序是否在最近的 CubismModel::Update 函数中发生变化.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>true    Drawable 的绘制顺序在最近的 CubismModel::Update 函数中发生了变化
    /// false   Drawable 的绘制顺序在最近的 CubismModel::Update 函数中没有变化</returns>
    public unsafe bool GetDrawableDynamicFlagRenderOrderDidChange(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmRenderOrderDidChange);
    }

    /// <summary>
    /// 获取 Drawable 的顶点信息是否在最近的 CubismModel::Update 函数中发生变化.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>true    Drawable 的顶点信息在最近的 CubismModel::Update 函数中发生了变化
    /// false   Drawable 的顶点信息在最近的 CubismModel::Update 函数中没有变化</returns>
    public unsafe bool GetDrawableDynamicFlagVertexPositionsDidChange(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmVertexPositionsDidChange);
    }

    /// <summary>
    /// 获取 Drawable 的乘算色与屏幕色是否在最近的 CubismModel::Update 函数中发生变化.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>true    Drawable 的乘算色与屏幕色在最近的 CubismModel::Update 函数中发生了变化
    /// false   Drawable 的乘算色与屏幕色在最近的 CubismModel::Update 函数中没有变化</returns>
    public unsafe bool GetDrawableDynamicFlagBlendColorDidChange(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmBlendColorDidChange);
    }

    /// <summary>
    /// 获取 Drawable 的裁剪遮罩列表.
    /// </summary>
    /// <returns>Drawable 的裁剪遮罩列表</returns>
    public unsafe int** GetDrawableMasks()
    {
        if (Model == IntPtr.Zero)
        {
            return null;
        }

        return CubismCore.GetDrawableMasks(Model);
    }

    /// <summary>
    /// 获取 Drawable 的裁剪遮罩个数列表.
    /// </summary>
    /// <returns>Drawable 的裁剪遮罩个数列表</returns>
    public unsafe int* GetDrawableMaskCounts()
    {
        if (Model == IntPtr.Zero)
        {
            return null;
        }

        return CubismCore.GetDrawableMaskCounts(Model);
    }

    //========================================================
    //  Offscreen Functions.
    //========================================================

    /// <summary>
    /// 获取 Offscreen 的混合模式 (官方 5-r.5 的新增 API).
    /// </summary>
    /// <param name="offscreenIndex">Offscreen 的索引</param>
    /// <returns>Offscreen 的混合模式</returns>
    public unsafe CsmBlendMode GetOffscreenBlendModeType(int offscreenIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return default;
        }

        var blendMode = new CsmBlendMode();

        var blendModes = CubismCore.GetOffscreenBlendModes(Model);
        if (blendModes != null)
        {
            blendMode.SetBlendMode(blendModes[offscreenIndex]);
        }

        return blendMode;
    }

    /// <summary>
    /// 获取 Offscreen 的个数.
    /// </summary>
    /// <returns>Offscreen 的个数</returns>
    public int GetOffscreenCount()
    {
        if (Model == IntPtr.Zero)
        {
            return 0;
        }

        return CubismCore.GetOffscreenCount(Model);
    }

    /// <summary>
    /// 获取 Offscreen 的裁剪遮罩列表.
    /// </summary>
    /// <returns>Offscreen 的裁剪遮罩列表</returns>
    public unsafe int** GetOffscreenMasks()
    {
        if (Model == IntPtr.Zero)
        {
            return null;
        }

        return CubismCore.GetOffscreenMasks(Model);
    }

    /// <summary>
    /// 获取 Offscreen 的裁剪遮罩个数列表.
    /// </summary>
    /// <returns>Offscreen 的裁剪遮罩个数列表</returns>
    public unsafe int* GetOffscreenMaskCounts()
    {
        if (Model == IntPtr.Zero)
        {
            return null;
        }

        return CubismCore.GetOffscreenMaskCounts(Model);
    }

    /// <summary>
    /// 获取 Offscreen 的持有者 (部件) 索引列表.
    /// </summary>
    /// <returns>Offscreen 的持有者索引列表</returns>
    public unsafe int* GetOffscreenOwnerIndices()
    {
        if (Model == IntPtr.Zero)
        {
            return null;
        }

        return CubismCore.GetOffscreenOwnerIndices(Model);
    }

    /// <summary>
    /// 获取 Offscreen 的持有者 (部件) ID.
    /// 注意持有者索引是部件的索引 (不是 Drawable).
    /// </summary>
    /// <param name="offscreenIndex">Offscreen 的索引</param>
    /// <returns>持有者 (部件) 的 ID</returns>
    public unsafe string GetOffscreenOwnerId(int offscreenIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return string.Empty;
        }

        var ownerIndex = GetOffscreenOwnerIndices()[offscreenIndex];
        var partIds = CubismCore.GetPartIds(Model);

        return Context.IdManager.GetId(new string(partIds[ownerIndex]));
    }

    /// <summary>
    /// 获取 Offscreen 的乘算色.
    /// </summary>
    /// <param name="offscreenIndex">Offscreen 的索引</param>
    /// <returns>Offscreen 的乘算色</returns>
    public unsafe Vector4 GetOffscreenMultiplyColor(int offscreenIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return default;
        }

        return CubismCore.GetOffscreenMultiplyColors(Model)[offscreenIndex];
    }

    /// <summary>
    /// 获取 Offscreen 的屏幕色.
    /// </summary>
    /// <param name="offscreenIndex">Offscreen 的索引</param>
    /// <returns>Offscreen 的屏幕色</returns>
    public unsafe Vector4 GetOffscreenScreenColor(int offscreenIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return default;
        }

        return CubismCore.GetOffscreenScreenColors(Model)[offscreenIndex];
    }

    /// <summary>
    /// 获取 Offscreen 使用遮罩时的反转设置.
    /// </summary>
    /// <param name="offscreenIndex">Offscreen 的索引</param>
    /// <returns>Offscreen 的遮罩反转设置</returns>
    public unsafe bool GetOffscreenInvertedMask(int offscreenIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        var constantFlags = CubismCore.GetOffscreenConstantFlags(Model)[offscreenIndex];
        return IsBitSet(constantFlags, CsmEnum.CsmIsInvertedMask);
    }

    /// <summary>
    /// 获取 Offscreen 的透明度.
    /// </summary>
    /// <param name="offscreenIndex">Offscreen 的索引</param>
    /// <returns>Offscreen 的透明度</returns>
    public unsafe float GetOffscreenOpacity(int offscreenIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 1.0f;
        }

        if (offscreenIndex < 0 || offscreenIndex >= CubismCore.GetOffscreenCount(Model))
        {
            return 1.0f; // 没有 Offscreen, 因此跳过
        }

        return CubismCore.GetOffscreenOpacities(Model)[offscreenIndex];
    }

    /// <summary>
    /// 是否正在使用裁剪遮罩?
    /// </summary>
    /// <returns>true    正在使用裁剪遮罩
    /// false   没有使用裁剪遮罩</returns>
    public unsafe bool IsUsingMasking()
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        for (int d = 0; d < CubismCore.GetDrawableCount(Model); ++d)
        {
            if (CubismCore.GetDrawableMaskCounts(Model)[d] <= 0)
            {
                continue;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Offscreen 是否正在使用裁剪遮罩?
    /// </summary>
    /// <returns>true    Offscreen 正在使用裁剪遮罩
    /// false   Offscreen 没有使用裁剪遮罩</returns>
    public unsafe bool IsUsingMaskingForOffscreen()
    {
        if (Model == IntPtr.Zero)
        {
            return false;
        }

        for (int d = 0; d < CubismCore.GetOffscreenCount(Model); ++d)
        {
            if (CubismCore.GetOffscreenMaskCounts(Model)[d] <= 0)
            {
                continue;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// 读入保存的参数
    /// </summary>
    public unsafe void LoadParameters()
    {
        if (Model == IntPtr.Zero)
        {
            return;
        }

        int parameterCount = CubismCore.GetParameterCount(Model);
        int savedParameterCount = _savedParameters.Count;

        if (parameterCount > savedParameterCount)
        {
            parameterCount = savedParameterCount;
        }

        for (int i = 0; i < parameterCount; ++i)
        {
            _parameterValues[i] = _savedParameters[i];
        }
    }

    /// <summary>
    /// 保存参数.
    /// </summary>
    public unsafe void SaveParameters()
    {
        if (Model == IntPtr.Zero)
        {
            return;
        }

        int parameterCount = CubismCore.GetParameterCount(Model);
        int savedParameterCount = _savedParameters.Count;

        if (savedParameterCount != parameterCount)
        {
            _savedParameters.Clear();
            for (int i = 0; i < parameterCount; ++i)
            {
                _savedParameters.Add(_parameterValues[i]);
            }
        }
        else
        {
            for (int i = 0; i < parameterCount; ++i)
            {
                _savedParameters[i] = _parameterValues[i];
            }
        }
    }

    /// <summary>
    /// 获取是否在模型整体上覆盖参数的重复设置.
    /// </summary>
    /// <returns>为 true 时, 在模型整体上执行参数重复</returns>
    public bool GetOverrideFlagForModelParameterRepeat()
    {
        return _isOverriddenParameterRepeat;
    }

    /// <summary>
    /// 设置是否在模型整体上覆盖参数的重复设置.
    /// </summary>
    /// <param name="isRepeat">为 true 时, 在模型整体上执行参数重复</param>
    public void SetOverrideFlagForModelParameterRepeat(bool isRepeat)
    {
        _isOverriddenParameterRepeat = isRepeat;
    }

    /// <summary>
    /// 获取单个参数的重复设置覆盖标志.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <returns>若覆盖则为 true</returns>
    public bool GetOverrideFlagForParameterRepeat(int parameterIndex)
    {
        return _userParameterRepeatDataList[parameterIndex].IsOverridden;
    }

    /// <summary>
    /// 设置单个参数的重复设置覆盖标志.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <param name="value">若覆盖则为 true</param>
    public void SetOverrideFlagForParameterRepeat(int parameterIndex, bool value)
    {
        _userParameterRepeatDataList[parameterIndex].IsOverridden = value;
    }

    /// <summary>
    /// 获取单个参数的重复标志.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <returns>若重复则为 true</returns>
    public bool GetRepeatFlagForParameterRepeat(int parameterIndex)
    {
        return _userParameterRepeatDataList[parameterIndex].IsParameterRepeated;
    }

    /// <summary>
    /// 设置单个参数的重复标志.
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <param name="value">若重复则为 true</param>
    public void SetRepeatFlagForParameterRepeat(int parameterIndex, bool value)
    {
        _userParameterRepeatDataList[parameterIndex].IsParameterRepeated = value;
    }

    /// <summary>
    /// 获取乘算色与屏幕色的设置 (官方 GetOverrideMultiplyAndScreenColor).
    /// </summary>
    /// <returns>乘算色与屏幕色的设置</returns>
    public CubismModelMultiplyAndScreenColor GetOverrideMultiplyAndScreenColor()
    {
        return _overrideMultiplyAndScreenColors;
    }

    //========================================================
    //  Culling Functions.
    //========================================================

    /// <summary>
    /// 获取 Drawable 的剔除信息.
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的剔除信息 (0: 不剔除 / 1: 剔除)</returns>
    public unsafe int GetDrawableCulling(int drawableIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 0;
        }

        if (GetOverrideFlagForModelCullings() || GetOverrideFlagForDrawableCullings(drawableIndex))
        {
            return _userDrawableCullings[drawableIndex].IsCulling;
        }

        var constantFlags = CubismCore.GetDrawableConstantFlags(Model);
        return IsBitSet(constantFlags[drawableIndex], CsmEnum.CsmIsDoubleSided) ? 0 : 1;
    }

    /// <summary>
    /// 设置 Drawable 的剔除信息
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <param name="isCulling">剔除时为 1, 不剔除时为 0</param>
    public void SetDrawableCulling(int drawableIndex, int isCulling)
    {
        _userDrawableCullings[drawableIndex].IsCulling = isCulling;
    }

    /// <summary>
    /// 获取 Offscreen 的剔除信息.
    /// </summary>
    /// <param name="offscreenIndex">Offscreen 的索引</param>
    /// <returns>Offscreen 的剔除信息 (0: 不剔除 / 1: 剔除)</returns>
    public unsafe int GetOffscreenCulling(int offscreenIndex)
    {
        if (Model == IntPtr.Zero)
        {
            return 0;
        }

        if (
            GetOverrideFlagForModelCullings() || GetOverrideFlagForOffscreenCullings(offscreenIndex)
        )
        {
            return _userOffscreenCullings[offscreenIndex].IsCulling;
        }

        var constantFlags = CubismCore.GetOffscreenConstantFlags(Model);
        return IsBitSet(constantFlags[offscreenIndex], CsmEnum.CsmIsDoubleSided) ? 0 : 1;
    }

    /// <summary>
    /// 设置 Offscreen 的剔除信息
    /// </summary>
    /// <param name="offscreenIndex">Offscreen 的索引</param>
    /// <param name="isCulling">剔除时为 1, 不剔除时为 0</param>
    public void SetOffscreenCulling(int offscreenIndex, int isCulling)
    {
        _userOffscreenCullings[offscreenIndex].IsCulling = isCulling;
    }

    /// <summary>
    /// 是否从 SDK 覆盖模型整体的剔除设置.
    /// </summary>
    /// <returns>true    ->  使用 SDK 上的剔除设置
    /// false   ->  使用模型的剔除设置</returns>
    public bool GetOverrideFlagForModelCullings()
    {
        return _isOverriddenCullings;
    }

    /// <summary>
    /// 设置是否从 SDK 覆盖模型整体的剔除设置
    /// 使用 SDK 上的剔除设置时为 true, 使用模型的剔除设置时为 false
    /// </summary>
    public void SetOverrideFlagForModelCullings(bool value)
    {
        _isOverriddenCullings = value;
    }

    /// <summary>
    /// 是否从 SDK 覆盖 drawable 的剔除设置.
    /// </summary>
    /// <returns>true    ->  使用 SDK 上的剔除设置
    /// false   ->  使用模型的剔除设置</returns>
    public bool GetOverrideFlagForDrawableCullings(int drawableIndex)
    {
        return _userDrawableCullings[drawableIndex].IsOverridden;
    }

    /// <summary>
    /// 设置是否从 SDK 覆盖 drawable 的剔除设置
    /// 使用 SDK 上的剔除设置时为 true, 使用模型的剔除设置时为 false
    /// </summary>
    public void SetOverrideFlagForDrawableCullings(int drawableIndex, bool value)
    {
        _userDrawableCullings[drawableIndex].IsOverridden = value;
    }

    /// <summary>
    /// 是否从 SDK 覆盖 offscreen 的剔除设置.
    /// </summary>
    /// <returns>true    ->  使用 SDK 上的剔除设置
    /// false   ->  使用模型的剔除设置</returns>
    public bool GetOverrideFlagForOffscreenCullings(int offscreenIndex)
    {
        return _userOffscreenCullings[offscreenIndex].IsOverridden;
    }

    /// <summary>
    /// 设置是否从 SDK 覆盖 offscreen 的剔除设置
    /// 使用 SDK 上的剔除设置时为 true, 使用模型的剔除设置时为 false
    /// </summary>
    public void SetOverrideFlagForOffscreenCullings(int offscreenIndex, bool value)
    {
        _userOffscreenCullings[offscreenIndex].IsOverridden = value;
    }

    /// <summary>
    /// 获取是否需要使用混合模式进行绘制.
    /// </summary>
    /// <returns>为 true 时, 使用混合模式</returns>
    public bool IsBlendModeEnabled()
    {
        return _isBlendModeEnabled;
    }

    /// <summary>
    /// 获取模型的透明度
    /// </summary>
    /// <returns>透明度的值</returns>
    public float GetModelOpacity()
    {
        return _modelOpacity;
    }

    /// <summary>
    /// 设置模型的透明度
    /// </summary>
    /// <param name="value">透明度的值</param>
    public void SetModelOpacity(float value)
    {
        _modelOpacity = value;
    }

    /// <summary>
    /// 获取参数的 ID.
    /// </summary>
    /// <param name="parameterIndex">参数的 Index</param>
    /// <returns>参数的 ID</returns>
    public string GetParameterId(int parameterIndex)
    {
        // 索引越界检测(旧实现条件写反, 合法索引反而抛异常).
        if (0 > parameterIndex || parameterIndex >= ParameterIds.Count)
        {
            throw new IndexOutOfRangeException("Out of ParameterIds size");
        }
        return ParameterIds[parameterIndex];
    }

    private static bool IsBitSet(byte data, byte mask)
    {
        return (data & mask) == mask;
    }
}
