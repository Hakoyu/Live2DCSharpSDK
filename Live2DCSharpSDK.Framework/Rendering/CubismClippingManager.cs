using System.Numerics;
using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Type;

namespace Live2DCSharpSDK.Framework.Rendering;

public abstract class CubismClippingManager
{
    /// <summary>
    /// 后端的种类 (既有. 为了判别 OpenGL / Vulkan 而原样保留)
    /// </summary>
    public abstract RenderType RenderType { get; }
    /// <summary>
    /// 处理的对象类型(用于区分 Drawable / Offscreen 两个体系).
    /// 官方 R5 通过 Initialize 等参数每次传入, 但本地把 manager 分成 2 套
    /// (drawable 用 / offscreen 用), 因此用字段保持.
    /// 默认值为 Drawable (值 0). Offscreen 为值 2 (值 1 已在官方中删除).
    /// </summary>
    public CsmEnum.DrawableObjectType Type { get; set; } = CsmEnum.DrawableObjectType.Drawable;
    public unsafe abstract CubismClippingContext CreateClippingContext(CubismClippingManager manager,
        CubismModel model, int* clippingDrawableIndices, int clipCount);

    /// <summary>
    /// 实验时 1 个通道为 1, 只有 RGB 时为 3, 也包含 Alpha 时为 4
    /// </summary>
    public const int ColorChannelCount = 4;
    /// <summary>
    /// 通常每张帧缓冲的最大遮罩数
    /// </summary>
    public const int ClippingMaskMaxCountOnDefault = 36;
    /// <summary>
    /// 帧缓冲为 2 张以上时每张帧缓冲的最大遮罩数
    /// </summary>
    public const int ClippingMaskMaxCountOnMultiRenderTexture = 32;

    /// <summary>
    /// Offscreen 表面的地址 (官方 T_RenderTarget. R5 中改为 CubismRenderTarget)
    /// </summary>
    protected CubismRenderTarget CurrentMaskBuffer = null!;
    /// <summary>
    /// 遮罩清除标志的数组
    /// </summary>
    protected List<bool> ClearedMaskBufferFlags = [];

    protected List<CubismTextureColor> ChannelColors = [];
    /// <summary>
    /// 遮罩用裁剪上下文的列表
    /// </summary>
    protected List<CubismClippingContext> ClippingContextListForMask = [];
    /// <summary>
    /// 绘制用裁剪上下文的列表
    /// </summary>
    public List<CubismClippingContext?> ClippingContextListForDraw { get; init; } = [];
    /// <summary>
    /// Offscreen 绘制用裁剪上下文的列表
    /// (官方 _clippingContextListForOffscreen / GetClippingContextListForOffscreen)
    /// </summary>
    public List<CubismClippingContext?> ClippingContextListForOffscreen { get; init; } = [];
    /// <summary>
    /// 裁剪遮罩的缓冲区尺寸 (初始值:256)
    /// </summary>
    public Vector2 ClippingMaskBufferSize { get; private set; }
    /// <summary>
    /// 要生成的渲染纹理张数
    /// </summary>
    public int RenderTextureCount { get; private set; }

    /// <summary>
    /// 遮罩计算用的矩阵
    /// </summary>
    protected CubismMatrix44 TmpMatrix = new();
    /// <summary>
    /// 遮罩计算用的矩阵
    /// </summary>
    protected CubismMatrix44 TmpMatrixForMask = new();
    /// <summary>
    /// 遮罩计算用的矩阵
    /// </summary>
    protected CubismMatrix44 TmpMatrixForDraw = new();
    /// <summary>
    /// 遮罩配置计算用的矩形
    /// </summary>
    protected RectF TmpBoundsOnModel = new();

    public CubismClippingManager()
    {
        ClippingMaskBufferSize = new(256, 256);

        ChannelColors.Add(new(1.0f, 0f, 0f, 0f));
        ChannelColors.Add(new(0f, 1.0f, 0f, 0f));
        ChannelColors.Add(new(0f, 0f, 1.0f, 0f));
        ChannelColors.Add(new(0f, 0f, 0f, 1.0f));
    }

