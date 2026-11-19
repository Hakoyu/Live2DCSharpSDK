using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Motion;
using Live2DCSharpSDK.Framework.Physics;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// ユーザーが実際に使用するモデルの基底クラス。これを継承してユーザーが実装する。
/// </summary>
public abstract class CubismUserModel : IDisposable
{
    /// <summary>
    /// レンダラ
    /// </summary>
    public CubismRenderer? Renderer { get; protected set; }
    /// <summary>
    /// モデル行列
    /// </summary>
    public CubismModelMatrix ModelMatrix { get; protected set; }
    /// <summary>
    /// Modelインスタンス
    /// </summary>
    public CubismModel Model => _moc.Model;
    /// <summary>
    /// 初期化されたかどうか
    /// </summary>
    public bool Initialized { get; set; }
    /// <summary>
    /// 更新されたかどうか
    /// </summary>
    public bool Updating { get; set; }
    /// <summary>
    /// 不透明度
    /// </summary>
    public float Opacity { get; set; }

    /// <summary>
    /// Mocデータ
    /// </summary>
    protected CubismMoc _moc;

    /// <summary>
    /// 破棄済みか（S1: Dispose の冪等ガード）
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 更新処理のスケジューラ（官方 5-r.5 で CubismUserModel に追加された）
    /// </summary>
    protected readonly CubismUpdateScheduler _updateScheduler;

    /// <summary>
    /// モーション管理
    /// </summary>
    protected CubismMotionManager _motionManager;
    /// <summary>
    /// 表情管理
    /// </summary>
    protected CubismExpressionMotionManager _expressionManager;
    /// <summary>
    /// 自動まばたき
    /// </summary>
    protected CubismEyeBlink? _eyeBlink;
    /// <summary>
    /// 呼吸
    /// </summary>
    protected CubismBreath _breath;
    /// <summary>
    /// ポーズ管理
    /// </summary>
    protected CubismPose? _pose;
    /// <summary>
    /// マウスドラッグ
    /// </summary>
    protected CubismTargetPoint _dragManager;
    /// <summary>
    /// 物理演算
    /// </summary>
    protected CubismPhysics? _physics;
    /// <summary>
    /// ユーザデータ
    /// </summary>
    protected CubismModelUserData? _modelUserData;
    /// <summary>
    /// 最後のリップシンクの制御値
    /// </summary>
    protected float _lastLipSyncValue;
    /// <summary>
    /// X軸方向の加速度
    /// </summary>
    protected float _accelerationX;
    /// <summary>
    /// Y軸方向の加速度
    /// </summary>
    protected float _accelerationY;
    /// <summary>
    /// Z軸方向の加速度
    /// </summary>
    protected float _accelerationZ;
    /// <summary>
    /// MOC3整合性検証するかどうか
    /// </summary>
    protected bool _mocConsistency;
    /// <summary>
    /// モーション整合性検証するかどうか（官方 5-r.5 で追加）
    /// </summary>
    protected bool _motionConsistency;

    // 注: 5-r.5 で _lipSync / _dragX / _dragY は基底クラスから削除された
    // （口型は CubismLipSyncUpdater、ドラッグは CubismLookUpdater が担当する。PLAN U11 / U12 / D-03）。

    /// <summary>
    /// CubismMotionQueueManagerにイベント用に登録するためのCallback。
    /// CubismUserModelの継承先のEventFiredを呼ぶ。
    /// </summary>
    /// <param name="eventValue">発火したイベントの文字列データ</param>
    /// <param name="customData">CubismUserModelを継承したインスタンスを想定</param>
    public static void CubismDefaultMotionEventCallback(CubismUserModel? customData, string eventValue)
    {
        customData?.MotionEventFired(eventValue);
    }

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    public CubismUserModel()
    {
        Opacity = 1.0f;

        // 更新処理のスケジューラを作成
        _updateScheduler = new();

        // モーションマネージャーを作成
        // MotionQueueManagerクラスからの継承なので使い方は同じ
        _motionManager = new();
        _motionManager.SetEventCallback(CubismDefaultMotionEventCallback, this);

        // 表情モーションマネージャを作成
        _expressionManager = new();

        // ドラッグによるアニメーション
        _dragManager = new();
    }

    /// <summary>
    /// 解放する（G2 finding F1: 派生クラスの Dispose が確実に呼ばれるよう virtual にする。
    /// S1: 冪等。二重呼び出しで Moc / Model を二重解放しない）。
    /// </summary>
    public virtual void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _updateScheduler.Dispose();

        // LoadModel 前に解放された場合は _moc が未生成（.editorconfig で CS8618 を無効化しているため
        // 非 null 注釈でも実行時 null になり得る。analyse.md H-1）。
        _moc?.Dispose();

