using System.Numerics;
using System.Runtime.InteropServices;

namespace Live2DCSharpSDK.Framework.Core;

public static class CsmEnum
{
    //Alignment constraints.

    /// <summary>
    /// Necessary alignment for mocs (in bytes).
    /// </summary>
    public const int csmAlignofMoc = 64;
    /// <summary>
    /// Necessary alignment for models (in bytes).
    /// </summary>
    public const int CsmAlignofModel = 16;

    //Bit masks for non-dynamic drawable flags.

    /// <summary>
    /// Additive blend mode mask.
    /// </summary>
    public const byte CsmBlendAdditive = 1 << 0;
    /// <summary>
    /// Multiplicative blend mode mask.
    /// </summary>
    public const byte CsmBlendMultiplicative = 1 << 1;
    /// <summary>
    /// Double-sidedness mask.
    /// </summary>
    public const byte CsmIsDoubleSided = 1 << 2;
    /// <summary>
    /// Clipping mask inversion mode mask.
    /// </summary>
    public const byte CsmIsInvertedMask = 1 << 3;

    //Bit masks for dynamic drawable flags.

    /// <summary>
    /// Flag set when visible.
    /// </summary>
    public const byte CsmIsVisible = 1 << 0;
    /// <summary>
    /// Flag set when visibility did change.
    /// </summary>
    public const byte CsmVisibilityDidChange = 1 << 1;
    /// <summary>
    /// Flag set when opacity did change.
    /// </summary>
    public const byte CsmOpacityDidChange = 1 << 2;
    /// <summary>
    /// Flag set when draw order did change.
    /// </summary>
    public const byte CsmDrawOrderDidChange = 1 << 3;
    /// <summary>
    /// Flag set when render order did change.
    /// </summary>
    public const byte CsmRenderOrderDidChange = 1 << 4;
    /// <summary>
    /// Flag set when vertex positions did change.
    /// </summary>
    public const byte CsmVertexPositionsDidChange = 1 << 5;
    /// <summary>
    /// Flag set when blend color did change.
    /// </summary>
    public const byte CsmBlendColorDidChange = 1 << 6;

    //moc3 file format version.

    /// <summary>
    /// unknown
    /// </summary>
    public const int CsmMocVersion_Unknown = 0;
    /// <summary>
    /// moc3 file version 3.0.00 - 3.2.07
    /// </summary>
    public const int CsmMocVersion_30 = 1;
    /// <summary>
    /// moc3 file version 3.3.00 - 3.3.03
    /// </summary>
    public const int CsmMocVersion_33 = 2;
    /// <summary>
    /// moc3 file version 4.0.00 - 4.1.05
    /// </summary>
    public const int CsmMocVersion_40 = 3;
    /// <summary>
    /// moc3 file version 4.2.00 - 4.2.04
    /// </summary>
    public const int CsmMocVersion_42 = 4;
    /// <summary>
    /// moc3 file version 5.0.00 - 5.2.03
    /// </summary>
    public const int CsmMocVersion_50 = 5;
    /// <summary>
    /// moc3 file version 5.3.00 -
    /// </summary>
    public const int CsmMocVersion_53 = 6;

    //Parameter types.

    /// <summary>
    /// Normal parameter.
    /// </summary>
    public const int CsmParameterType_Normal = 0;
    /// <summary>
    /// Parameter for blend shape.
    /// </summary>
    public const int CsmParameterType_BlendShape = 1;

    //Color blend types (Live2DCubismCore.h:118-160).

