using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Motion;
using Live2DCSharpSDK.Framework.Physics;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 用户实际使用的模型的基类. 用户继承此类进行实现.
/// </summary>
public abstract class CubismUserModel : IDisposable
{
    /// <summary>
    /// 渲染器
    /// </summary>
    public CubismRenderer? Renderer { get; protected set; }
    /// <summary>
    /// 模型矩阵
    /// </summary>
    public CubismModelMatrix ModelMatrix { get; protected set; }
    /// <summary>
    /// Model 实例
    /// </summary>
    public CubismModel Model => _moc.Model;
    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool Initialized { get; set; }
    /// <summary>
    /// 是否已更新
    /// </summary>
    public bool Updating { get; set; }
    /// <summary>
    /// 不透明度
    /// </summary>
    public float Opacity { get; set; }

    /// <summary>
    /// Moc 数据
    /// </summary>
    protected CubismMoc _moc;

    /// <summary>
    /// 是否已释放(Dispose 的幂等保护).
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 更新处理的调度器 (官方 5-r.5 中添加到 CubismUserModel)
    /// </summary>
    protected readonly CubismUpdateScheduler _updateScheduler;

    /// <summary>
    /// 动作管理
    /// </summary>
    protected CubismMotionManager _motionManager;
    /// <summary>
    /// 表情管理
    /// </summary>
    protected CubismExpressionMotionManager _expressionManager;
    /// <summary>
    /// 自动眨眼
    /// </summary>
    protected CubismEyeBlink? _eyeBlink;
    /// <summary>
    /// 呼吸
    /// </summary>
    protected CubismBreath _breath;
    /// <summary>
    /// 姿势管理
    /// </summary>
    protected CubismPose? _pose;
    /// <summary>
    /// 鼠标拖拽
    /// </summary>
    protected CubismTargetPoint _dragManager;
    /// <summary>
    /// 物理演算
    /// </summary>
    protected CubismPhysics? _physics;
    /// <summary>
    /// 用户数据
    /// </summary>
    protected CubismModelUserData? _modelUserData;
    /// <summary>
    /// 最后一次口型同步的控制值
    /// </summary>
    protected float _lastLipSyncValue;
    /// <summary>
    /// X 轴方向的加速度
    /// </summary>
    protected float _accelerationX;
    /// <summary>
    /// Y 轴方向的加速度
    /// </summary>
    protected float _accelerationY;
    /// <summary>
    /// Z 轴方向的加速度
    /// </summary>
    protected float _accelerationZ;
    /// <summary>
    /// 是否进行 MOC3 一致性校验
    /// </summary>
    protected bool _mocConsistency;
    /// <summary>
    /// 是否进行动作一致性校验 (官方 5-r.5 中追加)
    /// </summary>
    protected bool _motionConsistency;

    // 注: 5-r.5 中 _lipSync / _dragX / _dragY 已从基类中删除
    // (口型同步由 CubismLipSyncUpdater 负责, 拖拽由 CubismLookUpdater 负责).

    /// <summary>
    /// 用于向 CubismMotionQueueManager 注册事件的 Callback.
    /// 调用 CubismUserModel 继承类的 EventFired.
    /// </summary>
    /// <param name="eventValue">已触发事件的字符串数据</param>
    /// <param name="customData">预期为继承 CubismUserModel 的实例</param>
    public static void CubismDefaultMotionEventCallback(CubismUserModel? customData, string eventValue)
    {
        customData?.MotionEventFired(eventValue);
    }

    /// <summary>
    /// 构造函数.
    /// </summary>
    public CubismUserModel()
    {
        Opacity = 1.0f;

        // 创建更新处理的调度器
        _updateScheduler = new();

        // 创建动作管理器
        // 由于继承自 MotionQueueManager 类, 用法相同
        _motionManager = new();
        _motionManager.SetEventCallback(CubismDefaultMotionEventCallback, this);

        // 创建表情动作管理器
        _expressionManager = new();

        // 基于拖拽的动画
        _dragManager = new();
    }

    /// <summary>
    /// 释放资源(Dispose 改为 virtual, 确保派生类的释放逻辑一定执行.
    /// 幂等: 重复调用不会二次释放 Moc / Model).
    /// </summary>
    public virtual void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _updateScheduler.Dispose();

