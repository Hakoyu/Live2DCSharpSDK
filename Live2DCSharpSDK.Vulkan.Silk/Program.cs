using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Vulkan;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace Live2DCSharpSDK.Silk.Vulkan;

internal class Program : VulkanApi
{
    private IWindow window;
    private Vk vk;

    /// <summary>
    /// 本次运行使用的 Cubism 上下文, 由 Main 创建后传入, 退出路径统一释放.
    /// </summary>
    private readonly CubismContext _context;

    public Program(CubismContext context)
    {
        _context = context;
    }

    static void Main(string[] args)
    {
        var cubismAllocator = new LAppAllocator();
        var cubismOption = new CubismOption()
        {
            LogFunction = Console.WriteLine,
            LoggingLevel = LAppDefine.CubismLoggingLevel,
        };
        // 上下文在窗口/模型创建前启动.
        var cubismContext = CubismContext.Create(cubismAllocator, cubismOption);
        cubismContext.StartUp();

        new Program(cubismContext).InitWindow();
    }

    public override unsafe VkNonDispatchableHandle CreateSurface<T>(VkHandle handle, T* handel1)
    {
        return window.VkSurface!.Create(handle, handel1);
    }

    public override unsafe byte** GetRequiredExtensions(out uint count)
    {
        return window.VkSurface!.GetRequiredExtensions(out count);
    }

    public override void GetWindowSize(out int width, out int height)
    {
        width = window.FramebufferSize.X;
        height = window.FramebufferSize.Y;
    }

    private void InitWindow()
    {
        //Create a window.
        var options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(400, 400),
            Title = "Vulkan",
        };

        window = Window.Create(options);
        window.Initialize();

        if (window.VkSurface is null)
        {
            throw new Exception("Windowing platform doesn't support Vulkan.");
        }
        vk = Vk.GetApi();

        LAppDelegateVulkan? live2d = null;
        try
        {
            var app = new LAppDelegateVulkan(this)
            {
                BGColor = new(0, 1, 0, 1),
                // 上下文交给 delegate 管理, Shutdown 时由 delegate 统一释放.
                Context = _context,
            };
            live2d = app;

            // 模型路径不硬编码, 而由 App 层的 ModelResourceLocator 解析
            // (与 Avalonia / OpenTK / WPF 三个宿主相同的规则:
            //   环境变量 LIVE2D_MODEL_DIR / LIVE2D_MODEL_NAME 优先 → 从可执行文件位置向上层目录
            //   相对查找官方 SDK 包的 CubismSdkForNative-5-r.5/Samples/Resources
            //   → 可执行文件旁的 Resources).
            // 未找到时也不抛异常, 以空场景继续启动.
            LAppModel? model = null;
            if (ModelResourceLocator.TryResolve(null, out string modelDir, out string modelName))
            {
                try
                {
                    model = app.Live2dManager.LoadModel(modelDir, modelName);
                    Console.WriteLine(
                        $"[Live2D App]model loaded: {Path.Combine(modelDir, modelName)}"
                    );
                }
                catch (Exception e)
                {
                    Console.WriteLine(
                        $"[Live2D App]model load failed: {e.GetType().Name}: {e.Message}"
                    );
                }
            }
            else
            {
                Console.WriteLine($"[Live2D App]未检测到模型, 以空场景启动: {modelName}");
            }

            window.Resize += (size) =>
            {
                app.Resize();
            };

            // [仅验收用] V-d(Ren offscreen 探针): 默认关闭, 仅当 L2D_PROBE_OFFSCREEN=1 时启用.
            // 30 帧后输出 offscreen 数量与 DrawOffscreen 次数并退出.
            // 输出格式固定, 与既有的验收流程保持一致.
            int probeFrames = 0;
            bool probeOffscreen = Environment.GetEnvironmentVariable("L2D_PROBE_OFFSCREEN") == "1";
            window.Render += (time) =>
            {
                app.Run((float)time);

                if (probeOffscreen && ++probeFrames >= 30)
                {
                    var renderer = model?.Renderer as CubismRenderer_Vulkan;
                    Console.WriteLine(
                        $"OFFSCREEN_PROBE offscreens={renderer?.OffscreenCount} draws={renderer?.OffscreenDrawCount}"
                    );
                    window.Close();
                }
            };

            window.Run();
        }
        finally
        {
            // 退出路径: 先关停 live2d(模型 -> 渲染器 -> 纹理 -> 设备/实例), 再销毁窗口与 Framework.
            // 顺序与 OpenGL 宿主一致, 且各层均为幂等实现, 不会重复释放.
            live2d?.Shutdown();
            window.Dispose();
            // 防御: delegate 未创建时也释放上下文(Dispose 幂等, 已释放时为空操作).
            _context.Dispose();
        }
    }
}
