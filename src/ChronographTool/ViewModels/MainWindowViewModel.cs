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
    // 約 60 fps；只負責刷新畫面，時間由時間戳計算。
    private readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    private bool shutDown;

    public MainWindowViewModel(RecordRepository repository, SettingsService settings, IUiServices ui, TimeProvider time)
    {
        this.settings = settings;
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
