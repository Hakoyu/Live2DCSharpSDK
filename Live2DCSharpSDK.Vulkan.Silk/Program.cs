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

    static void Main(string[] args)
    {
        var cubismAllocator = new LAppAllocator();
        var cubismOption = new CubismOption()
        {
            LogFunction = Console.WriteLine,
            LoggingLevel = LAppDefine.CubismLoggingLevel
        };
        CubismFramework.StartUp(cubismAllocator, cubismOption);

        new Program().InitWindow();
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
        var live2d = new LAppDelegateVulkan(this)
        {
            BGColor = new(0, 1, 0, 1)
        };

        // P8c 启动验收：默认使用官方 5-r.5 包内的 Haru（原 F:\live2d\Resources 路径在本机不存在）。
        // 环境变量 L2D_MODEL_DIR / L2D_MODEL_NAME 可切换验收模型（tester 用，不改变默认行为）。
        string modelDir = Environment.GetEnvironmentVariable("L2D_MODEL_DIR")
            ?? "D:\\AgentGameWorkSpace\\CubismSdkForNative-5-r.5\\Samples\\Resources\\Haru\\";
        string modelName = Environment.GetEnvironmentVariable("L2D_MODEL_NAME") ?? "Haru";
        var model = live2d.Live2dManager.LoadModel(modelDir, modelName);

        window.Resize += (size) =>
        {
            live2d.Resize();
        };
        // t16 検証用: 30 フレーム後に offscreen 数と DrawOffscreen 回数を出力して終了する
        int probeFrames = 0;
        bool probeOffscreen = Environment.GetEnvironmentVariable("L2D_PROBE_OFFSCREEN") == "1";
        window.Render += (time) =>
        {
            live2d.Run((float)time);

            if (probeOffscreen && ++probeFrames >= 30)
            {
                var renderer = model.Renderer as CubismRenderer_Vulkan;
                Console.WriteLine($"OFFSCREEN_PROBE offscreens={renderer?.OffscreenCount} draws={renderer?.OffscreenDrawCount}");
                window.Close();
            }
        };
        window.Run();
    }
}
