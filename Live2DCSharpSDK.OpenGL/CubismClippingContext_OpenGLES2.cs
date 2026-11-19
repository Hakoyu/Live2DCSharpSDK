using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// OpenGL 用のクリッピングコンテキスト（バックエンド固有のフック）。
/// 現時点で GL 固有の状態は持たず、基底 <see cref="CubismClippingContext"/> にそのまま委譲する。
/// </summary>
/// <remarks>
/// v7/S6 補修: 主コンストラクタの <c>CubismModel model</c> 引数を削除した（CS9113 対応）。
/// ・官方 R5 にはバックエンド固有の CubismClippingContext_OpenGLES2 が存在せず（OpenGL は
///   Framework の CubismClippingContext を直接使う）、「官方と同じシグネチャ」という制約は無い。
/// ・基底のコンストラクタは (manager, clippingDrawableIndices, clipCount) のみを取り、
///   Vulkan 側の対称実装 CubismClippingContext_Vulkan も model を取らない。
/// ・引数はクラス本体で一切読まれておらず（コンストラクタ本体は空）、副作用も無いため、
///   削除しても clipping の挙動は変わらない。将来 model が必要になった時点で追加すればよい。
/// </remarks>
public unsafe class CubismClippingContext_OpenGLES2(CubismClippingManager manager,
    int* clippingDrawableIndices, int clipCount) : CubismClippingContext(manager, clippingDrawableIndices, clipCount)
{

}
