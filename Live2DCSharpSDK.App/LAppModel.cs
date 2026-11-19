using System.Globalization;
using System.Text.Json;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Motion;
using Live2DCSharpSDK.Framework.Physics;

namespace Live2DCSharpSDK.App;

public class LAppModel : CubismUserModel
{
    /// <summary>
    /// 模型设置信息
    /// </summary>
    private readonly ModelSettingObj _modelSetting;

    /// <summary>
    /// 存放模型设置的目录
    /// </summary>
    private readonly string _modelHomeDir;

    /// <summary>
    /// 模型中配置的眨眼功能参数 ID
    /// </summary>
    private readonly List<string> _eyeBlinkIds = [];

    /// <summary>
    /// 模型中配置的口型同步功能参数 ID
    /// </summary>
    private readonly List<string> _lipSyncIds = [];

    /// <summary>
    /// 已加载的动作列表
    /// </summary>
    private readonly Dictionary<string, ACubismMotion> _motions = [];

    /// <summary>
    /// 已加载的表情列表
    /// </summary>
    private readonly Dictionary<string, ACubismMotion> _expressions = [];

    public List<string> Motions => new(_motions.Keys);
    public List<string> Expressions => new(_expressions.Keys);
    public List<(string, int, float)> Parts
    {
        get
        {
            var list = new List<(string, int, float)>();
            var count = Model.GetPartCount();
            for (int a = 0; a < count; a++)
            {
                list.Add((Model.GetPartId(a), a, Model.GetPartOpacity(a)));
            }
            return list;
        }
    }

    public List<TextureInfo> Textures = [];

    public List<string> Parameters => new(Model.ParameterIds);

    /// <summary>
    /// 增量时间的累计值[秒]
    /// </summary>
    public float UserTimeSeconds { get; set; }

    public bool RandomMotion { get; set; } = true;
    public bool CustomValueUpdate { get; set; }

    public Action<LAppModel>? ValueUpdate;

    /// <summary>
    /// 模型是否持有物理数据(本地扩展, 供宿主切换 UI 使用)
    /// </summary>
    public bool HasPhysics => _physicsUpdater != null;

    /// <summary>
    /// 是否执行物理演算(本地扩展, 官方示例无此开关, 用于宿主内切换物理的验证).
    /// 不持有物理数据的模型始终为 false.
    /// </summary>
    public bool PhysicsEnabled
    {
        get => _physicsUpdater?.Enabled ?? false;
        set
        {
            if (_physicsUpdater != null)
            {
                _physicsUpdater.Enabled = value;
            }
        }
    }

    /// <summary>
    /// 参数 ID: ParamAngleX
    /// </summary>
    public string IdParamAngleX { get; set; }

    /// <summary>
    /// 参数 ID: ParamAngleY
    /// </summary>
    public string IdParamAngleY { get; set; }

    /// <summary>
    /// 参数 ID: ParamAngleZ
    /// </summary>
    public string IdParamAngleZ { get; set; }

    /// <summary>
    /// 参数 ID: ParamBodyAngleX
    /// </summary>
    public string IdParamBodyAngleX { get; set; }

    /// <summary>
    /// 参数 ID: ParamEyeBallX
    /// </summary>
    public string IdParamEyeBallX { get; set; }

    /// <summary>
    /// 参数 ID: ParamEyeBallXY
    /// </summary>
    public string IdParamEyeBallY { get; set; }

    /// <summary>
    /// 参数 ID: ParamBreath
    /// </summary>
    public string IdParamBreath { get; set; }

    /// <summary>
    /// 拖拽产生的朝向参数信息(官方 5-r.5 的 CubismLook)
    /// </summary>
    private readonly CubismLook _look;

    /// <summary>
    /// 是否由动作更新参数(官方 5-r.5 的 _motionUpdated.
    /// CubismEyeBlinkUpdater 每帧通过 Func&lt;bool&gt; 读取该值)
    /// </summary>
    private bool _motionUpdated;

