using Avalonia.Threading;
using ChronographTool.Models;
using ChronographTool.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChronographTool.ViewModels;

public enum ShortcutAction
{
    StartPause,
    Lap,
    Reset,
}

public sealed partial class MainWindowViewModel : ObservableObject
{
    public const int StopwatchTab = 0;
    public const int CountdownTab = 1;
    public const int RecordsTab = 2;

    private readonly SettingsService settings;
    private readonly IUiServices ui;
    // 約 60 fps；只負責刷新畫面，時間由時間戳計算。
    private readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    private bool shutDown;

    public MainWindowViewModel(RecordRepository repository, SettingsService settings, IUiServices ui, TimeProvider time)
    {
        this.settings = settings;
        this.ui = ui;
        Records = new RecordsViewModel(repository, ui, Notify);
        Stopwatch = new StopwatchViewModel(time, SaveRecord, EnsureTicking);
        Countdown = new CountdownViewModel(time, settings.Current, ui, SaveRecord, EnsureTicking);
        SelectedTab = Math.Clamp(settings.Current.SelectedTab, StopwatchTab, RecordsTab);
        timer.Tick += (_, _) => Tick();
    }

    public StopwatchViewModel Stopwatch { get; }
    public CountdownViewModel Countdown { get; }
    public RecordsViewModel Records { get; }

    [ObservableProperty]
    public partial int SelectedTab { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; set; }

    public bool HasNotice => !string.IsNullOrEmpty(Notice);

    public void Notify(string message) =>
        Notice = string.IsNullOrEmpty(Notice) || Notice == message ? message : Notice + Environment.NewLine + message;

    [RelayCommand]
    private void DismissNotice() => Notice = null;

    /// <summary>處理快捷鍵；回傳 true 表示已處理。</summary>
    public bool HandleShortcut(ShortcutAction action)
    {
        switch (SelectedTab, action)
        {
            case (StopwatchTab, ShortcutAction.StartPause):
                Stopwatch.StartPauseCommand.Execute(null);
                return true;
            case (StopwatchTab, ShortcutAction.Lap):
                if (Stopwatch.LapCommand.CanExecute(null)) Stopwatch.LapCommand.Execute(null);
                return true;
            case (StopwatchTab, ShortcutAction.Reset):
                if (Stopwatch.ResetCommand.CanExecute(null)) Stopwatch.ResetCommand.Execute(null);
                return true;
            case (CountdownTab, ShortcutAction.StartPause):
                Countdown.StartPauseCommand.Execute(null);
                return true;
            case (CountdownTab, ShortcutAction.Reset):
                if (Countdown.ResetCommand.CanExecute(null)) Countdown.ResetCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 關閉程式前呼叫，可重複呼叫：先存備註，再把進行中的碼表／倒數存成紀錄，最後存設定。
    /// </summary>
    public void Shutdown(Action<AppSettings> writeWindowState)
    {
        if (shutDown) return;
        shutDown = true;
        timer.Stop();

        Records.CommitNote();
        Stopwatch.SaveOnExit();
        Countdown.SaveOnExit();

        var current = settings.Current;
        current.SelectedTab = SelectedTab;
        Countdown.WriteTo(current);
        writeWindowState(current);
        settings.Save();
    }

    /// <summary>Shutdown 之後，是否還有紀錄沒有寫入 records.json。</summary>
    public bool HasUnsavedRecords => Records.HasUnsavedChanges;

    /// <summary>
    /// 關閉前紀錄寫入失敗時，讓使用者選擇重試、另存或放棄。
    /// 回傳 true 表示可以關閉；false 表示使用者選擇返回程式（此時恢復成可繼續使用的狀態）。
    /// </summary>
    public async Task<bool> ResolveUnsavedRecordsAsync()
    {
        string? detail = null;
        while (Records.HasUnsavedChanges)
        {
            bool canRetry = Records.CanSaveToFile;
            var message = (canRetry
                    ? "有紀錄無法寫入 records.json，檔案可能被其他程式鎖定或沒有寫入權限。"
                    : "啟動時無法讀取 records.json，為避免覆蓋原檔，本次新增或修改的紀錄都沒有寫入。")
                + "直接關閉的話，這些變更會遺失。\n\n「另存新檔」會把全部紀錄存成 JSON，格式與 records.json 相同。"
                + (detail is null ? "" : $"\n\n{detail}");

            switch (await ui.AskUnsavedRecordsAsync(message, canRetry))
            {
                case UnsavedRecordsChoice.Retry:
                    if (Records.TrySaveNow()) return true;
                    detail = "重試仍然失敗。";
                    break;
                case UnsavedRecordsChoice.SaveCopy:
                    var (saved, error) = await Records.SaveCopyAsync();
                    if (saved) return true;
                    detail = error is null ? null : $"另存失敗：{error}";
                    break;
                case UnsavedRecordsChoice.Discard:
                    return true;
                default:
                    shutDown = false;
                    Notify("紀錄尚未寫入 records.json。解除檔案鎖定後，下次變更或關閉程式時會再嘗試寫入。");
                    return false;
            }
        }
        return true;
    }

    private void SaveRecord(SessionRecord record)
    {
        if (!SessionRecord.ShouldKeep(record.Duration)) return;
        Records.Add(record);
    }

    private void EnsureTicking()
    {
        if (!shutDown && !timer.IsEnabled) timer.Start();
        Tick();
    }

    private void Tick()
    {
        bool stopwatchRunning = Stopwatch.Tick();
        bool countdownRunning = Countdown.Tick();
        if (!stopwatchRunning && !countdownRunning) timer.Stop();
    }
}