        DeleteRenderer();
    }

    /// <summary>
    /// 更新処理をスケジューラに登録する（官方 5-r.5 の _updateScheduler.AddUpdatableList のラッパー）。
    /// </summary>
    /// <param name="updater">登録する更新処理</param>
    protected void RegisterUpdater(ICubismUpdater updater)
    {
        _updateScheduler.AddUpdatableList(updater);
    }

    /// <summary>
    /// マウスドラッグの情報を設定する。
    /// </summary>
    /// <param name="x">ドラッグしているカーソルのX位置</param>
    /// <param name="y">ドラッグしているカーソルのY位置</param>
    public void SetDragging(float x, float y)
    {
        _dragManager.Set(x, y);
    }

    /// <summary>
    /// 加速度の情報を設定する。
    /// </summary>
    /// <param name="x">X軸方向の加速度</param>
    /// <param name="y">Y軸方向の加速度</param>
    /// <param name="z">Z軸方向の加速度</param>
    protected void SetAcceleration(float x, float y, float z)
    {
        _accelerationX = x;
        _accelerationY = y;
        _accelerationZ = z;
    }

    /// <summary>
    /// モデルデータを読み込む。
    /// </summary>
    /// <param name="buffer">moc3ファイルが読み込まれているバッファ</param>
    /// <param name="shouldCheckMocConsistency">MOCの整合性チェックフラグ(初期値 : false)</param>
    protected void LoadModel(byte[] buffer, bool shouldCheckMocConsistency = false)
    {
        _moc = new CubismMoc(buffer, shouldCheckMocConsistency);
        Model.SaveParameters();
        ModelMatrix = new CubismModelMatrix(Model.GetCanvasWidth(), Model.GetCanvasHeight());
    }

    /// <summary>
    /// モーションデータを読み込む（官方 5-r.5 の CubismUserModel::LoadMotion）。
    /// ローカルは moc3 と同様にファイルパスを受け取る形にしている。
    /// model3.json のフェード値で上書きしない版（fade 引数は null 相当）。
    /// </summary>
    /// <param name="buffer">motion3.jsonのファイルパス</param>
    /// <param name="name">モーション名（ログ用）</param>
    /// <param name="onFinishedMotionHandler">モーション再生終了時に呼び出されるコールバック関数。NULLの場合、呼び出されない。</param>
    /// <param name="onBeganMotionHandler">モーション再生開始時に呼び出されるコールバック関数。NULLの場合、呼び出されない。</param>
    /// <param name="shouldCheckMotionConsistency">motion3.jsonの整合性を検証するかどうか</param>
    /// <returns>生成したモーションデータ。読み込めない場合は null</returns>
    protected ACubismMotion? LoadMotion(string buffer, string name,
        FinishedMotionCallback? onFinishedMotionHandler = null,
        BeganMotionCallback? onBeganMotionHandler = null,
        bool shouldCheckMotionConsistency = false)
    {
        return LoadMotionCore(buffer, name, null, null,
            onFinishedMotionHandler, onBeganMotionHandler, shouldCheckMotionConsistency);
    }

    /// <summary>
    /// モデル設定からモーションデータを読み込む（官方 5-r.5 の CubismUserModel::LoadMotion の modelSetting 版）。
    /// 官方の ICubismModelSetting* に相当するものは、本地では ModelSettingObj（model3.json の DTO）である。
    /// model3.json の FadeInTime / FadeOutTime が 0 以上の場合、motion3.json 側のフェード値を上書きする
    /// （官方 GetMotionFadeInTimeValue / GetMotionFadeOutTimeValue と同じ判定）。
    /// </summary>
    /// <param name="modelSetting">モデル設定（公式 ICubismModelSetting* の本地等価物）</param>
    /// <param name="group">モーショングループ名</param>
    /// <param name="index">グループ内のインデックス</param>
    /// <param name="shouldCheckMotionConsistency">motion3.jsonの整合性を検証するかどうか</param>
    /// <param name="onFinishedMotionHandler">モーション再生終了時に呼び出されるコールバック関数。NULLの場合、呼び出されない。</param>
    /// <param name="onBeganMotionHandler">モーション再生開始時に呼び出されるコールバック関数。NULLの場合、呼び出されない。</param>
    /// <param name="modelHomeDir">model3.json が置かれたディレクトリ（motion の相対パス解決用）。NULL の場合はカレントディレクトリ基準</param>
    /// <returns>生成したモーションデータ。グループ/インデックス/ファイルが無い場合は null</returns>
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

        // 負値は「motion3.json 側の値を上書きしない」を意味する（官方と同じ）
        var fadeInTime = modelSetting.GetMotionFadeInTimeValue(group, index);
        var fadeOutTime = modelSetting.GetMotionFadeOutTimeValue(group, index);

        return LoadMotionCore(path, $"{group}_{index}",
            fadeInTime >= 0.0f ? fadeInTime : null,
            fadeOutTime >= 0.0f ? fadeOutTime : null,
            onFinishedMotionHandler, onBeganMotionHandler, shouldCheckMotionConsistency);
    }

    /// <summary>
    /// モーションデータ読み込みの共通処理。
    /// </summary>
    /// <param name="buffer">motion3.jsonのファイルパス</param>
    /// <param name="name">モーション名（ログ用）</param>
    /// <param name="fadeInTime">上書きするフェードイン時間（null は上書きしない）</param>
    /// <param name="fadeOutTime">上書きするフェードアウト時間（null は上書きしない）</param>
    /// <param name="onFinishedMotionHandler">モーション再生終了時に呼び出されるコールバック関数</param>
    /// <param name="onBeganMotionHandler">モーション再生開始時に呼び出されるコールバック関数</param>
    /// <param name="shouldCheckMotionConsistency">motion3.jsonの整合性を検証するかどうか</param>
    /// <returns>生成したモーションデータ。読み込めない場合は null</returns>
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
            // 官方 CubismMotion::Create は整合性不一致時に NULL を返し、呼び出し側（LAppModel）はそのモーションをスキップする。
            // 本地はコンストラクタで例外を投げるため、ここで捕捉して NULL 相当（null）に変換する（G2 finding F2）。
            // 注意: 引数不正・ファイル不在・JSON 破損など他の例外は握りつぶさず、そのまま伝播させる。
            CubismLog.Warning("[Live2D SDK]failed to load motion (inconsistent): {0} : {1}", name, e.Message);
            return null;
        }

        // 官方 LoadMotion: model3.json の FadeInTime / FadeOutTime が 0 以上なら上書きする
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
    /// ポーズデータを読み込む。
    /// </summary>
    /// <param name="buffer">pose3.jsonが読み込まれているバッファ</param>
    protected void LoadPose(string buffer)
    {
        _pose = new CubismPose(buffer);
    }

    /// <summary>
    /// 物理演算データを読み込む。
    /// </summary>
    /// <param name="buffer">physics3.jsonが読み込まれているバッファ</param>
    protected void LoadPhysics(string buffer)
    {
        _physics = new CubismPhysics(buffer);
    }

    /// <summary>
    /// ユーザーデータを読み込む。
    /// </summary>
    /// <param name="buffer">userdata3.jsonが読み込まれているバッファ</param>
    protected void LoadUserData(string buffer)
    {
        _modelUserData = new CubismModelUserData(buffer);
    }

    /// <summary>
    /// 未復元の Moc バッファから .moc3 のバージョンを取得する（官方 CubismUserModel::GetMocVersionFromBuffer）。
    /// </summary>
    /// <param name="buffer">moc3ファイルが読み込まれているバッファ</param>
    /// <returns>.moc3 Version</returns>
    public static uint GetMocVersionFromBuffer(byte[] buffer)
    {
        return CubismMoc.GetMocVersionFromBuffer(buffer);
    }

    /// <summary>
    /// 指定した位置にDrawableがヒットしているかどうかを取得する。
    /// </summary>
    /// <param name="drawableId">検証したいDrawableのID</param>
    /// <param name="pointX">X位置</param>
    /// <param name="pointY">Y位置</param>
    /// <returns>true    ヒットしている
    /// false   ヒットしていない</returns>
    public unsafe bool IsHit(string drawableId, float pointX, float pointY)
    {
        var drawIndex = Model.GetDrawableIndex(drawableId);

        if (drawIndex < 0)
        {
            return false; // 存在しない場合はfalse
        }

        var count = Model.GetDrawableVertexCount(drawIndex);
        var vertices = Model.GetDrawableVertices(drawIndex);

        // S2: 頂点が空（count == 0）または null の場合はヒット判定しない。
        // 旧実装は count / vertices を検証せず vertices[0] / vertices[1] を直接読んでいたため、
        // 空メッシュや解放済みモデル（CubismModel.Dispose 後は count=0, vertices=null）で AV になっていた。
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
    /// レンダラを生成して初期化を実行する。
    /// 本地は「ホストが生成したレンダラを設定する」方式を維持する（PLAN D-06）。
    /// </summary>
    protected void CreateRenderer(CubismRenderer renderer)
    {
        Renderer = renderer;
    }

    /// <summary>
    /// オフスクリーン（レンダーターゲット）のサイズを設定する（官方 5-r.5 の新規 API）。
    /// ホストが実サイズで毎フレーム呼ぶ想定（PLAN D-06）。
    /// </summary>
    /// <param name="width">モデルを描画するバッファの幅</param>
    /// <param name="height">モデルを描画するバッファの高さ</param>
    public void SetRenderTargetSize(uint width, uint height)
    {
        Renderer?.SetRenderTargetSize(width, height);
    }

    /// <summary>
    /// レンダラを解放する。
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
    /// Eventが再生処理時にあった場合の処理をする。
    /// 継承で上書きすることを想定している。
    /// 上書きしない場合はログ出力をする。
    /// </summary>
    /// <param name="eventValue">発火したイベントの文字列データ</param>
    protected abstract void MotionEventFired(string eventValue);
}