    /// <summary>
    /// 物理演算的更新处理(为切换物理 ON/OFF 在 App 侧派生, 官方直接注册 CubismPhysicsUpdater)
    /// </summary>
    private readonly LAppPhysicsUpdater? _physicsUpdater;

    /// <summary>
    /// wav 文件处理器
    /// </summary>
    /// <remarks>
    /// 本轮不引入 wav 解码, 因此暂不实现; 不注册 CubismLipSyncUpdater 时口型始终无效.
    /// </remarks>
    //LAppWavFileHandler _wavFileHandler;

    private readonly LAppDelegate _lapp;

    private readonly Random _random = new();

    public event Action<LAppModel, string>? Motion;

    public LAppModel(LAppDelegate lapp, CubismContext context, string dir, string fileName)
        : base(context)
    {
        _lapp = lapp;

        if (LAppDefine.MocConsistencyValidationEnable)
        {
            _mocConsistency = true;
        }

        // motion3.json 的一致性校验(官方 LAppModel.cpp:45-48)
        if (LAppDefine.MotionConsistencyValidationEnable)
        {
            _motionConsistency = true;
        }

        IdParamBreath = Context.IdManager.GetId(CubismDefaultParameterId.ParamBreath);
        IdParamAngleX = Context.IdManager.GetId(CubismDefaultParameterId.ParamAngleX);
        IdParamAngleY = Context.IdManager.GetId(CubismDefaultParameterId.ParamAngleY);
        IdParamAngleZ = Context.IdManager.GetId(CubismDefaultParameterId.ParamAngleZ);
        IdParamBodyAngleX = Context.IdManager.GetId(CubismDefaultParameterId.ParamBodyAngleX);
        IdParamEyeBallX = Context.IdManager.GetId(CubismDefaultParameterId.ParamEyeBallX);
        IdParamEyeBallY = Context.IdManager.GetId(CubismDefaultParameterId.ParamEyeBallY);

        _modelHomeDir = dir;

        CubismLog.Debug($"[Live2D App]load model setting: {fileName}");

        using var stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        _modelSetting = JsonSerializer.Deserialize(
            stream,
            ModelSettingObjContext.Default.ModelSettingObj
        ) ?? throw new Exception("model3.json error");

        Updating = true;
        Initialized = false;

        //Cubism Model
        var path = _modelSetting.FileReferences?.Moc;
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = Path.GetFullPath(Path.Combine(_modelHomeDir, path));
            if (!File.Exists(path))
            {
                throw new Exception("model is null");
            }

            CubismLog.Debug($"[Live2D App]create model: {path}");

            LoadModel(File.ReadAllBytes(path), _mocConsistency);
        }

        //Expression
        if (_modelSetting.FileReferences?.Expressions?.Count > 0)
        {
            for (int i = 0; i < _modelSetting.FileReferences.Expressions.Count; i++)
            {
                var item = _modelSetting.FileReferences.Expressions[i];
                string name = item.Name;
                path = item.File;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                path = Path.GetFullPath(Path.Combine(_modelHomeDir, path));
                if (!File.Exists(path))
                {
                    continue;
                }

                var motion = new CubismExpressionMotion(path, Context);

                if (_expressions.ContainsKey(name))
                {
                    _expressions[name] = motion;
                }
                else
                {
                    _expressions.Add(name, motion);
                }
            }
        }