    /// <summary>
    /// Normal blend.
    /// </summary>
    public const int CsmColorBlendType_Normal = 0;
    /// <summary>
    /// Add compatible blend. For version 5.2 and earlier.
    /// </summary>
    public const int CsmColorBlendType_AddCompatible = 1;
    /// <summary>
    /// Multiply compatible blend. For version 5.2 and earlier.
    /// </summary>
    public const int CsmColorBlendType_MultiplyCompatible = 2;
    /// <summary>
    /// Add blend.
    /// </summary>
    public const int CsmColorBlendType_Add = 3;
    /// <summary>
    /// AddGlow blend.
    /// </summary>
    public const int CsmColorBlendType_AddGlow = 4;
    /// <summary>
    /// Darken blend.
    /// </summary>
    public const int CsmColorBlendType_Darken = 5;
    /// <summary>
    /// Multiply blend.
    /// </summary>
    public const int CsmColorBlendType_Multiply = 6;
    /// <summary>
    /// ColorBurn blend.
    /// </summary>
    public const int CsmColorBlendType_ColorBurn = 7;
    /// <summary>
    /// LinearBurn blend.
    /// </summary>
    public const int CsmColorBlendType_LinearBurn = 8;
    /// <summary>
    /// Lighten blend.
    /// </summary>
    public const int CsmColorBlendType_Lighten = 9;
    /// <summary>
    /// Screen blend.
    /// </summary>
    public const int CsmColorBlendType_Screen = 10;
    /// <summary>
    /// ColorDodge blend.
    /// </summary>
    public const int CsmColorBlendType_ColorDodge = 11;
    /// <summary>
    /// Overlay blend.
    /// </summary>
    public const int CsmColorBlendType_Overlay = 12;
    /// <summary>
    /// SoftLight blend.
    /// </summary>
    public const int CsmColorBlendType_SoftLight = 13;
    /// <summary>
    /// HardLight blend.
    /// </summary>
    public const int CsmColorBlendType_HardLight = 14;
    /// <summary>
    /// LinearLight blend.
    /// </summary>
    public const int CsmColorBlendType_LinearLight = 15;
    /// <summary>
    /// Hue blend.
    /// </summary>
    public const int CsmColorBlendType_Hue = 16;
    /// <summary>
    /// Color blend.
    /// </summary>
    public const int CsmColorBlendType_Color = 17;

    //Alpha blend types (Live2DCubismCore.h:162-175).

    /// <summary>
    /// Over blend.
    /// </summary>
    public const int CsmAlphaBlendType_Over = 0;
    /// <summary>
    /// Atop blend.
    /// </summary>
    public const int CsmAlphaBlendType_Atop = 1;
    /// <summary>
    /// Out blend.
    /// </summary>
    public const int CsmAlphaBlendType_Out = 2;
    /// <summary>
    /// ConjointOver blend.
    /// </summary>
    public const int CsmAlphaBlendType_ConjointOver = 3;
    /// <summary>
    /// DisjointOver blend.
    /// </summary>
    public const int CsmAlphaBlendType_DisjointOver = 4;

    //Rendering object types.
    //官方来源是 Framework/src/Rendering/CubismRenderer.hpp:33-37（非 Core C API）。
    //Cubism 5-r.5 只保留 Drawable=0 / Offscreen=2，值 1 已被官方删除，不要补（PLAN D-15）。

    /// <summary>
    /// Type of rendering object.
    /// </summary>
    public enum DrawableObjectType
    {
        /// <summary>
        /// Drawable object.
        /// </summary>
        Drawable = 0,
        /// <summary>
        /// Offscreen object.
        /// </summary>
        Offscreen = 2
    }
}

/// <summary>
/// Log handler.
/// </summary>
/// <param name="message">Null-terminated string message to log.</param>
public delegate void LogFunction(string message);

public static partial class CubismCore
{
    //VERSION

    public static uint Version()
    {
        return GetVersion();
    }

    /// <summary>
    /// Queries Core version.
    /// </summary>
    /// <returns>Core version.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetVersion")]
    internal static partial uint GetVersion();

    /// <summary>
    /// Gets Moc file supported latest version.
    /// </summary>
    /// <returns>csmMocVersion (Moc file latest format version).</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetLatestMocVersion")]
    internal static partial uint GetLatestMocVersion();

