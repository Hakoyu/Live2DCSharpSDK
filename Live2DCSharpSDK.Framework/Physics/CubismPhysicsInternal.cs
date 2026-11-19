using System.Numerics;

namespace Live2DCSharpSDK.Framework.Physics;

/// <summary>
/// 物理演算的适用目标种类.
/// </summary>
public enum CubismPhysicsTargetType
{
    /// <summary>
    /// 对参数适用
    /// </summary>
    CubismPhysicsTargetType_Parameter,
}

/// <summary>
/// 物理演算的输入种类.
/// </summary>
public enum CubismPhysicsSource
{
    /// <summary>
    /// 来自 X 轴位置
    /// </summary>
    CubismPhysicsSource_X,
    /// <summary>
    /// 来自 Y 轴位置
    /// </summary>
    CubismPhysicsSource_Y,
    /// <summary>
    /// 来自角度
    /// </summary>
    CubismPhysicsSource_Angle,
}

/// <summary>
/// 物理演算使用的外部力.
/// </summary>
public record PhysicsJsonEffectiveForces
{
    /// <summary>
    /// 重力
    /// </summary>
    public Vector2 Gravity;
    /// <summary>
    /// 风力
    /// </summary>
    public Vector2 Wind;
};

/// <summary>
/// 物理演算的参数信息.
/// </summary>
public record CubismPhysicsParameter
{
    /// <summary>
    /// 参数 ID
    /// </summary>
    public required string Id;
    /// <summary>
    /// 适用目标种类
    /// </summary>
    public CubismPhysicsTargetType TargetType;
};

/// <summary>
/// 物理演算的归一化信息.
/// </summary>
public record CubismPhysicsNormalization
{
    /// <summary>
    /// 最小值
    /// </summary>
    public float Minimum;
    /// <summary>
    /// 最大值
    /// </summary>
    public float Maximum;
    /// <summary>
    /// 默认值
    /// </summary>
    public float Default;
}

/// <summary>
/// 物理演算使用的质点信息.
/// </summary>
public record CubismPhysicsParticle
{
    /// <summary>
    /// 初始位置
    /// </summary>
    public Vector2 InitialPosition;
    /// <summary>
    /// 移动灵敏度
    /// </summary>
    public float Mobility;
    /// <summary>
    /// 延迟
    /// </summary>
    public float Delay;
    /// <summary>
    /// 加速度
    /// </summary>
    public float Acceleration;
    /// <summary>
    /// 距离
    /// </summary>
    public float Radius;
    /// <summary>
    /// 当前位置
    /// </summary>
    public Vector2 Position;
    /// <summary>
    /// 上一位置
    /// </summary>
    public Vector2 LastPosition;
    /// <summary>
    /// 上一帧重力
    /// </summary>
    public Vector2 LastGravity;
    /// <summary>
    /// 当前受力
    /// </summary>
    public Vector2 Force;
    /// <summary>
    /// 当前速度
    /// </summary>
    public Vector2 Velocity;
}

/// <summary>
/// 物理演算质点的管理.
/// </summary>
public record CubismPhysicsSubRig
{
    /// <summary>
    /// 输入数量
    /// </summary>
    public int InputCount;
    /// <summary>
    /// 输出数量
    /// </summary>
    public int OutputCount;
    /// <summary>
    /// 质点数量
    /// </summary>
    public int ParticleCount;
    /// <summary>
    /// 输入起始索引
    /// </summary>
    public int BaseInputIndex;
    /// <summary>
    /// 输出起始索引
    /// </summary>
    public int BaseOutputIndex;
    /// <summary>
    /// 质点起始索引
    /// </summary>
    public int BaseParticleIndex;
    /// <summary>
    /// 归一化位置
    /// </summary>
    public required CubismPhysicsNormalization NormalizationPosition;
    /// <summary>
    /// 归一化角度
    /// </summary>
    public required CubismPhysicsNormalization NormalizationAngle;
}

/// <summary>
/// 归一化参数取值函数的声明.
/// </summary>
/// <param name="targetTranslation">运算结果的移动值</param>
/// <param name="targetAngle">运算结果的角度</param>
/// <param name="value">参数值</param>
/// <param name="parameterMinimumValue">参数最小值</param>
/// <param name="parameterMaximumValue">参数最大值</param>
/// <param name="parameterDefaultValue">参数默认值</param>
/// <param name="normalizationPosition">归一化位置</param>
/// <param name="normalizationAngle">归一化角度</param>
/// <param name="isInverted">值是否被反转?</param>
/// <param name="weight">权重</param>
public delegate void NormalizedPhysicsParameterValueGetter(
    ref Vector2 targetTranslation,
    ref float targetAngle,
    float value,
    float parameterMinimumValue,
    float parameterMaximumValue,
    float parameterDefaultValue,
    CubismPhysicsNormalization normalizationPosition,
    CubismPhysicsNormalization normalizationAngle,
    bool isInverted,
    float weight
);

