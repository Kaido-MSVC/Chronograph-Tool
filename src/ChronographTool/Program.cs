using Avalonia;
using ChronographTool.Services;

namespace ChronographTool;

internal static class Program
{
    internal static SingleInstance? Instance { get; private set; }

    // 在 AppMain 之前不要使用 Avalonia 或依賴 SynchronizationContext 的程式碼。
    [STAThread]
    public static int Main(string[] args)
    {
        using var instance = new SingleInstance();
        if (!instance.IsFirst)
        {
            // 已有實體在執行：請它把視窗帶到前景，避免兩個程式同時寫入 records.json。
            instance.ActivateExisting();
            return 0;
        }

        Instance = instance;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia 設定，IDE 預覽器也會使用。
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