    public void Dispose()
    {
        ClippingContextListForDraw.Clear();
        ClippingContextListForOffscreen.Clear();
        ClippingContextListForMask.Clear();
        ChannelColors.Clear();
        ClearedMaskBufferFlags.Clear();

        // §2.F 漏点③: 官方 R5 会连遮罩用渲染目标一起销毁
        CurrentMaskBuffer?.DestroyRenderTarget();
        CurrentMaskBuffer = null!;
    }

    /// <summary>
    /// 管理器的初始化处理
    /// 注册使用裁剪遮罩的绘制对象
    /// </summary>
    /// <param name="model">模型的实例</param>
    /// <param name="maskBufferCount">缓冲区的生成数量</param>
    public unsafe void Initialize(CubismModel model, int maskBufferCount)
    {
        RenderTextureCount = maskBufferCount;

        // 设置渲染纹理的清除标志
        for (int i = 0; i < RenderTextureCount; ++i)
        {
            ClearedMaskBufferFlags.Add(false);
        }

        // 根据对象种类切换遮罩对象的列表 (官方 .tpp:91-106)
        int objectCount;
        int[] objectMaskCounts;
        unsafe
        {
            objectMaskCounts = Type == CsmEnum.DrawableObjectType.Offscreen
                ? ToArray(model.GetOffscreenMaskCounts(), model.GetOffscreenCount())
                : ToArray(model.GetDrawableMaskCounts(), model.GetDrawableCount());
        }

        List<CubismClippingContext?> objectClippingContextList = Type == CsmEnum.DrawableObjectType.Offscreen
            ? ClippingContextListForOffscreen
            : ClippingContextListForDraw;

        objectCount = objectMaskCounts.Length;

        // 注册所有使用裁剪遮罩的绘制对象
        // 裁剪遮罩通常限定在数个左右使用
        for (int i = 0; i < objectCount; i++)
        {
            if (objectMaskCounts[i] <= 0)
            {
                // 未使用裁剪遮罩的艺术网格 (多数情况下不使用)
                objectClippingContextList.Add(null);
                continue;
            }

            // 检查是否与已有的 ClipContext 相同
            CubismClippingContext? cc;
            unsafe
            {
                int* masks = Type == CsmEnum.DrawableObjectType.Offscreen
                    ? model.GetOffscreenMasks()[i]
                    : model.GetDrawableMasks()[i];
                cc = FindSameClip(masks, objectMaskCounts[i]);
                if (cc == null)
                {
                    // 如果不存在相同的遮罩则生成
                    cc = CreateClippingContext(this, model, masks, objectMaskCounts[i]);
                    ClippingContextListForMask.Add(cc);
                }
            }

            if (Type == CsmEnum.DrawableObjectType.Offscreen)
            {
                cc.AddClippedOffscreen(i);
            }
            else
            {
                cc.AddClippedDrawable(i);
            }

            objectClippingContextList.Add(cc);
        }
    }