/// <summary>
/// 物理演算取值函数的声明.
/// </summary>
/// <param name="translation">移动值</param>
/// <param name="particles">质点列表</param>
/// <param name="particleIndex"></param>
/// <param name="isInverted">值是否被反转?</param>
/// <param name="parentGravity">父级重力</param>
/// <returns>值</returns>
public unsafe delegate float PhysicsValueGetter(
    Vector2 translation,
    CubismPhysicsParticle[] particles,
    int currentParticleIndex,
    int particleIndex,
    bool isInverted,
    Vector2 parentGravity
);

/// <summary>
/// 物理演算缩放系数取值函数的声明.
/// </summary>
/// <param name="translationScale">移动值的缩放系数</param>
/// <param name="angleScale">角度的缩放系数</param>
/// <returns>缩放系数值</returns>
public unsafe delegate float PhysicsScaleGetter(Vector2 translationScale, float angleScale);

/// <summary>
/// 物理演算的输入信息.
/// </summary>
public record CubismPhysicsInput
{
    /// <summary>
    /// 输入源参数
    /// </summary>
    public required CubismPhysicsParameter Source;
    /// <summary>
    /// 输入源参数的索引
    /// </summary>
    public int SourceParameterIndex;
    /// <summary>
    /// 权重
    /// </summary>
    public float Weight;
    /// <summary>
    /// 输入种类
    /// </summary>
    public CubismPhysicsSource Type;
    /// <summary>
    /// 值是否被反转
    /// </summary>
    public bool Reflect;
    /// <summary>
    /// 归一化参数值的取值函数
    /// </summary>
    public NormalizedPhysicsParameterValueGetter GetNormalizedParameterValue;
}

/// <summary>
/// 物理演算的输出信息.
/// </summary>
public record CubismPhysicsOutput
{
    /// <summary>
    /// 输出目标参数
    /// </summary>
    public required CubismPhysicsParameter Destination;
    /// <summary>
    /// 输出目标参数的索引
    /// </summary>
    public int DestinationParameterIndex;
    /// <summary>
    /// 摆的索引
    /// </summary>
    public int VertexIndex;
    /// <summary>
    /// 移动值的缩放系数
    /// </summary>
    public Vector2 TranslationScale;
    /// <summary>
    /// 角度的缩放系数
    /// </summary>
    public float AngleScale;
    /// <summary>
    /// 权重
    /// </summary>
    public float Weight;
    /// <summary>
    /// 输出种类
    /// </summary>
    public CubismPhysicsSource Type;
    /// <summary>
    /// 值是否被反转
    /// </summary>
    public bool Reflect;
    /// <summary>
    /// 低于最小值时使用的值
    /// </summary>
    public float ValueBelowMinimum;
    /// <summary>
    /// 超过最大值时使用的值
    /// </summary>
    public float ValueExceededMaximum;
    /// <summary>
    /// 物理演算的取值函数
    /// </summary>
    public PhysicsValueGetter GetValue;
    /// <summary>
    /// 物理演算的缩放系数取值函数
    /// </summary>
    public PhysicsScaleGetter GetScale;
}

/// <summary>
/// 物理演算的数据.
/// </summary>
public record CubismPhysicsRig
{
    /// <summary>
    /// 物理演算的质点数量
    /// </summary>
    public int SubRigCount;
    /// <summary>
    /// 物理演算质点管理的列表
    /// </summary>
    public required CubismPhysicsSubRig[] Settings;
    /// <summary>
    /// 物理演算的输入列表
    /// </summary>
    public required CubismPhysicsInput[] Inputs;
    /// <summary>
    /// 物理演算的输出列表
    /// </summary>
    public required CubismPhysicsOutput[] Outputs;
    /// <summary>
    /// 物理演算的质点列表
    /// </summary>
    public required CubismPhysicsParticle[] Particles;
    /// <summary>
    /// 重力
    /// </summary>
    public Vector2 Gravity;
    /// <summary>
    /// 风力
    /// </summary>
    public Vector2 Wind;
    /// <summary>
    /// 物理演算运行帧率(FPS)
    /// </summary>
    public float Fps;
}