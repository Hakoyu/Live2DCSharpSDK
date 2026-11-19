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
    /// モデルセッティング情報
    /// </summary>
    private readonly ModelSettingObj _modelSetting;
    /// <summary>
    /// モデルセッティングが置かれたディレクトリ
    /// </summary>
    private readonly string _modelHomeDir;
    /// <summary>
    /// モデルに設定されたまばたき機能用パラメータID
    /// </summary>
    private readonly List<string> _eyeBlinkIds = [];
    /// <summary>
    /// モデルに設定されたリップシンク機能用パラメータID
    /// </summary>
    private readonly List<string> _lipSyncIds = [];
    /// <summary>
    /// 読み込まれているモーションのリスト
    /// </summary>
    private readonly Dictionary<string, ACubismMotion> _motions = [];
    /// <summary>
    /// 読み込まれている表情のリスト
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
                list.Add((Model.GetPartId(a),
                    a, Model.GetPartOpacity(a)));
            }
            return list;
        }
    }

    public List<TextureInfo> Textures = [];

    public List<string> Parameters => new(Model.ParameterIds);

    /// <summary>
    /// デルタ時間の積算値[秒]
    /// </summary>
    public float UserTimeSeconds { get; set; }

    public bool RandomMotion { get; set; } = true;
    public bool CustomValueUpdate { get; set; }

    public Action<LAppModel>? ValueUpdate;

    /// <summary>
    /// モデルが物理データを持つかどうか（ローカル拡張。ホストの切替 UI 用）
    /// </summary>
    public bool HasPhysics => _physicsUpdater != null;

    /// <summary>
    /// 物理演算を実行するかどうか（ローカル拡張。官方サンプルには切替が無い。V6 の切替検証用）。
    /// 物理データを持たないモデルでは常に false になる。
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
    /// パラメータID: ParamAngleX
    /// </summary>
    public string IdParamAngleX { get; set; }
    /// <summary>
    /// パラメータID: ParamAngleY
    /// </summary>
    public string IdParamAngleY { get; set; }
    /// <summary>
    /// パラメータID: ParamAngleZ
    /// </summary>
    public string IdParamAngleZ { get; set; }
    /// <summary>
    /// パラメータID: ParamBodyAngleX
    /// </summary>
    public string IdParamBodyAngleX { get; set; }
    /// <summary>
    /// パラメータID: ParamEyeBallX
    /// </summary>
    public string IdParamEyeBallX { get; set; }
    /// <summary>
    /// パラメータID: ParamEyeBallXY
    /// </summary>
    public string IdParamEyeBallY { get; set; }

    public string IdParamBreath { get; set; } = CubismFramework.CubismIdManager
        .GetId(CubismDefaultParameterId.ParamBreath);

    /// <summary>
    /// ドラッグによる顔の向きのパラメータ情報（官方 5-r.5 の CubismLook）
    /// </summary>
    private readonly CubismLook _look;

    /// <summary>
    /// モーションによるパラメータ更新の有無（官方 5-r.5 の _motionUpdated。
    /// CubismEyeBlinkUpdater が Func&lt;bool&gt; 経由で毎フレーム参照する）
    /// </summary>
    private bool _motionUpdated;

    /// <summary>
    /// 物理演算の更新処理（物理の ON/OFF を切り替えるため App 側で派生。公式は CubismPhysicsUpdater をそのまま登録）
    /// </summary>
    private readonly LAppPhysicsUpdater? _physicsUpdater;

    /// <summary>
    /// wavファイルハンドラ
    /// </summary>
    /// <remarks>
    /// PLAN D-03 / U3: 本輪は wav デコードを導入しないため未実装（口型は CubismLipSyncUpdater を登録しない＝常に無効）。
    /// </remarks>
    //LAppWavFileHandler _wavFileHandler;

    private readonly LAppDelegate _lapp;

    private readonly Random _random = new();

    public event Action<LAppModel, string>? Motion;

    public LAppModel(LAppDelegate lapp, string dir, string fileName)
    {
        _lapp = lapp;

        if (LAppDefine.MocConsistencyValidationEnable)
        {
            _mocConsistency = true;
        }

        // motion3.json の整合性検証（官方 LAppModel.cpp:45-48）
        if (LAppDefine.MotionConsistencyValidationEnable)
        {
            _motionConsistency = true;
        }

        IdParamAngleX = CubismFramework.CubismIdManager
            .GetId(CubismDefaultParameterId.ParamAngleX);
        IdParamAngleY = CubismFramework.CubismIdManager
            .GetId(CubismDefaultParameterId.ParamAngleY);
        IdParamAngleZ = CubismFramework.CubismIdManager.
            GetId(CubismDefaultParameterId.ParamAngleZ);
        IdParamBodyAngleX = CubismFramework.CubismIdManager
            .GetId(CubismDefaultParameterId.ParamBodyAngleX);
        IdParamEyeBallX = CubismFramework.CubismIdManager
            .GetId(CubismDefaultParameterId.ParamEyeBallX);
        IdParamEyeBallY = CubismFramework.CubismIdManager
            .GetId(CubismDefaultParameterId.ParamEyeBallY);

        _modelHomeDir = dir;

        CubismLog.Debug($"[Live2D App]load model setting: {fileName}");

        using var stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        _modelSetting = JsonSerializer.Deserialize(stream, ModelSettingObjContext.Default.ModelSettingObj)
            ?? throw new Exception("model3.json error");

        Updating = true;
        Initialized = false;

        //Cubism Model
        var path = _modelSetting.FileReferences?.Moc;
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = Path.GetFullPath(_modelHomeDir + path);
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
                path = Path.GetFullPath(_modelHomeDir + path);
                if (!File.Exists(path))
                {
                    continue;
                }

                var motion = new CubismExpressionMotion(path);

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
            path = Path.GetFullPath(_modelHomeDir + path);
            if (File.Exists(path))
            {
                LoadPhysics(path);
            }
        }

        //Pose
        path = _modelSetting.FileReferences?.Pose;
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = Path.GetFullPath(_modelHomeDir + path);
            if (File.Exists(path))
            {
                LoadPose(path);
            }
        }

        //EyeBlink
        if (_modelSetting.IsExistEyeBlinkParameters())
        {
            _eyeBlink = new CubismEyeBlink(_modelSetting);
        }

        LoadBreath();

        //UserData
        path = _modelSetting.FileReferences?.UserData;
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = Path.GetFullPath(_modelHomeDir + path);
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
        // 更新処理の登録（官方 LAppModel::SetupModel の LAppModel.cpp:158-272）
        // 実行順は登録順ではなく ICubismUpdater の executionOrder で決まる:
        //   200 EyeBlink → 300 Expression → 400 Look → 500 Breath → 600 Physics → 700 LipSync → 800 Pose
        //-----------------------------------------------------------------

        // 表情（官方 LAppModel.cpp:158-159）
        RegisterUpdater(new CubismExpressionUpdater(_expressionManager));

        // 物理演算（官方 LAppModel.cpp:172-173）
        if (_physics != null)
        {
            _physicsUpdater = new LAppPhysicsUpdater(_physics);
            RegisterUpdater(_physicsUpdater);
        }

        // ポーズ（官方 LAppModel.cpp:188-189）
        if (_pose != null)
        {
            RegisterUpdater(new CubismPoseUpdater(_pose));
        }

        // まばたき（官方 LAppModel.cpp:198-201。モーションで更新されたフレームは瞬きしない）
        if (_eyeBlink != null)
        {
            RegisterUpdater(new CubismEyeBlinkUpdater(() => _motionUpdated, _eyeBlink));
        }

        // 呼吸（官方 LAppModel.cpp:219-220）
        RegisterUpdater(new CubismBreathUpdater(_breath));

        // リップシンクは登録しない（PLAN D-03: wav デコードを導入しないため。口型の値は常に 0 相当）
        // 官方 LAppModel.cpp:249-250 は IParameterProvider を渡して 700 番に登録する。

        // ドラッグによる顔の向き（官方 LAppModel.cpp:255-269。係数は R1 の手書き実装と同一）
        _look = CubismLook.Create();
        _look.SetParameters(
        [
            new() { ParameterId = IdParamAngleX, FactorX = 30.0f },
            new() { ParameterId = IdParamAngleY, FactorY = 30.0f },
            new() { ParameterId = IdParamAngleZ, FactorXY = -30.0f },
            new() { ParameterId = IdParamBodyAngleX, FactorX = 10.0f },
            new() { ParameterId = IdParamEyeBallX, FactorX = 1.0f },
            new() { ParameterId = IdParamEyeBallY, FactorY = 1.0f },
        ]);
        RegisterUpdater(new CubismLookUpdater(_look, _dragManager));

        // 登録後の並べ替え（官方 LAppModel.cpp:272。忘れても OnLateUpdate が自動で並べ替える）
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

    public new void Dispose()
    {
        base.Dispose();

        _motions.Clear();
        _expressions.Clear();

        // model3.json に Motions が無いモデル（大肥鱼など）でも例外にしない
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
                    Weight = 0.5f
                },
                new()
                {
                    ParameterId = IdParamAngleY,
                    Offset = 0.0f,
                    Peak = 8.0f,
                    Cycle = 3.5345f,
                    Weight = 0.5f
                },
                new()
                {
                    ParameterId = IdParamAngleZ,
                    Offset = 0.0f,
                    Peak = 10.0f,
                    Cycle = 5.5345f,
                    Weight = 0.5f
                },
                new()
                {
                    ParameterId = IdParamBodyAngleX,
                    Offset = 0.0f,
                    Peak = 4.0f,
                    Cycle = 15.5345f,
                    Weight = 0.5f
                },
                new()
                {
                    ParameterId = IdParamBreath,
                    Offset = 0.5f,
                    Peak = 0.5f,
                    Cycle = 3.2345f,
                    Weight = 0.5f
                }
            ]
        };
    }

    /// <summary>
    /// レンダラを再構築する
    /// </summary>
    public void ReloadRenderer()
    {
        DeleteRenderer();

        CreateRenderer(_lapp.CreateRenderer(Model));

        SetupTextures();
    }

    /// <summary>
    /// モデルの更新処理。モデルのパラメータから描画状態を決定する。
    /// </summary>
    /// <remarks>
    /// 官方 5-r.5 の LAppModel::Update()（LAppModel.cpp:379-408）と同じ順序:
    /// 時間更新 → モーション更新 → 不透明度取得 → CubismUpdateScheduler（200→800）→ Model.Update()。
    /// R1 では まばたき/表情/ドラッグ/呼吸/物理/リップシンク/ポーズ を手書きしていたが、
    /// 5-r.5 では SetupModel で登録した 6 つの Updater が scheduler から順に呼ばれる。
    /// </remarks>
    public void Update()
    {
        float deltaTimeSeconds = LAppPal.DeltaTime;
        UserTimeSeconds += deltaTimeSeconds;

        // モーションによるパラメータ更新の有無（CubismEyeBlinkUpdater が参照する）
        _motionUpdated = false;

        //-----------------------------------------------------------------
        Model.LoadParameters(); // 前回セーブされた状態をロード
        if (_motionManager.IsFinished() && RandomMotion)
        {
            // モーションの再生がない場合、待機モーションの中からランダムで再生する
            StartRandomMotion(LAppDefine.MotionGroupIdle, MotionPriority.PriorityIdle);
        }
        else
        {
            _motionUpdated = _motionManager.UpdateMotion(Model, deltaTimeSeconds); // モーションを更新
        }
        Model.SaveParameters(); // 状態を保存

        //-----------------------------------------------------------------

        // 不透明度
        Opacity = Model.GetModelOpacity();

        // まばたき(200) → 表情(300) → ドラッグ(400) → 呼吸(500) → 物理(600) → ポーズ(800)
        _updateScheduler.OnLateUpdate(Model, deltaTimeSeconds);

        // ホスト独自のパラメータ更新（ローカル拡張。官方には無い）。
        // scheduler（＝ドラッグ/Look を含む）の後、Model.Update() の前に呼ぶ。
        if (CustomValueUpdate)
        {
            ValueUpdate?.Invoke(this);
        }

        Model.Update();
    }

    /// <summary>
    /// モデルを描画する処理。モデルを描画する空間のView-Projection行列を渡す。
    /// </summary>
    /// <param name="matrix">View-Projection行列</param>
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
    /// 引数で指定したモーションの再生を開始する。
    /// </summary>
    /// <param name="group">モーショングループ名</param>
    /// <param name="no">グループ内の番号</param>
    /// <param name="priority">優先度</param>
    /// <param name="onFinishedMotionHandler">モーション再生終了時に呼び出されるコールバック関数。NULLの場合、呼び出されない。</param>
    /// <param name="onBeganMotionHandler">モーション再生開始時に呼び出されるコールバック関数。NULLの場合、呼び出されない。</param>
    /// <returns>開始したモーションの識別番号を返す。個別のモーションが終了したか否かを判定するIsFinished()の引数で使用する。開始できない時は「-1」</returns>
    public CubismMotionQueueEntry? StartMotion(string name, MotionPriority priority, FinishedMotionCallback? onFinishedMotionHandler = null, BeganMotionCallback? onBeganMotionHandler = null)
    {
        var temp = name.Split("_");
        if (temp.Length != 2)
        {
            throw new Exception("motion name error");
        }
        return StartMotion(temp[0], int.Parse(temp[1]), priority, onFinishedMotionHandler, onBeganMotionHandler);
    }

    public CubismMotionQueueEntry? StartMotion(string group, int no, MotionPriority priority, FinishedMotionCallback? onFinishedMotionHandler = null, BeganMotionCallback? onBeganMotionHandler = null)
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

        var item = _modelSetting.FileReferences.Motions[group][no];

        //ex) idle_0
        string name = $"{group}_{no}";

        CubismMotion motion;
        if (!_motions.TryGetValue(name, out var value))
        {
            // 官方 LAppModel::StartMotion（LAppModel.cpp:436-439）と同じく、
            // LoadMotion(modelSetting, group, no, ...) 経由で model3.json の FadeInTime / FadeOutTime を反映する（PLAN U01）。
            var loaded = LoadMotion(_modelSetting, group, no, _motionConsistency,
                onFinishedMotionHandler, onBeganMotionHandler, _modelHomeDir);
            if (loaded is not CubismMotion cubismMotion)
            {
                // 官方は CubismLogError（アサートのみ）。本地の CubismLog.Error は例外を投げるため、
                // ホストを巻き込まないよう Warning で記録して null を返す（P7 判断。Framework 側は変更しない）。
                CubismLog.Warning($"[Live2D App]Can't start motion [{group}_{no}].");

                // 読み込めなかったモーションの予約優先度を戻す（官方 LAppModel.cpp:448）
                _motionManager.ReservePriority = MotionPriority.PriorityNone;
                return null;
            }

            motion = cubismMotion;
            motion.SetEffectIds(_eyeBlinkIds, _lipSyncIds);
        }
        else
        {
            motion = (value as CubismMotion)!;
            motion.OnFinishedMotion = onFinishedMotionHandler;
            motion.OnBeganMotion = onBeganMotionHandler;
        }

        //voice
        string voice = item.Sound;
        if (!string.IsNullOrWhiteSpace(voice))
        {
            //string path = voice;
            //path = _modelHomeDir + path;
            // PLAN D-03: wav デコードは本輪対象外のため _wavFileHandler.Start(path) は行わない。
        }

        CubismLog.Debug($"[Live2D App]start motion: [{group}_{no}]");
        return _motionManager.StartMotionPriority(motion, priority);
    }

    /// <summary>
    /// ランダムに選ばれたモーションの再生を開始する。
    /// </summary>
    /// <param name="group">モーショングループ名</param>
    /// <param name="priority">優先度</param>
    /// <param name="onFinishedMotionHandler">モーション再生終了時に呼び出されるコールバック関数。NULLの場合、呼び出されない。</param>
    /// <param name="onBeganMotionHandler">モーション再生開始時に呼び出されるコールバック関数。NULLの場合、呼び出されない。</param>
    /// <returns>開始したモーションの識別番号を返す。個別のモーションが終了したか否かを判定するIsFinished()の引数で使用する。開始できない時は「-1」</returns>
    public object? StartRandomMotion(string group, MotionPriority priority, FinishedMotionCallback? onFinishedMotionHandler = null, BeganMotionCallback? onBeganMotionHandler = null)
    {
        if (_modelSetting.FileReferences?.Motions?.ContainsKey(group) == true)
        {
            int no = _random.Next() % _modelSetting.FileReferences.Motions[group].Count;
            return StartMotion(group, no, priority, onFinishedMotionHandler, onBeganMotionHandler);
        }

        return null;
    }

    /// <summary>
    /// モーショングループ名の一覧（ホストの切り替え UI 用の App 拡張。官方サンプルには無い）。
    /// </summary>
    public List<string> MotionGroups => _modelSetting.FileReferences?.Motions?.Keys.ToList() ?? [];

    /// <summary>
    /// 指定グループのモーション数を取得する（ホストの切り替え UI 用の App 拡張。官方サンプルには無い）。
    /// </summary>
    /// <param name="group">モーショングループ名</param>
    /// <returns>モーション数。グループが無い場合は 0</returns>
    public int GetMotionCount(string group)
    {
        if (_modelSetting.FileReferences?.Motions?.TryGetValue(group, out var list) == true)
        {
            return list.Count;
        }

        return 0;
    }

    /// <summary>
    /// 引数で指定した表情モーションをセットする
    /// </summary>
    /// <param name="expressionID">表情モーションのID</param>
    public void SetExpression(string expressionID)
    {
        ACubismMotion motion = _expressions[expressionID];
        CubismLog.Debug($"[Live2D App]expression: [{expressionID}]");

        if (motion != null)
        {
            // 官方 5-r.5 で CubismExpressionMotionManager.StartMotionPriority は削除された（親クラスの StartMotion を使う）。
            _expressionManager.StartMotion(motion);
        }
        else
        {
            CubismLog.Debug($"[Live2D App]expression[{expressionID}] is null ");
        }
    }

    /// <summary>
    /// ランダムに選ばれた表情モーションをセットする
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
    /// イベントの発火を受け取る
    /// </summary>
    /// <param name="eventValue"></param>
    protected override void MotionEventFired(string eventValue)
    {
        CubismLog.Debug($"[Live2D App]{eventValue} is fired on LAppModel!!");
        Motion?.Invoke(this, eventValue);
    }

    /// <summary>
    /// 当たり判定テスト。
    /// 指定IDの頂点リストから矩形を計算し、座標が矩形範囲内か判定する。
    /// </summary>
    /// <param name="hitAreaName">当たり判定をテストする対象のID</param>
    /// <param name="x">判定を行うX座標</param>
    /// <param name="y">判定を行うY座標</param>
    /// <returns></returns>
    public bool HitTest(string hitAreaName, float x, float y)
    {
        // 透明時は当たり判定なし。
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
                    var id = CubismFramework.CubismIdManager.GetId(_modelSetting.HitAreas[i].Id);

                    return IsHit(id, x, y);
                }
            }
        }
        return false; // 存在しない場合はfalse
    }

    /// <summary>
    /// モデルを描画する処理。モデルを描画する空間のView-Projection行列を渡す。
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
    /// モーションデータをグループ名から一括で解放する。
    /// モーションデータの名前は内部でModelSettingから取得する。
    /// </summary>
    /// <param name="group">モーションデータのグループ名</param>
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
    /// OpenGLのテクスチャユニットにテクスチャをロードする
    /// </summary>
    private void SetupTextures()
    {
        if (_modelSetting.FileReferences?.Textures?.Count > 0)
        {
            for (int index = 0; index < _modelSetting.FileReferences.Textures.Count; index++)
            {
                var texturePath = _modelSetting.FileReferences.Textures[index];
                if (string.IsNullOrWhiteSpace(texturePath))
                    continue;
                texturePath = Path.GetFullPath(_modelHomeDir + texturePath);
                var texture = _lapp.TextureManager.CreateTextureFromPngFile(this, index, texturePath);
                Textures.Add(texture);
            }
        }
    }

    /// <summary>
    /// モーションデータをグループ名から一括でロードする。
    /// モーションデータの名前は内部でModelSettingから取得する。
    /// </summary>
    /// <param name="group">モーションデータのグループ名</param>
    private void PreloadMotionGroup(string group)
    {
        // グループに登録されているモーション数を取得
        var list = _modelSetting.FileReferences.Motions[group];

        for (int i = 0; i < list.Count; i++)
        {
            //ex) idle_0
            string name = $"{group}_{i}";

            // モーションデータの読み込み（公式 LAppModel::PreloadMotionGroup と同じく
            // LoadMotion(modelSetting, group, index, ...) 経由で model3.json のフェード値を反映する。PLAN U01）
            var tmpMotion = LoadMotion(_modelSetting, group, i, _motionConsistency,
                null, null, _modelHomeDir);

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
    /// 物理演算の更新処理（官方 CubismPhysicsUpdater に ON/OFF を追加した App 拡張）。
    /// </summary>
    /// <remarks>
    /// 実行順序は CubismPhysicsUpdater の 600 をそのまま継承する（自前のソートはしない）。
    /// 官方サンプルに物理の切替は無いが、V6（宿主内で 動作/表情/物理 を切り替える）で必要なため、
    /// scheduler に登録する型は官方と同じ CubismPhysicsUpdater 系のまま有効フラグだけ追加している。
    /// </remarks>
    /// <param name="physics">物理演算</param>
    private sealed class LAppPhysicsUpdater(CubismPhysics physics) : CubismPhysicsUpdater(physics)
    {
        /// <summary>
        /// 物理演算を実行するかどうか
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 更新処理。無効時は何もしない。
        /// </summary>
        /// <param name="model">更新対象のモデル</param>
        /// <param name="deltaTimeSeconds">デルタ時間[秒]</param>
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
