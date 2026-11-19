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

        // モデルのパスはハードコードせず、App 層の ModelResourceLocator で解決する
        // （Avalonia / OpenTK / WPF の三宿主と同じ規則:
        //   環境変数 LIVE2D_MODEL_DIR / LIVE2D_MODEL_NAME を最優先 → 実行ファイル位置から親方向へ
        //   公式 SDK パッケージの CubismSdkForNative-5-r.5/Samples/Resources を相対探索
        //   → 実行ファイル隣の Resources）。
        // 見つからない場合も例外にせず、空のシーンで起動を続ける。
        LAppModel? model = null;
        if (ModelResourceLocator.TryResolve(null, out string modelDir, out string modelName))
        {
            try
            {
                model = live2d.Live2dManager.LoadModel(modelDir, modelName);
                Console.WriteLine($"[Live2D App]model loaded: {Path.Combine(modelDir, modelName)}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Live2D App]model load failed: {e.GetType().Name}: {e.Message}");
            }
        }
        else
        {
            Console.WriteLine($"[Live2D App]モデル未検出のため空のシーンで起動します: {modelName}");
        }

        window.Resize += (size) =>
        {
            live2d.Resize();
        };

        // 【仅验收用】V-d（Ren offscreen 探针）: 默认关闭，仅当 L2D_PROBE_OFFSCREEN=1 时启用。
        // 30 帧後に offscreen 数と DrawOffscreen 回数を出力して終了する。
        // 出力書式は既存の受け入れ手順（t16 / V-d）に合わせて固定している。
        int probeFrames = 0;
        bool probeOffscreen = Environment.GetEnvironmentVariable("L2D_PROBE_OFFSCREEN") == "1";
        window.Render += (time) =>
        {
            live2d.Run((float)time);

            if (probeOffscreen && ++probeFrames >= 30)
            {
                var renderer = model?.Renderer as CubismRenderer_Vulkan;
                Console.WriteLine($"OFFSCREEN_PROBE offscreens={renderer?.OffscreenCount} draws={renderer?.OffscreenDrawCount}");
                window.Close();
            }
        };
        window.Run();
    }
}