    /// <summary>
    /// Gets Moc file format version.
    /// </summary>
    /// <param name="address">Address of moc.</param>
    /// <param name="size">Size of moc (in bytes).</param>
    /// <returns>csmMocVersion</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetMocVersion")]
    internal static partial uint GetMocVersion(IntPtr address, int size);

    //CONSISTENCY

    /// <summary>
    /// Checks consistency of a moc.
    /// </summary>
    /// <param name="address">Address of unrevived moc. The address must be aligned to 'csmAlignofMoc'.</param>
    /// <param name="size">Size of moc (in bytes).</param>
    /// <returns>'1' if Moc is valid; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmHasMocConsistency")]
    [return: MarshalAs(UnmanagedType.I4)]
    internal static partial bool HasMocConsistency(IntPtr address, int size);

    //LOGGING

    /// <summary>
    /// Queries log handler.
    /// </summary>
    /// <returns>Log handler.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetLogFunction")]
    internal static partial LogFunction GetLogFunction();

    /// <summary>
    /// Sets log handler.
    /// </summary>
    /// <param name="handler">Handler to use.</param>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmSetLogFunction")]
    internal static partial void SetLogFunction(LogFunction handler);

    //MOC

    /// <summary>
    /// Tries to revive a moc from bytes in place.
    /// </summary>
    /// <param name="address">Address of unrevived moc. The address must be aligned to 'csmAlignofMoc'.</param>
    /// <param name="size">Size of moc (in bytes).</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmReviveMocInPlace")]
    internal static partial IntPtr ReviveMocInPlace(IntPtr address, int size);

    //MODEL

    /// <summary>
    /// Queries size of a model in bytes.
    /// </summary>
    /// <param name="moc">Moc to query.</param>
    /// <returns>Valid size on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetSizeofModel")]
    internal static partial int GetSizeofModel(IntPtr moc);

    /// <summary>
    /// Tries to instantiate a model in place.
    /// </summary>
    /// <param name="moc">Source moc.</param>
    /// <param name="address">Address to place instance at. Address must be aligned to 'csmAlignofModel'.</param>
    /// <param name="size">Size of memory block for instance (in bytes).</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmInitializeModelInPlace")]
    internal static partial IntPtr InitializeModelInPlace(IntPtr moc, IntPtr address, int size);

    /// <summary>
    /// Updates a model.
    /// </summary>
    /// <param name="model">Model to update.</param>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmUpdateModel")]
    internal static partial void UpdateModel(IntPtr model);

    //CANVAS

    /// <summary>
    /// Reads info on a model canvas.
    /// </summary>
    /// <param name="model">Model query.</param>
    /// <param name="outSizeInPixels">Canvas dimensions.</param>
    /// <param name="outOriginInPixels">Origin of model on canvas.</param>
    /// <param name="outPixelsPerUnit">Aspect used for scaling pixels to units.</param>
    [DllImport("Live2DCubismCore", EntryPoint = "csmReadCanvasInfo")]
#pragma warning disable SYSLIB1054 // 使用 “LibraryImportAttribute” 而不是 “DllImportAttribute” 在编译时生成 P/Invoke 封送代码
    internal extern static void ReadCanvasInfo(IntPtr model, out Vector2 outSizeInPixels,
#pragma warning restore SYSLIB1054 // 使用 “LibraryImportAttribute” 而不是 “DllImportAttribute” 在编译时生成 P/Invoke 封送代码
        out Vector2 outOriginInPixels, out float outPixelsPerUnit);

    //PARAMETERS

    /// <summary>
    /// Gets number of parameters.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid count on success; '-1' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetParameterCount")]
    internal static partial int GetParameterCount(IntPtr model);

    /// <summary>
    /// Gets parameter IDs.
    /// All IDs are null-terminated ANSI strings.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetParameterIds")]
    internal static unsafe partial sbyte** GetParameterIds(IntPtr model);

    /// <summary>
    /// Gets parameter types.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetParameterTypes")]
    internal static unsafe partial int* GetParameterTypes(IntPtr model);

    /// <summary>
    /// Gets minimum parameter values.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetParameterMinimumValues")]
    internal static unsafe partial float* GetParameterMinimumValues(IntPtr model);

