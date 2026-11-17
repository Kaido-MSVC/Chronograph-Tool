using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ChronographTool.Services;
using ChronographTool.ViewModels;
using ChronographTool.Views;

namespace ChronographTool;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var notices = new List<string>();

            var settings = new SettingsService(AppPaths.SettingsFile);
            var settingsResult = settings.Load();
            if (Describe("settings.json", settingsResult, "設定") is { } settingsNotice) notices.Add(settingsNotice);

            var records = new RecordRepository(AppPaths.RecordsFile);
            var recordsResult = records.Load();
            if (Describe("records.json", recordsResult, "紀錄") is { } recordsNotice) notices.Add(recordsNotice);

            var window = new MainWindow(settings.Current);
            var viewModel = new MainWindowViewModel(records, settings, window.UiServices, TimeProvider.System);
            foreach (var notice in notices) viewModel.Notify(notice);
            window.DataContext = viewModel;
            desktop.MainWindow = window;

            // Windows 登出或關機時，視窗不一定會收到 Closing；在這裡也做一次收尾（可重複呼叫）。
            desktop.ShutdownRequested += (_, _) => window.FinishSession();

            Program.Instance?.Listen(() => Dispatcher.UIThread.Post(window.BringToFront));
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string? Describe<T>(string fileName, LoadResult<T> result, string what) => result.Outcome switch
    {
        LoadOutcome.RestoredFromBackup =>
            $"{fileName} 無法解析，已改名保留為「{Path.GetFileName(result.PreservedPath)}」，並從備份（.bak）還原{what}。",
        LoadOutcome.CorruptUsingDefault =>
            $"{fileName} 無法解析，已改名保留為「{Path.GetFileName(result.PreservedPath)}」。沒有可用的備份，{what}從空白開始。",
        LoadOutcome.Unreadable when result.PreservedPath is not null =>
            $"{fileName} 無法解析，已改名保留為「{Path.GetFileName(result.PreservedPath)}」；備份（.bak）目前無法讀取（{result.Error}）。" +
            $"為避免覆蓋備份，本次執行不會寫入{what}。請解除檔案鎖定後重新開啟程式，即可從備份還原。",
        LoadOutcome.Unreadable =>
            $"無法讀取 {fileName} 或其備份（{result.Error}）。為避免覆蓋原檔，本次執行不會寫入{what}。",
        LoadOutcome.MissingRestoredFromBackup =>
            $"找不到 {fileName}，已從備份（.bak）還原{what}。",
        LoadOutcome.MissingBackupCorrupt =>
            $"找不到 {fileName}，備份（.bak）也無法解析，已改名保留為「{Path.GetFileName(result.PreservedPath)}」。{what}從空白開始。",
        _ => null,
    };
}