        // 如果在 LoadModel 之前被释放, 则 _moc 尚未生成 (.editorconfig 中禁用了 CS8618,
        // 非 null 注解也可能在运行时为 null).
        _moc?.Dispose();

        DeleteRenderer();
    }

    /// <summary>
    /// 把更新处理注册到调度器 (官方 5-r.5 的 _updateScheduler.AddUpdatableList 的封装).
    /// </summary>
    /// <param name="updater">要注册的更新处理</param>
    protected void RegisterUpdater(ICubismUpdater updater)
    {
        _updateScheduler.AddUpdatableList(updater);
    }

    /// <summary>
    /// 设置鼠标拖拽的信息.
    /// </summary>
    /// <param name="x">正在拖拽的光标的 X 位置</param>
    /// <param name="y">正在拖拽的光标的 Y 位置</param>
    public void SetDragging(float x, float y)
    {
        _dragManager.Set(x, y);
    }

    /// <summary>
    /// 设置加速度的信息.
    /// </summary>
    /// <param name="x">X 轴方向的加速度</param>
    /// <param name="y">Y 轴方向的加速度</param>
    /// <param name="z">Z 轴方向的加速度</param>
    protected void SetAcceleration(float x, float y, float z)
    {
        _accelerationX = x;
        _accelerationY = y;
        _accelerationZ = z;
    }

    /// <summary>
    /// 读入模型数据.
    /// </summary>
    /// <param name="buffer">已读入 moc3 文件的缓冲区</param>
    /// <param name="shouldCheckMocConsistency">MOC 一致性检查标志(初始值 : false)</param>
    protected void LoadModel(byte[] buffer, bool shouldCheckMocConsistency = false)
    {
        _moc = new CubismMoc(buffer, shouldCheckMocConsistency);
        Model.SaveParameters();
        ModelMatrix = new CubismModelMatrix(Model.GetCanvasWidth(), Model.GetCanvasHeight());
    }

    /// <summary>
    /// 读入动作数据 (官方 5-r.5 的 CubismUserModel::LoadMotion).
    /// 本地与 moc3 一样采用接收文件路径的形式.
    /// 不使用 model3.json 的淡入淡出值覆盖的版本 (fade 参数相当于 null).
    /// </summary>
    /// <param name="buffer">motion3.json 的文件路径</param>
    /// <param name="name">动作名 (用于日志)</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数. 为 NULL 时不被调用.</param>
    /// <param name="onBeganMotionHandler">动作播放开始时调用的回调函数. 为 NULL 时不被调用.</param>
    /// <param name="shouldCheckMotionConsistency">是否校验 motion3.json 的一致性</param>
    /// <returns>生成的动作数据. 无法读入时为 null</returns>
    protected ACubismMotion? LoadMotion(string buffer, string name,
        FinishedMotionCallback? onFinishedMotionHandler = null,
        BeganMotionCallback? onBeganMotionHandler = null,
        bool shouldCheckMotionConsistency = false)
    {
        return LoadMotionCore(buffer, name, null, null,
            onFinishedMotionHandler, onBeganMotionHandler, shouldCheckMotionConsistency);
    }

    /// <summary>
    /// 从模型设置中读入动作数据 (官方 5-r.5 的 CubismUserModel::LoadMotion 的 modelSetting 版本).
    /// 相当于官方 ICubismModelSetting* 的东西, 在本地是 ModelSettingObj (model3.json 的 DTO).
    /// model3.json 的 FadeInTime / FadeOutTime 为 0 以上时, 覆盖 motion3.json 侧的淡入淡出值
    /// (与官方 GetMotionFadeInTimeValue / GetMotionFadeOutTimeValue 相同的判定).
    /// </summary>
    /// <param name="modelSetting">模型设置 (官方 ICubismModelSetting* 的本地等价物)</param>
    /// <param name="group">动作组名</param>
    /// <param name="index">组内的索引</param>
    /// <param name="shouldCheckMotionConsistency">是否校验 motion3.json 的一致性</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数. 为 NULL 时不被调用.</param>
    /// <param name="onBeganMotionHandler">动作播放开始时调用的回调函数. 为 NULL 时不被调用.</param>
    /// <param name="modelHomeDir">model3.json 所在的目录 (用于解析 motion 的相对路径). 为 NULL 时以当前目录为基准</param>
    /// <returns>生成的动作数据. 没有组/索引/文件时为 null</returns>
    protected ACubismMotion? LoadMotion(ModelSettingObj? modelSetting, string group, int index,
        bool shouldCheckMotionConsistency = false,
        FinishedMotionCallback? onFinishedMotionHandler = null,
        BeganMotionCallback? onBeganMotionHandler = null,
        string? modelHomeDir = null)
    {
        if (modelSetting == null)
        {
            CubismLog.Warning("Failed to LoadMotion(). Model setting is NULL.");
            return null;
        }

        var fileName = modelSetting.GetMotionFileName(group, index);
        if (string.IsNullOrEmpty(fileName))
        {
            CubismLog.Warning($"Failed to LoadMotion(). Motion is not found. group={group}, index={index}");
            return null;
        }

        var path = string.IsNullOrEmpty(modelHomeDir)
            ? Path.GetFullPath(fileName)
            : Path.GetFullPath(modelHomeDir + fileName);

        // 负值表示 "不覆盖 motion3.json 侧的值" (与官方相同)
        var fadeInTime = modelSetting.GetMotionFadeInTimeValue(group, index);
        var fadeOutTime = modelSetting.GetMotionFadeOutTimeValue(group, index);

        return LoadMotionCore(path, $"{group}_{index}",
            fadeInTime >= 0.0f ? fadeInTime : null,
            fadeOutTime >= 0.0f ? fadeOutTime : null,
            onFinishedMotionHandler, onBeganMotionHandler, shouldCheckMotionConsistency);
    }

    /// <summary>
    /// 读入动作数据的公共处理.
    /// </summary>
    /// <param name="buffer">motion3.json 的文件路径</param>
    /// <param name="name">动作名 (用于日志)</param>
    /// <param name="fadeInTime">要覆盖的淡入时间 (null 则不覆盖)</param>
    /// <param name="fadeOutTime">要覆盖的淡出时间 (null 则不覆盖)</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数</param>
    /// <param name="onBeganMotionHandler">动作播放开始时调用的回调函数</param>
    /// <param name="shouldCheckMotionConsistency">是否校验 motion3.json 的一致性</param>
    /// <returns>生成的动作数据. 无法读入时为 null</returns>
    private ACubismMotion? LoadMotionCore(string buffer, string name,
        float? fadeInTime, float? fadeOutTime,
        FinishedMotionCallback? onFinishedMotionHandler,
        BeganMotionCallback? onBeganMotionHandler,
        bool shouldCheckMotionConsistency)
    {
        if (string.IsNullOrEmpty(buffer))
        {
            CubismLog.Warning("Failed to LoadMotion(). Buffer is NULL.");
            return null;
        }

        CubismLog.Debug($"[Live2D SDK]load motion: {name}");

        ACubismMotion motion;
        try
        {
            motion = new CubismMotion(buffer, onFinishedMotionHandler, onBeganMotionHandler, shouldCheckMotionConsistency);
        }
        catch (CubismMotionConsistencyException e)
        {
            // 官方 CubismMotion::Create 在一致性不一致时返回 NULL, 调用侧 (LAppModel) 会跳过该动作.
            // 本地构造函数会抛异常, 因此在此捕获并转换为 NULL 等价的 null(其他异常不吞掉).
            // 注意: 参数非法, 文件不存在, JSON 损坏等其他异常不予吞掉, 直接向上传播.
            CubismLog.Warning("[Live2D SDK]failed to load motion (inconsistent): {0} : {1}", name, e.Message);
            return null;
        }

        // 官方 LoadMotion: 如果 model3.json 的 FadeInTime / FadeOutTime 为 0 以上则覆盖
        if (fadeInTime.HasValue)
        {
            motion.FadeInSeconds = fadeInTime.Value;
        }

        if (fadeOutTime.HasValue)
        {
            motion.FadeOutSeconds = fadeOutTime.Value;
        }

        return motion;
    }

    /// <summary>
    /// 读入姿势数据.
    /// </summary>
    /// <param name="buffer">已读入 pose3.json 的缓冲区</param>
    protected void LoadPose(string buffer)
    {
        _pose = new CubismPose(buffer);
    }

    /// <summary>
    /// 读入物理演算数据.
    /// </summary>
    /// <param name="buffer">已读入 physics3.json 的缓冲区</param>
    protected void LoadPhysics(string buffer)
    {
        _physics = new CubismPhysics(buffer);
    }

    /// <summary>
    /// 读入用户数据.
    /// </summary>
    /// <param name="buffer">已读入 userdata3.json 的缓冲区</param>
    protected void LoadUserData(string buffer)
    {
        _modelUserData = new CubismModelUserData(buffer);
    }

    /// <summary>
    /// 从未还原的 Moc 缓冲区获取 .moc3 的版本 (官方 CubismUserModel::GetMocVersionFromBuffer).
    /// </summary>
    /// <param name="buffer">已读入 moc3 文件的缓冲区</param>
    /// <returns>.moc3 Version</returns>
    public static uint GetMocVersionFromBuffer(byte[] buffer)
    {
        return CubismMoc.GetMocVersionFromBuffer(buffer);
    }

    /// <summary>
    /// 获取指定位置上是否命中了 Drawable.
    /// </summary>
    /// <param name="drawableId">要校验的 Drawable 的 ID</param>
    /// <param name="pointX">X位置</param>
    /// <param name="pointY">Y位置</param>
    /// <returns>true    已命中
    /// false   未命中</returns>
    public unsafe bool IsHit(string drawableId, float pointX, float pointY)
    {
        var drawIndex = Model.GetDrawableIndex(drawableId);

        if (drawIndex < 0)
        {
            return false; // 存在しない場合はfalse
        }

        var count = Model.GetDrawableVertexCount(drawIndex);
        var vertices = Model.GetDrawableVertices(drawIndex);

        // 顶点为空(count == 0)或 null 时不做命中判定.
        // 旧实现没有校验 count / vertices 就直接读取 vertices[0] / vertices[1],
        // 因此在空网格或已释放的模型 (CubismModel.Dispose 后为 count=0, vertices=null) 上会发生 AV.
        if (count <= 0 || vertices == null)
        {
            return false;
        }

        var left = vertices[0];
        var right = vertices[0];
        var top = vertices[1];
        var bottom = vertices[1];

        for (int j = 1; j < count; ++j)
        {
            var x = vertices[CubismFramework.VertexOffset + j * CubismFramework.VertexStep];
            var y = vertices[CubismFramework.VertexOffset + j * CubismFramework.VertexStep + 1];

            if (x < left)
            {
                left = x; // Min x
            }

            if (x > right)
            {
                right = x; // Max x
            }

            if (y < top)
            {
                top = y; // Min y
            }

            if (y > bottom)
            {
                bottom = y; // Max y
            }
        }

        var tx = ModelMatrix.InvertTransformX(pointX);
        var ty = ModelMatrix.InvertTransformY(pointY);

        return (left <= tx) && (tx <= right) && (top <= ty) && (ty <= bottom);
    }

    /// <summary>
    /// 生成渲染器并执行初始化.
    /// 本地保持由宿主创建渲染器并注入模型的方式.
    /// </summary>
    protected void CreateRenderer(CubismRenderer renderer)
    {
        Renderer = renderer;
    }

    /// <summary>
    /// 设置 Offscreen (渲染目标) 的尺寸 (官方 5-r.5 的新增 API).
    /// 由宿主按实际尺寸每帧调用.
    /// </summary>
    /// <param name="width">绘制模型的缓冲区宽度</param>
    /// <param name="height">绘制模型的缓冲区高度</param>
    public void SetRenderTargetSize(uint width, uint height)
    {
        Renderer?.SetRenderTargetSize(width, height);
    }

    /// <summary>
    /// 释放渲染器.
    /// </summary>
    protected void DeleteRenderer()
    {
        if (Renderer != null)
        {
            Renderer.Dispose();
            Renderer = null;
        }
    }

    /// <summary>
    /// 处理播放过程中发生 Event 时的逻辑.
    /// 预期通过继承进行重写.
    /// 不重写时输出日志.
    /// </summary>
    /// <param name="eventValue">已触发事件的字符串数据</param>
    protected abstract void MotionEventFired(string eventValue);
}