    /// <summary>
    /// Gets maximum parameter values.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetParameterMaximumValues")]
    internal static unsafe partial float* GetParameterMaximumValues(IntPtr model);

    /// <summary>
    /// Gets default parameter values.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetParameterDefaultValues")]
    internal static unsafe partial float* GetParameterDefaultValues(IntPtr model);

    /// <summary>
    /// Gets read/write parameter values buffer.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetParameterValues")]
    internal static unsafe partial float* GetParameterValues(IntPtr model);

    /// <summary>
    /// Gets Parameter Repeat informations.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetParameterRepeats")]
    internal static unsafe partial int* GetParameterRepeats(IntPtr model);

    /// <summary>
    /// Gets number of key values of each parameter.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetParameterKeyCounts")]
    internal static unsafe partial int* GetParameterKeyCounts(IntPtr model);

    /// <summary>
    /// Gets key values of each parameter.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetParameterKeyValues")]
    internal static unsafe partial float** GetParameterKeyValues(IntPtr model);

    //PARTS

    /// <summary>
    /// Gets number of parts.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid count on success; '-1' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetPartCount")]
    internal static partial int GetPartCount(IntPtr model);

    /// <summary>
    /// Gets parts IDs.
    /// All IDs are null-terminated ANSI strings.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetPartIds")]
    internal static unsafe partial sbyte** GetPartIds(IntPtr model);

    /// <summary>
    /// Gets read/write part opacities buffer.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetPartOpacities")]
    internal static unsafe partial float* GetPartOpacities(IntPtr model);

    /// <summary>
    /// Gets part's parent part indices.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetPartParentPartIndices")]
    internal static unsafe partial int* GetPartParentPartIndices(IntPtr model);

    /// <summary>
    /// Gets part's offscreen indices.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetPartOffscreenIndices")]
    internal static unsafe partial int* GetPartOffscreenIndices(IntPtr model);

    //DRAWABLES

    /// <summary>
    /// Gets number of drawables.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid count on success; '-1' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableCount")]
    internal static partial int GetDrawableCount(IntPtr model);

    /// <summary>
    /// Gets drawable IDs.
    /// All IDs are null-terminated ANSI strings.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableIds")]
    internal static unsafe partial sbyte** GetDrawableIds(IntPtr model);

    /// <summary>
    /// Gets constant drawable flags.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableConstantFlags")]
    internal static unsafe partial byte* GetDrawableConstantFlags(IntPtr model);

    /// <summary>
    /// Gets dynamic drawable flags.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableDynamicFlags")]
    internal static unsafe partial byte* GetDrawableDynamicFlags(IntPtr model);

    /// <summary>
    /// Gets drawable blend modes.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableBlendModes")]
    internal static unsafe partial int* GetDrawableBlendModes(IntPtr model);

    /// <summary>
    /// Gets drawable texture indices.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableTextureIndices")]
    internal static unsafe partial int* GetDrawableTextureIndices(IntPtr model);

    /// <summary>
    /// Gets drawable draw orders.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableDrawOrders")]
    internal static unsafe partial int* GetDrawableDrawOrders(IntPtr model);

    /// <summary>
    /// Gets model draw orders.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    /// <remarks>
    /// R5 改名：R1 的 csmGetDrawableRenderOrders 在 5-r.5 已从 Core 头文件中删除，
    /// 新入口点为 csmGetRenderOrders（Live2DCubismCore.h:332）。EntryPoint 是字符串，
    /// 改错不会编译报错，但运行期首次调用会抛 EntryPointNotFoundException。
    /// </remarks>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetRenderOrders")]
    internal static unsafe partial int* GetRenderOrders(IntPtr model);

    /// <summary>
    /// Gets drawable opacities.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableOpacities")]
    internal static unsafe partial float* GetDrawableOpacities(IntPtr model);