        //Physics
        path = _modelSetting.FileReferences?.Physics;
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = Path.GetFullPath(Path.Combine(_modelHomeDir, path));
            if (File.Exists(path))
            {
                LoadPhysics(path);
            }
        }

        //Pose
        path = _modelSetting.FileReferences?.Pose;
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = Path.GetFullPath(Path.Combine(_modelHomeDir, path));
            if (File.Exists(path))
            {
                LoadPose(path);
            }
        }

        //EyeBlink
        if (_modelSetting.IsExistEyeBlinkParameters())
        {
            _eyeBlink = new CubismEyeBlink(_modelSetting, Context);
        }

        LoadBreath();

        //UserData
        path = _modelSetting.FileReferences?.UserData;
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = Path.GetFullPath(Path.Combine(_modelHomeDir, path));
            if (File.Exists(path))
            {
                LoadUserData(path);
            }
        }

        // EyeBlinkIds
        if (_eyeBlink != null)
        {
            _eyeBlinkIds.AddRange(_eyeBlink.ParameterIds);
        }

        // LipSyncIds
        if (_modelSetting.IsExistLipSyncParameters())
        {
            foreach (var item in _modelSetting.Groups)
            {
                if (item.Name == CubismModelSettingJson.LipSync)
                {
                    _lipSyncIds.AddRange(item.Ids);
                }
            }
        }

        //Layout
        Dictionary<string, float> layout = [];
        _modelSetting.GetLayoutMap(layout);
        ModelMatrix.SetupFromLayout(layout);

        Model.SaveParameters();

        if (_modelSetting.FileReferences?.Motions?.Count > 0)
        {
            foreach (var item in _modelSetting.FileReferences.Motions)
            {
                PreloadMotionGroup(item.Key);
            }
        }

        //-----------------------------------------------------------------
        // 注册更新处理(官方 LAppModel::SetupModel, LAppModel.cpp:158-272)
        // 执行顺序不由注册顺序决定, 而由 ICubismUpdater 的 executionOrder 决定:
        //   200 EyeBlink → 300 Expression → 400 Look → 500 Breath → 600 Physics → 700 LipSync → 800 Pose
        //-----------------------------------------------------------------

        // 表情(官方 LAppModel.cpp:158-159)
        RegisterUpdater(new CubismExpressionUpdater(_expressionManager));

        // 物理演算(官方 LAppModel.cpp:172-173)
        if (_physics != null)
        {
            _physicsUpdater = new LAppPhysicsUpdater(_physics);
            RegisterUpdater(_physicsUpdater);
        }

        // 姿态(官方 LAppModel.cpp:188-189)
        if (_pose != null)
        {
            RegisterUpdater(new CubismPoseUpdater(_pose));
        }

        // 眨眼(官方 LAppModel.cpp:198-201. 由动作更新过的帧不眨眼)
        if (_eyeBlink != null)
        {
            RegisterUpdater(new CubismEyeBlinkUpdater(() => _motionUpdated, _eyeBlink));
        }

        // 呼吸(官方 LAppModel.cpp:219-220)
        RegisterUpdater(new CubismBreathUpdater(_breath));

        // 不注册口型同步: 本轮不引入 wav 解码, 口型值恒为 0.
        // 官方 LAppModel.cpp:249-250 传入 IParameterProvider 并注册到 700 号.

        // 拖拽导致的朝向(对应官方 LAppModel.cpp:255-269, 系数与手写实现一致).
        _look = CubismLook.Create();
        _look.SetParameters([
            new() { ParameterId = IdParamAngleX, FactorX = 30.0f },
            new() { ParameterId = IdParamAngleY, FactorY = 30.0f },
            new() { ParameterId = IdParamAngleZ, FactorXY = -30.0f },
            new() { ParameterId = IdParamBodyAngleX, FactorX = 10.0f },
            new() { ParameterId = IdParamEyeBallX, FactorX = 1.0f },
            new() { ParameterId = IdParamEyeBallY, FactorY = 1.0f },
        ]);
        RegisterUpdater(new CubismLookUpdater(_look, _dragManager));

        // 注册后排序(官方 LAppModel.cpp:272. 即使忘记, OnLateUpdate 也会自动排序)
        _updateScheduler.SortUpdatableList();

        _motionManager.StopAllMotions();

        Updating = false;
        Initialized = true;
        if (Renderer != null)
        {
            DeleteRenderer();
        }
        Renderer = lapp.CreateRenderer(Model);

        SetupTextures();
    }

    /// <summary>
    /// 释放资源(基类 Dispose 已改为 virtual, 因此此处改为 override).
    /// 清理顺序: 先释放 App 侧的资源(动作与纹理), 最后释放基类(scheduler / Moc / Renderer).
    /// </summary>
    public override void Dispose()
    {
        // --- 先释放 App 侧的资源 ---
        _motions.Clear();
        _expressions.Clear();

        // model3.json 中没有 Motions 的模型(例如大肥鱼)也不抛出异常
        if (_modelSetting.FileReferences?.Motions?.Count > 0)
        {
            foreach (var item in _modelSetting.FileReferences.Motions)
            {
                ReleaseMotionGroup(item.Key);
            }
        }

        foreach (var item in Textures)
        {
            _lapp.TextureManager.ReleaseTexture(item);
        }
        Textures.Clear();

        // --- 最后释放基类(_updateScheduler / _moc / Renderer) ---
        base.Dispose();
    }

    public void LoadBreath()
    {
        //Breath
        _breath = new()
        {
            Parameters =
            [
                new()
                {
                    ParameterId = IdParamAngleX,
                    Offset = 0.0f,
                    Peak = 15.0f,
                    Cycle = 6.5345f,
                    Weight = 0.5f,
                },
                new()
                {
                    ParameterId = IdParamAngleY,
                    Offset = 0.0f,
                    Peak = 8.0f,
                    Cycle = 3.5345f,
                    Weight = 0.5f,
                },
                new()
                {
                    ParameterId = IdParamAngleZ,
                    Offset = 0.0f,
                    Peak = 10.0f,
                    Cycle = 5.5345f,
                    Weight = 0.5f,
                },
                new()
                {
                    ParameterId = IdParamBodyAngleX,
                    Offset = 0.0f,
                    Peak = 4.0f,
                    Cycle = 15.5345f,
                    Weight = 0.5f,
                },
                new()
                {
                    ParameterId = IdParamBreath,
                    Offset = 0.5f,
                    Peak = 0.5f,
                    Cycle = 3.2345f,
                    Weight = 0.5f,
                },
            ],
        };
    }

    /// <summary>
    /// 重建渲染器
    /// </summary>
    public void ReloadRenderer()
    {
        DeleteRenderer();

        CreateRenderer(_lapp.CreateRenderer(Model));

        SetupTextures();
    }

    /// <summary>
    /// 使用所属 delegate 当前帧的增量时间更新模型(兼容旧调用点).
    /// 新代码请调用 <see cref="Update(float)"/> 并显式传入增量时间.
    /// </summary>
    public void Update()
    {
        Update(_lapp.DeltaTime);
    }

    /// <summary>
    /// 模型的更新处理. 由模型参数决定绘制状态.
    /// </summary>
    /// <param name="deltaTimeSeconds">增量时间(秒)</param>
    /// <remarks>
    /// 与官方 5-r.5 的 LAppModel::Update()(LAppModel.cpp:379-408)顺序相同:
    /// 时间更新 → 动作更新 → 不透明度获取 → CubismUpdateScheduler(200→800) → Model.Update().
    /// 此前的实现手写眨眼/表情/拖拽/呼吸/物理/口型同步/姿态,
    /// 5-r.5 中由 SetupModel 注册的 6 个 Updater 依次从 scheduler 调用.
    /// </remarks>
    public void Update(float deltaTimeSeconds)
    {
        UserTimeSeconds += deltaTimeSeconds;

        // 是否由动作更新参数(CubismEyeBlinkUpdater 会读取该值)
        _motionUpdated = false;

        //-----------------------------------------------------------------
        Model.LoadParameters(); // 载入上次保存的状态
        if (_motionManager.IsFinished() && RandomMotion)
        {
            // 没有动作在播放时, 从待机动作中随机播放一个
            StartRandomMotion(LAppDefine.MotionGroupIdle, MotionPriority.PriorityIdle);
        }
        else
        {
            _motionUpdated = _motionManager.UpdateMotion(Model, deltaTimeSeconds); // 更新动作
        }
        Model.SaveParameters(); // 保存状态

        //-----------------------------------------------------------------

        // 不透明度
        Opacity = Model.GetModelOpacity();

        // 眨眼(200) → 表情(300) → 拖拽(400) → 呼吸(500) → 物理(600) → 姿态(800)
        _updateScheduler.OnLateUpdate(Model, deltaTimeSeconds);

        // 宿主独有的参数更新(本地扩展, 官方没有).
        // 在 scheduler(包含拖拽/Look)之后, Model.Update() 之前调用.
        if (CustomValueUpdate)
        {
            ValueUpdate?.Invoke(this);
        }

        Model.Update();
    }

    /// <summary>
    /// 绘制模型的处理. 传入绘制模型所在空间的 View-Projection 矩阵.
    /// </summary>
    /// <param name="matrix">View-Projection 矩阵</param>
    public void Draw(CubismMatrix44 matrix)
    {
        if (Model == null)
        {
            return;
        }

        matrix.MultiplyByMatrix(ModelMatrix);
        if (Renderer != null)
        {
            Renderer.SetMvpMatrix(matrix);
        }

        DoDraw();
    }

    /// <summary>
    /// 开始播放参数指定的动作.
    /// </summary>
    /// <param name="name">动作名, 格式为 "组名_编号", 组名本身允许包含下划线</param>
    /// <param name="priority">优先级</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数. 为 NULL 时不调用.</param>
    /// <param name="onBeganMotionHandler">动作播放开始时调用的回调函数. 为 NULL 时不调用.</param>
    /// <returns>动作的队列条目. 无法开始时返回 null.</returns>
    public CubismMotionQueueEntry? StartMotion(
        string name,
        MotionPriority priority,
        FinishedMotionCallback? onFinishedMotionHandler = null,
        BeganMotionCallback? onBeganMotionHandler = null
    )
    {
        // 以最后一个下划线拆分: 官方动作名的格式是 "组名_编号", 组名本身允许包含下划线(如 "Tap_Body_0").
        if (string.IsNullOrEmpty(name))
        {
            CubismLog.Warning("[Live2D App]motion name is empty.");
            return null;
        }

        int separator = name.LastIndexOf('_');
        if (separator <= 0 || separator == name.Length - 1)
        {
            CubismLog.Warning($"[Live2D App]motion name error: [{name}]. Expected \"group_no\".");
            return null;
        }

        string noText = name[(separator + 1)..];
        if (
            int.TryParse(noText, NumberStyles.None, CultureInfo.InvariantCulture, out int no)
            is false
        )
        {
            CubismLog.Warning($"[Live2D App]motion name error: [{name}]. Invalid motion index.");
            return null;
        }

        return StartMotion(
            name[..separator],
            no,
            priority,
            onFinishedMotionHandler,
            onBeganMotionHandler
        );
    }

    /// <summary>
    /// 开始播放指定动作组内的动作.
    /// 组或编号不存在时与官方一样按 "无法开始" 处理, 返回 null 而不抛异常.
    /// </summary>
    /// <param name="group">动作组名</param>
    /// <param name="no">组内编号</param>
    /// <param name="priority">优先级</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数. 为 NULL 时不调用.</param>
    /// <param name="onBeganMotionHandler">动作播放开始时调用的回调函数. 为 NULL 时不调用.</param>
    /// <returns>动作的队列条目. 无法开始时返回 null.</returns>
    public CubismMotionQueueEntry? StartMotion(
        string group,
        int no,
        MotionPriority priority,
        FinishedMotionCallback? onFinishedMotionHandler = null,
        BeganMotionCallback? onBeganMotionHandler = null
    )
    {
        if (priority == MotionPriority.PriorityForce)
        {
            _motionManager.ReservePriority = priority;
        }
        else if (!_motionManager.ReserveMotion(priority))
        {
            CubismLog.Debug("[Live2D App]can't start motion.");
            return null;
        }

        // 组或编号不存在时按 "无法开始" 处理, 与官方 GetMotionFileName 返回空串后走同一分支等价.
        var motions = _modelSetting.FileReferences?.Motions;
        if (
            motions is null
            || motions.TryGetValue(group, out var items) is false
            || no < 0
            || no >= items.Count
        )
        {
            CubismLog.Warning(
                $"[Live2D App]Can't start motion [{group}_{no}]. Motion is not found."
            );

            // 恢复未能开始的动作的预约优先级(官方 LAppModel.cpp:448)
            _motionManager.ReservePriority = MotionPriority.PriorityNone;
            return null;
        }

        var item = items[no];

        //ex) idle_0
        string name = $"{group}_{no}";

        CubismMotion motion;
        if (!_motions.TryGetValue(name, out var value))
        {
            // 与官方 LAppModel::StartMotion(LAppModel.cpp:436-439)相同,
            // 经 LoadMotion(modelSetting, group, no, ...) 应用 model3.json 中的 FadeInTime / FadeOutTime.
            var loaded = LoadMotion(
                _modelSetting,
                group,
                no,
                _motionConsistency,
                onFinishedMotionHandler,
                onBeganMotionHandler,
                _modelHomeDir
            );
            if (loaded is not CubismMotion cubismMotion)
            {
                // 官方只做 CubismLogError(仅断言). 本地的 CubismLog.Error 会抛出异常,
                // 为不影响宿主只记录 Warning 并返回 null(Framework 侧不做改动).
                CubismLog.Warning($"[Live2D App]Can't start motion [{group}_{no}].");

                // 恢复未能加载的动作的预约优先级(官方 LAppModel.cpp:448)
                _motionManager.ReservePriority = MotionPriority.PriorityNone;
                return null;
            }

            motion = cubismMotion;
            motion.SetEffectIds(_eyeBlinkIds, _lipSyncIds);
        }
        else
        {
            if (value is not CubismMotion cachedMotion)
            {
                CubismLog.Warning(
                    $"[Live2D App]Can't start motion [{group}_{no}]. Cached motion type mismatch."
                );
                _motionManager.ReservePriority = MotionPriority.PriorityNone;
                return null;
            }

            motion = cachedMotion;
            motion.OnFinishedMotion = onFinishedMotionHandler;
            motion.OnBeganMotion = onBeganMotionHandler;
        }

        //voice
        string voice = item.Sound;
        if (!string.IsNullOrWhiteSpace(voice))
        {
            //string path = voice;
            //path = _modelHomeDir + path;
            // wav 解码不在本轮范围内, 因此不调用 _wavFileHandler.Start(path).
        }

        CubismLog.Debug($"[Live2D App]start motion: [{group}_{no}]");
        return _motionManager.StartMotionPriority(motion, priority);
    }

    /// <summary>
    /// 开始播放随机选中的动作.
    /// </summary>
    /// <param name="group">动作组名</param>
    /// <param name="priority">优先级</param>
    /// <param name="onFinishedMotionHandler">动作播放结束时调用的回调函数. 为 NULL 时不调用.</param>
    /// <param name="onBeganMotionHandler">动作播放开始时调用的回调函数. 为 NULL 时不调用.</param>
    /// <returns>动作的队列条目. 无法开始时返回 null.</returns>
    public object? StartRandomMotion(
        string group,
        MotionPriority priority,
        FinishedMotionCallback? onFinishedMotionHandler = null,
        BeganMotionCallback? onBeganMotionHandler = null
    )
    {
        var motions = _modelSetting.FileReferences?.Motions;
        if (
            motions is null
            || motions.TryGetValue(group, out var items) is false
            || items.Count == 0
        )
        {
            // 与官方 StartRandomMotion 相同: 组不存在或为空时直接返回无效句柄, 不抛异常.
            return null;
        }

        int no = _random.Next() % items.Count;
        return StartMotion(group, no, priority, onFinishedMotionHandler, onBeganMotionHandler);
    }

    /// <summary>
    /// 动作组名一览(供宿主切换 UI 使用的 App 扩展, 官方示例中没有).
    /// </summary>
    public List<string> MotionGroups => _modelSetting.FileReferences?.Motions?.Keys.ToList() ?? [];

    /// <summary>
    /// 获取指定动作组中的动作数量(供宿主切换 UI 使用的 App 扩展, 官方示例中没有).
    /// </summary>
    /// <param name="group">动作组名</param>
    /// <returns>动作数量. 不存在该组时为 0</returns>
    public int GetMotionCount(string group)
    {
        if (_modelSetting.FileReferences?.Motions?.TryGetValue(group, out var list) == true)
        {
            return list.Count;
        }

        return 0;
    }

    /// <summary>
    /// 设置参数指定的表情动作
    /// </summary>
    /// <param name="expressionID">表情动作的 ID</param>
    public void SetExpression(string expressionID)
    {
        ACubismMotion motion = _expressions[expressionID];
        CubismLog.Debug($"[Live2D App]expression: [{expressionID}]");

        if (motion != null)
        {
            // 官方 5-r.5 已删除 CubismExpressionMotionManager.StartMotionPriority(改用父类的 StartMotion).
            _expressionManager.StartMotion(motion);
        }
        else
        {
            CubismLog.Debug($"[Live2D App]expression[{expressionID}] is null ");
        }
    }

    /// <summary>
    /// 设置随机选中的表情动作
    /// </summary>
    public void SetRandomExpression()
    {
        if (_expressions.Count == 0)
        {
            return;
        }

        int no = _random.Next() % _expressions.Count;
        int i = 0;
        foreach (var item in _expressions)
        {
            if (i == no)
            {
                SetExpression(item.Key);
                return;
            }
            i++;
        }
    }

    /// <summary>
    /// 接收事件触发
    /// </summary>
    /// <param name="eventValue"></param>
    protected override void MotionEventFired(string eventValue)
    {
        CubismLog.Debug($"[Live2D App]{eventValue} is fired on LAppModel!!");
        Motion?.Invoke(this, eventValue);
    }

    /// <summary>
    /// 命中判定测试.
    /// 由指定 ID 的顶点列表计算矩形, 判定坐标是否在矩形范围内.
    /// </summary>
    /// <param name="hitAreaName">要测试命中判定的对象 ID</param>
    /// <param name="x">进行判定的 X 坐标</param>
    /// <param name="y">进行判定的 Y 坐标</param>
    /// <returns></returns>
    public bool HitTest(string hitAreaName, float x, float y)
    {
        // 完全透明时不做命中判定.
        if (Opacity < 1)
        {
            return false;
        }
        if (_modelSetting.HitAreas?.Count > 0)
        {
            for (int i = 0; i < _modelSetting.HitAreas?.Count; i++)
            {
                if (_modelSetting.HitAreas[i].Name == hitAreaName)
                {
                    var id = Context.IdManager.GetId(_modelSetting.HitAreas[i].Id);

                    return IsHit(id, x, y);
                }
            }
        }
        return false; // 不存在时返回 false
    }

    /// <summary>
    /// 绘制模型的处理. 传入绘制模型所在空间的 View-Projection 矩阵.
    /// </summary>
    protected void DoDraw()
    {
        if (Model == null)
        {
            return;
        }

        Renderer?.DrawModel();
    }

    /// <summary>
    /// 按组名批量释放动作数据.
    /// 动作数据的名字在内部从 ModelSetting 获取.
    /// </summary>
    /// <param name="group">动作数据的组名</param>
    private void ReleaseMotionGroup(string group)
    {
        var list = _modelSetting.FileReferences.Motions[group];
        for (int i = 0; i < list.Count; i++)
        {
            string voice = list[i].Sound;
            //TODO Voice
        }
    }

    /// <summary>
    /// 把纹理加载到 OpenGL 的纹理单元
    /// </summary>
    private void SetupTextures()
    {
        if (_modelSetting.FileReferences?.Textures?.Count > 0)
        {
            for (int index = 0; index < _modelSetting.FileReferences.Textures.Count; index++)
            {
                var texturePath = _modelSetting.FileReferences.Textures[index];
                Textures.Add(LoadTexture(index, texturePath));
            }
        }
    }

    /// <summary>
    /// 读取一张纹理.
    /// 解码失败时用 1x1 占位纹理顶替, 保证 Textures 的下标与纹理单元编号一一对应.
    /// </summary>
    /// <param name="index">纹理单元编号, 即 model3.json 中 Textures 数组的下标</param>
    /// <param name="texturePath">model3.json 中记录的纹理路径</param>
    /// <returns>纹理信息; 解码失败时为占位纹理</returns>
    private TextureInfo LoadTexture(int index, string? texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            // 空路径也要占位: 直接跳过会让后面的纹理整体前移, 绑定到错误的纹理单元.
            return _lapp.TextureManager.CreatePlaceholderTexture(this, index, string.Empty);
        }

        var fullPath = Path.GetFullPath(Path.Combine(_modelHomeDir, texturePath));

        // 不能直接跳过解码失败的纹理: OpenGL 按显式编号绑定, Vulkan 按创建顺序追加,
        // 跳过会让后续纹理整体前移, 因此必须用占位纹理保持索引不变.
        return _lapp.TextureManager.CreateTextureOrPlaceholder(this, index, fullPath);
    }

    /// <summary>
    /// 按组名批量加载动作数据.
    /// 动作数据的名字在内部从 ModelSetting 获取.
    /// </summary>
    /// <param name="group">动作数据的组名</param>
    private void PreloadMotionGroup(string group)
    {
        // 获取该组中注册的动作数量
        var list = _modelSetting.FileReferences.Motions[group];

        for (int i = 0; i < list.Count; i++)
        {
            //ex) idle_0
            string name = $"{group}_{i}";

            // 读取动作数据(与官方 LAppModel::PreloadMotionGroup 相同,
            // 经 LoadMotion(modelSetting, group, index, ...) 应用 model3.json 中的淡入淡出值.
            var tmpMotion = LoadMotion(
                _modelSetting,
                group,
                i,
                _motionConsistency,
                null,
                null,
                _modelHomeDir
            );

            if (tmpMotion is not CubismMotion cubismMotion)
            {
                CubismLog.Warning($"[Live2D App]Can't load motion [{name}].");
                continue;
            }

            cubismMotion.SetEffectIds(_eyeBlinkIds, _lipSyncIds);

            if (_motions.ContainsKey(name))
            {
                _motions[name] = tmpMotion;
            }
            else
            {
                _motions.Add(name, tmpMotion);
            }
        }
    }

    /// <summary>
    /// 物理演算的更新处理(在官方 CubismPhysicsUpdater 上追加 ON/OFF 的 App 扩展).
    /// </summary>
    /// <remarks>
    /// 执行顺序直接继承 CubismPhysicsUpdater 的 600(不做自己的排序).
    /// 官方示例没有物理开关, 但宿主内切换动作/表情/物理需要它,
    /// 因此注册到 scheduler 的仍是与官方相同的 CubismPhysicsUpdater 系, 只额外增加了启用标志.
    /// </remarks>
    /// <param name="physics">物理数据</param>
    private sealed class LAppPhysicsUpdater(CubismPhysics physics) : CubismPhysicsUpdater(physics)
    {
        /// <summary>
        /// 是否执行物理演算
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 更新处理. 无效时不做任何事.
        /// </summary>
        /// <param name="model">要更新的模型</param>
        /// <param name="deltaTimeSeconds">增量时间[秒]</param>
        public override void OnLateUpdate(CubismModel model, float deltaTimeSeconds)
        {
            if (!Enabled)
            {
                return;
            }

            base.OnLateUpdate(model, deltaTimeSeconds);
        }
    }
}