    /// <summary>
    /// 把非托管数组复制到托管数组 (用于获取遮罩数量)
    /// </summary>
    private static unsafe int[] ToArray(int* source, int count)
    {
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = source[i];
        }
        return result;
    }

    /// <summary>
    /// 确认是否已经创建过遮罩.
    /// 如果已经创建, 则返回对应的裁剪遮罩实例.
    /// 如果未创建则返回 NULL
    /// </summary>
    /// <param name="drawableMasks">用于遮罩绘制对象的绘制对象列表</param>
    /// <param name="drawableMaskCounts">用于遮罩绘制对象的绘制对象数量</param>
    /// <returns>如果存在对应的裁剪遮罩则返回实例, 否则返回 NULL.</returns>
    private unsafe CubismClippingContext? FindSameClip(int* drawableMasks, int drawableMaskCounts)
    {
        // 确认是否与已创建的 ClippingContext 一致
        for (int i = 0; i < ClippingContextListForMask.Count; i++)
        {
            var cc = ClippingContextListForMask[i];
            int count = cc.ClippingIdCount;
            if (count != drawableMaskCounts) continue; // 个数不同则为不同对象
            int samecount = 0;

            // 确认是否持有相同的 ID. 由于数组数量相同, 如果一致的个数相同就认为持有相同的对象.
            for (int j = 0; j < count; j++)
            {
                int clipId = cc.ClippingIdList[j];
                for (int k = 0; k < count; k++)
                {
                    if (drawableMasks[k] == clipId)
                    {
                        samecount++;
                        break;
                    }
                }
            }
            if (samecount == count)
            {
                return cc;
            }
        }
        return null; // 未找到
    }

    /// <summary>
    /// 计算高精度遮罩处理用的矩阵 (向后兼容. Drawable 用)
    /// </summary>
    /// <param name="model">模型的实例</param>
    /// <param name="isRightHanded">处理是否为右手系</param>
    public void SetupMatrixForHighPrecision(CubismModel model, bool isRightHanded)
    {
        SetupMatrixForHighPrecision(model, isRightHanded, CsmEnum.DrawableObjectType.Drawable);
    }

    /// <summary>
    /// 计算高精度遮罩处理用的矩阵
    /// (官方 R5 .tpp:176-272. 带 drawableObjectType / mvp)
    /// </summary>
    /// <param name="model">模型的实例</param>
    /// <param name="isRightHanded">处理是否为右手系</param>
    /// <param name="drawableObjectType">要处理的对象类型</param>
    /// <param name="mvp">MVP 矩阵 (Offscreen 用)</param>
    public void SetupMatrixForHighPrecision(CubismModel model, bool isRightHanded,
        CsmEnum.DrawableObjectType drawableObjectType, CubismMatrix44? mvp = null)
    {
        // 准备所有的裁剪
        // 使用相同裁剪时(多个时合并为一个裁剪)只设置一次
        int usingClipCount = 0;
        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            // 关于 1 个裁剪遮罩
            var cc = ClippingContextListForMask[clipIndex];

            // 计算包围使用此裁剪的全部绘制对象的矩形
            CalcClippedTotalBounds(model, cc, drawableObjectType);

            if (cc.IsUsing)
            {
                usingClipCount++; // 作为使用中计数
            }
        }

        if (usingClipCount <= 0)
        {
            return;
        }
        // 遮罩矩阵创建处理
        SetupLayoutBounds(0);

        // 尺寸与渲染纹理张数不符时进行对齐
        if (ClearedMaskBufferFlags.Count != RenderTextureCount)
        {
            ClearedMaskBufferFlags.Clear();

            for (int i = 0; i < RenderTextureCount; ++i)
            {
                ClearedMaskBufferFlags.Add(false);
            }
        }
        else
        {
            // 每帧开始时初始化遮罩的清除标志
            for (int i = 0; i < RenderTextureCount; ++i)
            {
                ClearedMaskBufferFlags[i] = false;
            }
        }

        // 实际生成遮罩
        // 决定所有遮罩的布局绘制方式, 并记录到 ClipContext, ClippedDrawContext
        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            // --- 实际绘制 1 个遮罩 ---
            var clipContext = ClippingContextListForMask[clipIndex];
            var allClippedDrawRect = clipContext.AllClippedDrawRect; // 使用此遮罩的全部绘制对象在逻辑坐标上的包围矩形
            var layoutBoundsOnTex01 = clipContext.LayoutBounds; // 把遮罩容纳在此范围内
            float MARGIN = 0.05f;
            float scaleX;
            float scaleY;
            float ppu = model.GetPixelsPerUnit();
            float maskPixelWidth = clipContext.Manager.ClippingMaskBufferSize.X;
            float maskPixelHeight = clipContext.Manager.ClippingMaskBufferSize.Y;
            float physicalMaskWidth = layoutBoundsOnTex01.Width * maskPixelWidth;
            float physicalMaskHeight = layoutBoundsOnTex01.Height * maskPixelHeight;

            TmpBoundsOnModel.SetRect(allClippedDrawRect);
            if (TmpBoundsOnModel.Width * ppu > physicalMaskWidth)
            {
                TmpBoundsOnModel.Expand(allClippedDrawRect.Width * MARGIN, 0.0f);
                scaleX = layoutBoundsOnTex01.Width / TmpBoundsOnModel.Width;
            }
            else
            {
                scaleX = ppu / physicalMaskWidth;
            }

            if (TmpBoundsOnModel.Height * ppu > physicalMaskHeight)
            {
                TmpBoundsOnModel.Expand(0.0f, allClippedDrawRect.Height * MARGIN);
                scaleY = layoutBoundsOnTex01.Height / TmpBoundsOnModel.Height;
            }
            else
            {
                scaleY = ppu / physicalMaskHeight;
            }


            // 求出生成遮罩时使用的矩阵
            CreateMatrixForMask(isRightHanded, layoutBoundsOnTex01, scaleX, scaleY);

            clipContext.MatrixForMask.SetMatrix(TmpMatrixForMask.Tr);
            clipContext.MatrixForDraw.SetMatrix(TmpMatrixForDraw.Tr);

            if (drawableObjectType == CsmEnum.DrawableObjectType.Offscreen)
            {
                // clipContext * mvp^-1 (官方 .tpp:265-270)
                CubismMatrix44 invertMvp = mvp != null ? InvertMatrix44(mvp) : new CubismMatrix44();
                clipContext.MatrixForDraw.MultiplyByMatrix(invertMvp);
            }
        }
    }

    /// <summary>
    /// 求 4x4 矩阵的逆矩阵 (官方 CubismMatrix44::GetInvert 的 1:1 移植.
    /// 由于本地 CubismMatrix44 没有 GetInvert, 因此放在这里).
    /// </summary>
    private static CubismMatrix44 InvertMatrix44(CubismMatrix44 src)
    {
        float[] tr = src.Tr;
        float r00 = tr[0];
        float r10 = tr[1];
        float r20 = tr[2];
        float r01 = tr[4];
        float r11 = tr[5];
        float r21 = tr[6];
        float r02 = tr[8];
        float r12 = tr[9];
        float r22 = tr[10];

        float tx = tr[12];
        float ty = tr[13];
        float tz = tr[14];

        float det = r00 * (r11 * r22 - r12 * r21) -
                    r01 * (r10 * r22 - r12 * r20) +
                    r02 * (r10 * r21 - r11 * r20);

        CubismMatrix44 dst = new();

        if (MathF.Abs(det) < CubismMath.Epsilon)
        {
            dst.LoadIdentity();
            return dst;
        }

        float invDet = 1.0f / det;

        float inv00 = (r11 * r22 - r12 * r21) * invDet;
        float inv01 = -(r01 * r22 - r02 * r21) * invDet;
        float inv02 = (r01 * r12 - r02 * r11) * invDet;
        float inv10 = -(r10 * r22 - r12 * r20) * invDet;
        float inv11 = (r00 * r22 - r02 * r20) * invDet;
        float inv12 = -(r00 * r12 - r02 * r10) * invDet;
        float inv20 = (r10 * r21 - r11 * r20) * invDet;
        float inv21 = -(r00 * r21 - r01 * r20) * invDet;
        float inv22 = (r00 * r11 - r01 * r10) * invDet;

        float[] dstTr = dst.Tr;
        dstTr[0] = inv00;
        dstTr[1] = inv10;
        dstTr[2] = inv20;
        dstTr[3] = 0.0f;
        dstTr[4] = inv01;
        dstTr[5] = inv11;
        dstTr[6] = inv21;
        dstTr[7] = 0.0f;
        dstTr[8] = inv02;
        dstTr[9] = inv12;
        dstTr[10] = inv22;
        dstTr[11] = 0.0f;

        dstTr[12] = -(inv00 * tx + inv01 * ty + inv02 * tz);
        dstTr[13] = -(inv10 * tx + inv11 * ty + inv12 * tz);
        dstTr[14] = -(inv20 * tx + inv21 * ty + inv22 * tz);
        dstTr[15] = 1.0f;

        return dst;
    }

    /// <summary>
    /// 创建遮罩生成与绘制用的矩阵.
    /// </summary>
    /// <param name="isRightHanded">指定是否把坐标作为右手系处理</param>
    /// <param name="layoutBoundsOnTex01">容纳遮罩的区域</param>
    /// <param name="scaleX">绘制对象的伸缩率</param>
    /// <param name="scaleY">绘制对象的伸缩率</param>
    protected void CreateMatrixForMask(bool isRightHanded, RectF layoutBoundsOnTex01, float scaleX, float scaleY)
    {
        TmpMatrix.LoadIdentity();
        {
            // 把 Layout0..1 转换为 -1..1
            TmpMatrix.TranslateRelative(-1.0f, -1.0f);
            TmpMatrix.ScaleRelative(2.0f, 2.0f);
        }
        {
            // view to Layout0..1
            TmpMatrix.TranslateRelative(layoutBoundsOnTex01.X, layoutBoundsOnTex01.Y); //new = [translate]
            TmpMatrix.ScaleRelative(scaleX, scaleY); //new = [translate][scale]
            TmpMatrix.TranslateRelative(-TmpBoundsOnModel.X, -TmpBoundsOnModel.Y); //new = [translate][scale][translate]
        }
        // 计算结果在 tmpMatrixForMask 中
        TmpMatrixForMask.SetMatrix(TmpMatrix.Tr);

        TmpMatrix.LoadIdentity();
        {
            TmpMatrix.TranslateRelative(layoutBoundsOnTex01.X, layoutBoundsOnTex01.Y * (isRightHanded ? -1.0f : 1.0f)); //new = [translate]
            TmpMatrix.ScaleRelative(scaleX, scaleY * (isRightHanded ? -1.0f : 1.0f)); //new = [translate][scale]
            TmpMatrix.TranslateRelative(-TmpBoundsOnModel.X, -TmpBoundsOnModel.Y); //new = [translate][scale][translate]
        }

        TmpMatrixForDraw.SetMatrix(TmpMatrix.Tr);
    }

    /// <summary>
    /// 配置裁剪上下文的布局.
    /// 尽量用满一张渲染纹理来布局遮罩.
    /// 遮罩组数为 4 以下时在 RGBA 各通道各放一个遮罩, 5 以上 6 以下时按 2,2,1,1 分配 RGBA.
    /// </summary>
    /// <param name="usingClipCount">要配置的裁剪上下文数量</param>
    protected void SetupLayoutBounds(int usingClipCount)
    {
        int useClippingMaskMaxCount = RenderTextureCount <= 1
        ? ClippingMaskMaxCountOnDefault
        : ClippingMaskMaxCountOnMultiRenderTexture * RenderTextureCount;

        if (usingClipCount <= 0 || usingClipCount > useClippingMaskMaxCount)
        {
            if (usingClipCount > useClippingMaskMaxCount)
            {
                // 输出遮罩限制数量的警告
                // 官方 release 语义: 仅记录 + 断言 (CSM_ASSERT 在 release 下不生效), 仍继续渲染.
                // 本地的 CubismLog.Error 会抛异常导致加载中断, 因此这里降级为 Warning.
                int count = usingClipCount - useClippingMaskMaxCount;
                CubismLog.Warning("[Live2D SDK]not supported mask count : {0}\n[Details] render texture count: {1}\n, mask count : {2}"
                    , count, RenderTextureCount, usingClipCount);
            }

            // 这种情况下每次清除并使用一个遮罩目标
            for (int index = 0; index < ClippingContextListForMask.Count; index++)
            {
                CubismClippingContext cc = ClippingContextListForMask[index];
                cc.LayoutChannelIndex = 0; // 反正每次都会清除, 固定为 0 即可
                cc.LayoutBounds.X = 0.0f;
                cc.LayoutBounds.Y = 0.0f;
                cc.LayoutBounds.Width = 1.0f;
                cc.LayoutBounds.Height = 1.0f;
                cc.BufferIndex = 0;
            }
            return;
        }

        // 渲染纹理为 1 张时分割为 9 份 (最多 36 张)
        int layoutCountMaxValue = RenderTextureCount <= 1 ? 9 : 8;

        // 尽量用满一张 RenderTexture 来布局遮罩
        // 遮罩组数为 4 以下时在 RGBA 各通道各放一个遮罩, 5 以上 6 以下时按 2,2,1,1 分配 RGBA
        int countPerSheetDiv = (usingClipCount + RenderTextureCount - 1) / RenderTextureCount; // 每张渲染纹理分配多少张(向上取整)
        int reduceLayoutTextureCount = usingClipCount % RenderTextureCount; // 把布局数量减少一张的渲染纹理数量(仅该数量的渲染纹理为对象)

        // 按顺序使用 RGBA
        int divCount = countPerSheetDiv / ColorChannelCount; // 每个通道放置的基本遮罩个数
        int modCount = countPerSheetDiv % ColorChannelCount; // 余数, 分配到该编号的通道为止, 每个通道加一个

        // 逐个准备 RGBA 的各通道(0:R , 1:G , 2:B, 3:A, )
        int curClipIndex = 0; // 按顺序设置

        for (int renderTextureIndex = 0; renderTextureIndex < RenderTextureCount; renderTextureIndex++)
        {
            for (int channelIndex = 0; channelIndex < ColorChannelCount; channelIndex++)
            {
                // 在此通道中布局的数量
                // NOTE: 布局数 = 每个通道的基本遮罩数 + 若该通道放置余数遮罩则再加 1
                int layoutCount = divCount + (channelIndex < modCount ? 1 : 0);

                // 决定减少一张布局数量时执行该操作的目标通道
                // div 为 0 时调整到正常的索引范围内
                int checkChannelIndex = modCount + (divCount < 1 ? -1 : 0);

                // 本次为目标通道, 且存在减少一张布局数量的渲染纹理时
                if (channelIndex == checkChannelIndex && reduceLayoutTextureCount > 0)
                {
                    // 如果当前渲染纹理是目标渲染纹理, 则把布局数量减少一张
                    layoutCount -= !(renderTextureIndex < reduceLayoutTextureCount) ? 1 : 0;
                }

                // 决定分割方式
                if (layoutCount == 0)
                {
                    // 什么也不做
                }
                else if (layoutCount == 1)
                {
                    // 全部原样使用
                    var cc = ClippingContextListForMask[curClipIndex++];
                    cc.LayoutChannelIndex = channelIndex;
                    cc.LayoutBounds.X = 0.0f;
                    cc.LayoutBounds.Y = 0.0f;
                    cc.LayoutBounds.Width = 1.0f;
                    cc.LayoutBounds.Height = 1.0f;
                    cc.BufferIndex = renderTextureIndex;
                }
                else if (layoutCount == 2)
                {
                    for (int i = 0; i < layoutCount; i++)
                    {
                        int xpos = i % 2;

                        var cc = ClippingContextListForMask[curClipIndex++];
                        cc.LayoutChannelIndex = channelIndex;

                        cc.LayoutBounds.X = xpos * 0.5f;
                        cc.LayoutBounds.Y = 0.0f;
                        cc.LayoutBounds.Width = 0.5f;
                        cc.LayoutBounds.Height = 1.0f;
                        cc.BufferIndex = renderTextureIndex;
                        // 把 UV 分解成 2 个来使用
                    }
                }
                else if (layoutCount <= 4)
                {
                    // 分割成 4 份来使用
                    for (int i = 0; i < layoutCount; i++)
                    {
                        int xpos = i % 2;
                        int ypos = i / 2;

                        var cc = ClippingContextListForMask[curClipIndex++];
                        cc.LayoutChannelIndex = channelIndex;

                        cc.LayoutBounds.X = xpos * 0.5f;
                        cc.LayoutBounds.Y = ypos * 0.5f;
                        cc.LayoutBounds.Width = 0.5f;
                        cc.LayoutBounds.Height = 0.5f;
                        cc.BufferIndex = renderTextureIndex;
                    }
                }
                else if (layoutCount <= layoutCountMaxValue)
                {
                    // 分割成 9 份来使用
                    for (int i = 0; i < layoutCount; i++)
                    {
                        int xpos = i % 3;
                        int ypos = i / 3;

                        var cc = ClippingContextListForMask[curClipIndex++];
                        cc.LayoutChannelIndex = channelIndex;

                        cc.LayoutBounds.X = xpos / 3.0f;
                        cc.LayoutBounds.Y = ypos / 3.0f;
                        cc.LayoutBounds.Width = 1.0f / 3.0f;
                        cc.LayoutBounds.Height = 1.0f / 3.0f;
                        cc.BufferIndex = renderTextureIndex;
                    }
                }
                // 超过遮罩限制张数时的处理
                else
                {
                    int count = usingClipCount - useClippingMaskMaxCount;

                    // 开发模式下使其停止
                    throw new Exception($"not supported mask count : {count}\n[Details] render texture count: {RenderTextureCount}\n, mask count : {usingClipCount}");
                }
            }
        }
    }

    /// <summary>
    /// 计算包围被遮罩的全部绘制对象的矩形(模型坐标系)
    /// </summary>
    /// <param name="model">模型的实例</param>
    /// <param name="clippingContext">裁剪遮罩的上下文</param>
    protected unsafe void CalcClippedDrawTotalBounds(CubismModel model, CubismClippingContext clippingContext)
    {
        CalcClippedTotalBounds(model, clippingContext, CsmEnum.DrawableObjectType.Drawable);
    }

    /// <summary>
    /// 计算包围被遮罩的全部绘制对象的矩形(模型坐标系)
    /// (官方 R5 .tpp:466-592. 同时支持 Drawable / Offscreen)
    /// </summary>
    /// <param name="model">模型的实例</param>
    /// <param name="clippingContext">裁剪遮罩的上下文</param>
    /// <param name="drawableObjectType">要处理的对象类型</param>
    protected unsafe void CalcClippedTotalBounds(CubismModel model, CubismClippingContext clippingContext,
        CsmEnum.DrawableObjectType drawableObjectType)
    {
        // 被裁剪遮罩(被遮罩的绘制对象)整体的矩形
        float clippedDrawTotalMinX = float.MaxValue, clippedDrawTotalMinY = float.MaxValue;
        float clippedDrawTotalMaxX = float.MinValue, clippedDrawTotalMaxY = float.MinValue;

        // 判定此遮罩是否实际需要
        // 只要使用此裁剪的 "绘制对象" 有一个可用, 就需要生成遮罩

        int clippedDrawCount;
        List<int> clippedOffscreenChildDrawableIndexList = [];

        if (drawableObjectType == CsmEnum.DrawableObjectType.Offscreen)
        {
            int clippedOffscreenCount = clippingContext.ClippedOffscreenIndexList.Count;

            for (int clippedOffscreenIndex = 0; clippedOffscreenIndex < clippedOffscreenCount; ++clippedOffscreenIndex)
            {
                // 求出使用遮罩的绘制对象被绘制的矩形
                int offscreenIndex = clippingContext.ClippedOffscreenIndexList[clippedOffscreenIndex];
                CollectOffscreenChildDrawableIndexList(model, offscreenIndex, clippedOffscreenChildDrawableIndexList);
            }

            clippedDrawCount = clippedOffscreenChildDrawableIndexList.Count;
        }
        else
        {
            clippedDrawCount = clippingContext.ClippedDrawableIndexList.Count;
        }

        for (int clippedObjectIndex = 0; clippedObjectIndex < clippedDrawCount; ++clippedObjectIndex)
        {
            // 求出使用遮罩的绘制对象被绘制的矩形
            int drawableIndex = drawableObjectType == CsmEnum.DrawableObjectType.Offscreen
                ? clippedOffscreenChildDrawableIndexList[clippedObjectIndex]
                : clippingContext.ClippedDrawableIndexList[clippedObjectIndex];

            int drawableVertexCount = model.GetDrawableVertexCount(drawableIndex);
            var drawableVertexes = model.GetDrawableVertices(drawableIndex);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            int loop = drawableVertexCount * CubismFramework.VertexStep;
            for (int pi = CubismFramework.VertexOffset; pi < loop; pi += CubismFramework.VertexStep)
            {
                float x = drawableVertexes[pi];
                float y = drawableVertexes[pi + 1];
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            //
            if (minX == float.MaxValue) continue; // 没有取到任何有效点, 因此跳过

            // 反映到整体矩形
            if (minX < clippedDrawTotalMinX) clippedDrawTotalMinX = minX;
            if (minY < clippedDrawTotalMinY) clippedDrawTotalMinY = minY;
            if (maxX > clippedDrawTotalMaxX) clippedDrawTotalMaxX = maxX;
            if (maxY > clippedDrawTotalMaxY) clippedDrawTotalMaxY = maxY;
        }
        if (clippedDrawTotalMinX == float.MaxValue)
        {
            clippingContext.AllClippedDrawRect.X = 0.0f;
            clippingContext.AllClippedDrawRect.Y = 0.0f;
            clippingContext.AllClippedDrawRect.Width = 0.0f;
            clippingContext.AllClippedDrawRect.Height = 0.0f;
            clippingContext.IsUsing = false;
        }
        else
        {
            clippingContext.IsUsing = true;
            float w = clippedDrawTotalMaxX - clippedDrawTotalMinX;
            float h = clippedDrawTotalMaxY - clippedDrawTotalMinY;
            clippingContext.AllClippedDrawRect.X = clippedDrawTotalMinX;
            clippingContext.AllClippedDrawRect.Y = clippedDrawTotalMinY;
            clippingContext.AllClippedDrawRect.Width = w;
            clippingContext.AllClippedDrawRect.Height = h;
        }
    }

    /// <summary>
    /// 把与 offscreen 关联的 part 的子 drawable 存入目标容器
    /// (官方 .tpp:595-600)
    /// </summary>
    /// <param name="model">模型的实例</param>
    /// <param name="offscreenIndex">Offscreen 的索引</param>
    /// <param name="childDrawableIndexList">目标 drawable 的存放容器</param>
    public unsafe void CollectOffscreenChildDrawableIndexList(CubismModel model, int offscreenIndex, List<int> childDrawableIndexList)
    {
        // 获取父对象
        int ownerIndex = model.GetOffscreenOwnerIndices()[offscreenIndex];
        CollectPartChildDrawableIndexList(model, ownerIndex, childDrawableIndexList);
    }

    /// <summary>
    /// 把 part 的子 drawable 存入目标容器 (官方 .tpp:603-614)
    /// </summary>
    /// <param name="model">模型的实例</param>
    /// <param name="partIndex">部件的索引</param>
    /// <param name="childDrawableIndexList">目标 drawable 的存放容器</param>
    public void CollectPartChildDrawableIndexList(CubismModel model, int partIndex, List<int> childDrawableIndexList)
    {
        PartChildDrawObjects childDrawObjects = model.GetPartsHierarchy()[partIndex].ChildDrawObjects;
        for (int i = 0; i < childDrawObjects.DrawableIndices.Count; ++i)
        {
            childDrawableIndexList.Add(childDrawObjects.DrawableIndices[i]);
        }
        for (int i = 0; i < childDrawObjects.OffscreenIndices.Count; ++i)
        {
            CollectOffscreenChildDrawableIndexList(model, childDrawObjects.OffscreenIndices[i], childDrawableIndexList);
        }
    }

    /// <summary>
    /// 获取颜色通道(RGBA)的标志
    /// </summary>
    /// <param name="channelNo">颜色通道(RGBA)的编号(0:R , 1:G , 2:B, 3:A)</param>
    /// <returns></returns>
    public CubismTextureColor GetChannelFlagAsColor(int channelNo)
    {
        return ChannelColors[channelNo];
    }

    /// <summary>
    /// 设置裁剪遮罩缓冲区的尺寸
    /// </summary>
    /// <param name="width">裁剪遮罩缓冲区的尺寸</param>
    /// <param name="height">裁剪遮罩缓冲区的尺寸</param>
    public void SetClippingMaskBufferSize(float width, float height)
    {
        ClippingMaskBufferSize = new Vector2(width, height);
    }
}
