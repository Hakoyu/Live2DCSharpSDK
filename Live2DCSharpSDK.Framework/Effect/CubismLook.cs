using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Effect;

/// <summary>
/// 让参数跟随目标 (拖拽输入) 的功能 (官方 5-r.5 的 CubismLook).
/// </summary>
/// <remarks>
/// 先作为 CubismLookUpdater 的编译前置引入, 随后整理为最终形态.
/// 生成仅通过 <see cref="Create"/> (构造函数为 private), 不制造重复入口.
/// </remarks>
public class CubismLook
{
    /// <summary>
    /// look 的参数信息 (官方 CubismLook::LookParameterData).
    /// </summary>
    public record LookParameterData
    {
        /// <summary>
        /// 参数 ID
        /// </summary>
        public required string ParameterId { get; set; }

        /// <summary>
        /// X 轴方向拖拽输入的系数
        /// </summary>
        public float FactorX { get; set; }

        /// <summary>
        /// Y 轴方向拖拽输入的系数
        /// </summary>
        public float FactorY { get; set; }

        /// <summary>
        /// X-Y (交叉) 拖拽输入的系数
        /// </summary>
        public float FactorXY { get; set; }
    }

    /// <summary>
    /// 与 look 绑定的参数信息列表
    /// </summary>
    private List<LookParameterData> _lookParameters = [];

    /// <summary>
    /// 构造函数 (与官方一致为 private. 生成请使用 <see cref="Create"/>).
    /// </summary>
    private CubismLook() { }

    /// <summary>
    /// 创建实例.
    /// </summary>
    /// <returns>创建的实例</returns>
    public static CubismLook Create()
    {
        return new CubismLook();
    }

    /// <summary>
    /// 销毁实例.
    /// </summary>
    /// <param name="instance">要销毁的实例</param>
    /// <remarks>不持有非托管资源, C# 侧只需断开引用.</remarks>
    public static void Delete(CubismLook? instance)
    {
        // C# 由 GC 回收, 因此不执行任何操作 (相当于官方的 CSM_DELETE_SELF).
    }

    /// <summary>
    /// 设置 look 的参数.
    /// </summary>
    /// <param name="lookParameters">要设置的参数信息列表</param>
    /// <remarks>官方对 csmVector 做值拷贝, 因此这里也拷贝后持有.</remarks>
    public void SetParameters(IReadOnlyList<LookParameterData> lookParameters)
    {
        _lookParameters = [.. lookParameters];
    }

    /// <summary>
    /// 获取与 look 绑定的参数信息.
    /// </summary>
    /// <returns>已设置的参数信息列表</returns>
    public IReadOnlyList<LookParameterData> GetParameters()
    {
        return _lookParameters;
    }

    /// <summary>
    /// 更新模型参数.
    /// </summary>
    /// <param name="model">更新目标的模型</param>
    /// <param name="dragX">目标的 X 坐标</param>
    /// <param name="dragY">目标的 Y 坐标</param>
    public void UpdateParameters(CubismModel model, float dragX, float dragY)
    {
        float dragXY = dragX * dragY;

        for (int i = 0; i < _lookParameters.Count; ++i)
        {
            var data = _lookParameters[i];

            model.AddParameterValue(
                data.ParameterId,
                (data.FactorX * dragX) + (data.FactorY * dragY) + (data.FactorXY * dragXY)
            );
        }
    }
}
