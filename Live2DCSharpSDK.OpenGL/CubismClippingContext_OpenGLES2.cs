using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// OpenGL 用的 Clipping 上下文 (后端专用的钩子).
/// 目前不持有 GL 固有状态, 直接委托给基类 <see cref="CubismClippingContext"/>.
/// </summary>
/// <remarks>
/// 已删除主构造函数中的 <c>CubismModel model</c> 参数(用于消除 CS9113 警告).
/// - 官方 R5 中不存在后端固有的 CubismClippingContext_OpenGLES2 (OpenGL 是
///   直接使用 Framework 的 CubismClippingContext), 因此不存在 "与官方相同签名" 的约束.
/// - 基类构造函数只接收 (manager, clippingDrawableIndices, clipCount),
///   Vulkan 侧的对等实现 CubismClippingContext_Vulkan 也不接收 model.
/// - 该参数在类主体中完全未被读取 (构造函数体为空), 也没有副作用, 因此
///   即使删除也不会改变 clipping 的行为. 将来需要 model 时再添加即可.
/// </remarks>
public unsafe class CubismClippingContext_OpenGLES2(
    CubismClippingManager manager,
    int* clippingDrawableIndices,
    int clipCount
) : CubismClippingContext(manager, clippingDrawableIndices, clipCount) { }
