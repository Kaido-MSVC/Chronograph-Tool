using ChronographTool.Core;
using ChronographTool.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChronographTool.ViewModels;

public sealed record CountdownPreset(string Label, int Minutes);

public sealed partial class CountdownViewModel : ObservableObject
{
    private readonly CountdownEngine engine;
    private readonly Action<SessionRecord> saveRecord;
    private readonly Action ensureTicking;
    private readonly IUiServices ui;

    public CountdownViewModel(TimeProvider time, AppSettings settings, IUiServices ui, Action<SessionRecord> saveRecord, Action ensureTicking)
    {
        engine = new CountdownEngine(time);
        this.ui = ui;
        this.saveRecord = saveRecord;
        this.ensureTicking = ensureTicking;
        Hours = Math.Clamp(settings.CountdownHours, 0, 99);
        Minutes = Math.Clamp(settings.CountdownMinutes, 0, 59);
        Seconds = Math.Clamp(settings.CountdownSeconds, 0, 59);
        SoundEnabled = settings.SoundEnabled;
        Refresh();
    }

    public IReadOnlyList<CountdownPreset> Presets { get; } =
    [
        new("1 分", 1), new("3 分", 3), new("5 分", 5), new("10 分", 10), new("25 分", 25),
    ];

    [ObservableProperty]
    public partial decimal? Hours { get; set; }

    [ObservableProperty]
    public partial decimal? Minutes { get; set; }

    [ObservableProperty]
    public partial decimal? Seconds { get; set; }

    [ObservableProperty]
    public partial bool SoundEnabled { get; set; }

    [ObservableProperty]
    public partial string Display { get; set; } = "";

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string StartPauseText { get; set; } = "開始";

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditDuration))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    public partial bool HasStarted { get; set; }

    [ObservableProperty]
    public partial bool IsTimeUp { get; set; }

    [ObservableProperty]
    public partial string? InputError { get; set; }

    public bool CanEditDuration => !HasStarted;

    public TimeSpan InputDuration => new(
        (int)Math.Clamp(Hours ?? 0, 0, 99),
        (int)Math.Clamp(Minutes ?? 0, 0, 59),
        (int)Math.Clamp(Seconds ?? 0, 0, 59));

    partial void OnHoursChanged(decimal? value) => InputChanged();
    partial void OnMinutesChanged(decimal? value) => InputChanged();
    partial void OnSecondsChanged(decimal? value) => InputChanged();

    private void InputChanged()
    {
        InputError = null;
        if (!engine.HasStarted) Refresh();
    }

    [RelayCommand]
    private void ApplyPreset(CountdownPreset preset)
    {
        if (engine.HasStarted) return;
        Hours = preset.Minutes / 60;
        Minutes = preset.Minutes % 60;
        Seconds = 0;
    }

    [RelayCommand]
    private void StartPause()
    {
        // 時間到之後再按開始：以同樣的時長重新開始（完成的那次已存過紀錄）。
        if (engine.IsCompleted)
        {
            engine.Reset();
            IsTimeUp = false;
        }

        if (engine.IsRunning)
        {
            // 已到時但 UI 還沒刷新到：當作完成處理，不要暫停成「中止」。
            if (engine.CheckCompleted())
            {
                OnCompleted();
                return;
            }
            engine.Pause();
        }
        else
        {
            if (!engine.HasStarted && !engine.SetDuration(InputDuration))
            {
                InputError = "請先設定大於 0 的時間。";
                return;
            }
            engine.Start();
        }
        Refresh();
        ensureTicking();
    }

    [RelayCommand(CanExecute = nameof(HasStarted))]
    private void Reset()
    {
        // 時間到之前重置＝中止，存一筆紀錄；已完成的在完成當下就存過了。
        // 已到時但 UI 還沒刷新到時，先結算成「完成」（使用者正要重置，不再發出提醒）。
        if (engine.CheckCompleted() || !engine.IsCompleted) SaveCurrent();
        engine.Reset();
        IsTimeUp = false;
        Refresh();
    }

    [RelayCommand]
    private void DismissTimeUp() => IsTimeUp = false;

    /// <summary>由 UI 刷新計時器呼叫；回傳是否仍需要刷新。</summary>
    public bool Tick()
    {
        if (engine.CheckCompleted())
        {
            OnCompleted();
            return false;
        }
        UpdateDisplay();
        return engine.IsRunning;
    }

    /// <summary>關閉程式時：已到時的先結算成「完成」，其餘尚未完成的倒數存成「中止」。</summary>
    public void SaveOnExit()
    {
        if (engine.CheckCompleted())
        {
            SaveCurrent();
        }
        else if (engine.HasStarted && !engine.IsCompleted)
        {
            engine.Pause();
            SaveCurrent();
        }
        engine.Reset();
        IsTimeUp = false;
        Refresh();
    }

    private void OnCompleted()
    {
        SaveCurrent();
        IsTimeUp = true;
        Refresh();
        ui.AlertTimeUp(SoundEnabled);
    }

    public void WriteTo(AppSettings settings)
    {
        settings.CountdownHours = (int)(Hours ?? 0);
        settings.CountdownMinutes = (int)(Minutes ?? 0);
        settings.CountdownSeconds = (int)(Seconds ?? 0);
        settings.SoundEnabled = SoundEnabled;
    }

    private void SaveCurrent()
    {
        if (engine.CreateRecord() is { } record) saveRecord(record);
    }

    private void Refresh()
    {
        IsRunning = engine.IsRunning;
        HasStarted = engine.HasStarted;
        StartPauseText = engine.IsRunning ? "暫停" : engine.IsCompleted ? "重新開始" : engine.HasStarted ? "繼續" : "開始";
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (engine.HasStarted)
        {
            Display = TimeFormat.Duration(engine.Remaining);
            Progress = engine.Duration > TimeSpan.Zero ? engine.Elapsed / engine.Duration * 100 : 0;
        }
        else
        {
            Display = TimeFormat.Duration(InputDuration);
            Progress = 0;
        }
    }
}
