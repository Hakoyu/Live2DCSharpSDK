using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Motion;

namespace Live2DCSharpSDK.App;

/// <summary>
/// 示例应用程序中管理 CubismModel 的类
/// 负责模型的创建与销毁, 点击事件处理, 模型切换.
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
public class LAppLive2DManager(LAppDelegate lapp) : IDisposable
{
    public event Action<CubismModel, ACubismMotion>? MotionFinished;

    /// <summary>
    /// 动作播放开始时触发(对应官方 5-r.5 的 BeganMotionCallback)
    /// </summary>
    public event Action<ACubismMotion>? MotionBegan;

    /// <summary>
    /// 绘制模型使用的 View 矩阵
    /// </summary>
    public CubismMatrix44 ViewMatrix { get; } = new();

    /// <summary>
    /// 模型实例容器
    /// </summary>
    private readonly List<LAppModel> _models = [];

    /// <summary>
    /// 返回当前场景中持有的模型
    /// </summary>
    /// <param name="no">模型列表的索引值</param>
    /// <returns>返回模型实例. 索引值越界时返回 NULL.</returns>
    public LAppModel GetModel(int no)
    {
        return _models[no];
    }

    /// <summary>
    /// 释放当前场景中持有的所有模型
    /// </summary>
    public void ReleaseAllModel()
    {
        for (int i = 0; i < _models.Count; i++)
        {
            _models[i].Dispose();
        }

        _models.Clear();
    }

    /// <summary>
    /// 拖拽画面时的处理
    /// </summary>
    /// <param name="x">画面的 X 坐标</param>
    /// <param name="y">画面的 Y 坐标</param>
    public void OnDrag(float x, float y)
    {
        for (int i = 0; i < _models.Count; i++)
        {
            LAppModel model = GetModel(i);

            model.SetDragging(x, y);
        }
    }

    /// <summary>
    /// 点击画面时的处理
    /// </summary>
    /// <param name="x">画面的 X 坐标</param>
    /// <param name="y">画面的 Y 坐标</param>
    public void OnTap(float x, float y)
    {
        CubismLog.Debug($"[Live2D App]tap point: x:{x:0.00} y:{y:0.00}");

        for (int i = 0; i < _models.Count; i++)
        {
            if (_models[i].HitTest(LAppDefine.HitAreaNameHead, x, y))
            {
                CubismLog.Debug($"[Live2D App]hit area: [{LAppDefine.HitAreaNameHead}]");
                _models[i].SetRandomExpression();
            }
            else if (_models[i].HitTest(LAppDefine.HitAreaNameBody, x, y))
            {
                CubismLog.Debug($"[Live2D App]hit area: [{LAppDefine.HitAreaNameBody}]");
                _models[i].StartRandomMotion(LAppDefine.MotionGroupTapBody, MotionPriority.PriorityNormal, OnFinishedMotion, OnBeganMotion);
            }
        }
    }

    private void OnFinishedMotion(CubismModel model, ACubismMotion self)
    {
        CubismLog.Info($"[Live2D App]Motion Finished: {self}");
        MotionFinished?.Invoke(model, self);
    }

    private void OnBeganMotion(ACubismMotion self)
    {
        CubismLog.Info($"[Live2D App]Motion Began: {self}");
        MotionBegan?.Invoke(self);
    }

    /// <summary>
    /// 设置模型使用的渲染目标尺寸(官方 LAppLive2DManager::SetRenderTargetSize).
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    public void SetRenderTargetSize(int width, int height)
    {
        for (int i = 0; i < _models.Count; i++)
        {
            GetModel(i).SetRenderTargetSize((uint)width, (uint)height);
        }
    }

    private readonly CubismMatrix44 _projection = new();

    /// <summary>
    /// 刷新画面时的处理
    /// 执行模型的更新处理与绘制处理.
    /// </summary>
    public void OnUpdate()
    {
        lapp.OnUpdatePre();

        int width = lapp.WindowWidth;
        int height = lapp.WindowHeight;
        foreach (var model in _models)
        {
            _projection.LoadIdentity();

            if (model.Model.GetCanvasWidth() > 1.0f && width < height)
            {
                // 把横向较长的模型显示在纵向较长的窗口时, 用模型的横向尺寸计算 scale
                model.ModelMatrix.SetWidth(2.0f);
                _projection.Scale(1.0f, (float)width / height);
            }
            else
            {
                _projection.Scale((float)height / width, 1.0f);
            }

            // 如有需要在此进行乘法运算
            if (ViewMatrix != null)
            {
                _projection.MultiplyByMatrix(ViewMatrix);
            }

            model.Update();
            model.Draw(_projection); // 因为是引用传递, projection 会被改写
        }
    }

    public LAppModel LoadModel(string dir, string name)
    {
        CubismLog.Debug($"[Live2D App]model load: {name}");

        // 从 ModelDir[] 中保存的目录名出发,
        // 确定 model3.json 的路径.
        // 需要让目录名与 model3.json 的名字保持一致.
        if (!dir.EndsWith('\\') && !dir.EndsWith('/'))
        {
            dir = Path.GetFullPath(dir + '/');
        }
        var modelJsonName = Path.GetFullPath($"{dir}{name}");
        if (!File.Exists(modelJsonName))
        {
            modelJsonName = Path.GetFullPath($"{dir}{name}.model3.json");
        }
        if (!File.Exists(modelJsonName))
        {
            dir = Path.GetFullPath(dir + name + '/');
            modelJsonName = Path.GetFullPath($"{dir}{name}.model3.json");
        }
        if (!File.Exists(modelJsonName))
        {
            throw new Exception($"[Live2D]File not found: {modelJsonName}");
        }

        var model = new LAppModel(lapp, dir, modelJsonName);
        _models.Add(model);

        return model;
    }

    public void RemoveModel(int index)
    {
        if (_models.Count > index)
        {
            var model = _models[index];
            _models.RemoveAt(index);
            model.Dispose();
        }
    }

    public void RemoveModel(LAppModel model)
    {
        _models.Remove(model);
        model.Dispose();
    }

    /// <summary>
    /// 获取模型个数
    /// </summary>
    /// <returns>持有的模型个数</returns>
    public int GetModelNum()
    {
        return _models.Count;
    }

    public void Dispose()
    {
        ReleaseAllModel();
    }
}