    /// <summary>
    /// Gets numbers of masks of each drawable.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableMaskCounts")]
    internal static unsafe partial int* GetDrawableMaskCounts(IntPtr model);

    /// <summary>
    /// Gets number of vertices of each drawable.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableMasks")]
    internal static unsafe partial int** GetDrawableMasks(IntPtr model);

    /// <summary>
    /// Gets number of vertices of each drawable.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableVertexCounts")]
    internal static unsafe partial int* GetDrawableVertexCounts(IntPtr model);

    /// <summary>
    /// Gets vertex position data of each drawable.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; a null pointer otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableVertexPositions")]
    internal static unsafe partial Vector2** GetDrawableVertexPositions(IntPtr model);

    /// <summary>
    /// Gets texture coordinate data of each drawables.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableVertexUvs")]
    internal static unsafe partial Vector2** GetDrawableVertexUvs(IntPtr model);

    /// <summary>
    /// Gets number of triangle indices for each drawable.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableIndexCounts")]
    internal static unsafe partial int* GetDrawableIndexCounts(IntPtr model);

    /// <summary>
    /// Gets triangle index data for each drawable.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableIndices")]
    internal static unsafe partial ushort** GetDrawableIndices(IntPtr model);

    /// <summary>
    /// Gets multiply color data for each drawable.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableMultiplyColors")]
    internal static unsafe partial Vector4* GetDrawableMultiplyColors(IntPtr model);

    /// <summary>
    /// Gets screen color data for each drawable.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableScreenColors")]
    internal static unsafe partial Vector4* GetDrawableScreenColors(IntPtr model);

    /// <summary>
    /// Gets drawable's parent part indices.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetDrawableParentPartIndices")]
    internal static unsafe partial int* GetDrawableParentPartIndices(IntPtr model);

    /// <summary>
    /// Resets all dynamic drawable flags.
    /// </summary>
    /// <param name="model">Model containing flags.</param>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmResetDrawableDynamicFlags")]
    internal static unsafe partial void ResetDrawableDynamicFlags(IntPtr model);

    //OFFSCREENS（R5 新增，Live2DCubismCore.h:675-756）

    /// <summary>
    /// Gets number of offscreens.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid count on success; '-1' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetOffscreenCount")]
    internal static partial int GetOffscreenCount(IntPtr model);

    /// <summary>
    /// Gets offscreen blend modes.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetOffscreenBlendModes")]
    internal static unsafe partial int* GetOffscreenBlendModes(IntPtr model);

    /// <summary>
    /// Gets offscreen opacities.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetOffscreenOpacities")]
    internal static unsafe partial float* GetOffscreenOpacities(IntPtr model);

    /// <summary>
    /// Gets offscreen owner indices.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetOffscreenOwnerIndices")]
    internal static unsafe partial int* GetOffscreenOwnerIndices(IntPtr model);

    /// <summary>
    /// Gets multiply color data for each offscreen.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetOffscreenMultiplyColors")]
    internal static unsafe partial Vector4* GetOffscreenMultiplyColors(IntPtr model);

    /// <summary>
    /// Gets screen color data for each offscreen.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetOffscreenScreenColors")]
    internal static unsafe partial Vector4* GetOffscreenScreenColors(IntPtr model);

    /// <summary>
    /// Gets numbers of masks of each offscreen.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetOffscreenMaskCounts")]
    internal static unsafe partial int* GetOffscreenMaskCounts(IntPtr model);

    /// <summary>
    /// Gets mask indices of each offscreen.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetOffscreenMasks")]
    internal static unsafe partial int** GetOffscreenMasks(IntPtr model);

    /// <summary>
    /// Gets constant offscreen flags.
    /// </summary>
    /// <param name="model">Model to query.</param>
    /// <returns>Valid pointer on success; '0' otherwise.</returns>
    [LibraryImport("Live2DCubismCore", EntryPoint = "csmGetOffscreenConstantFlags")]
    internal static unsafe partial byte* GetOffscreenConstantFlags(IntPtr model);
}