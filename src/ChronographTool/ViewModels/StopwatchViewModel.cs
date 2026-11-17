using System.Collections.ObjectModel;
using ChronographTool.Core;
using ChronographTool.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChronographTool.ViewModels;

public sealed partial class StopwatchViewModel : ObservableObject
{
    private readonly StopwatchEngine engine;
    private readonly Action<SessionRecord> saveRecord;
    private readonly Action ensureTicking;

    public StopwatchViewModel(TimeProvider time, Action<SessionRecord> saveRecord, Action ensureTicking)
    {
        engine = new StopwatchEngine(time);
        this.saveRecord = saveRecord;
        this.ensureTicking = ensureTicking;
    }

    /// <summary>計次清單，最新的一圈在最上面。</summary>
    public ObservableCollection<LapItemViewModel> Laps { get; } = [];

    [ObservableProperty]
    public partial string Display { get; set; } = TimeFormat.Duration(TimeSpan.Zero);

    [ObservableProperty]
    public partial string StartPauseText { get; set; } = "開始";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LapCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    public partial bool HasStarted { get; set; }

    public bool HasLaps => Laps.Count > 0;

    [RelayCommand]
    private void StartPause()
    {
        if (engine.IsRunning) engine.Pause();
        else engine.Start();
        Refresh();
        ensureTicking();
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Lap()
    {
        if (engine.Lap() is not { } lap) return;
        Laps.Insert(0, new LapItemViewModel(lap));
        LapItemViewModel.MarkExtremes(Laps, engine.Laps);
        OnPropertyChanged(nameof(HasLaps));
    }

    [RelayCommand(CanExecute = nameof(HasStarted))]
    private void Reset()
    {
        SaveCurrent();
        engine.Reset();
        Laps.Clear();
        OnPropertyChanged(nameof(HasLaps));
        Refresh();
    }

    /// <summary>由 UI 刷新計時器呼叫；回傳是否仍需要刷新。</summary>
    public bool Tick()
    {
        Display = TimeFormat.Duration(engine.Elapsed);
        return engine.IsRunning;
    }

    /// <summary>關閉程式時：已開始（計時中或暫停中）的碼表直接存成一筆紀錄。</summary>
    public void SaveOnExit()
    {
        engine.Pause();
        SaveCurrent();
        engine.Reset();
        // 關閉可能因紀錄未儲存而取消，畫面要跟著回到未開始狀態。
        Laps.Clear();
        OnPropertyChanged(nameof(HasLaps));
        Refresh();
    }

    private void SaveCurrent()
    {
        if (engine.CreateRecord() is { } record) saveRecord(record);
    }

    private void Refresh()
    {
        IsRunning = engine.IsRunning;
        HasStarted = engine.HasStarted;
        StartPauseText = engine.IsRunning ? "暫停" : engine.HasStarted ? "繼續" : "開始";
        Display = TimeFormat.Duration(engine.Elapsed);
    }
}
