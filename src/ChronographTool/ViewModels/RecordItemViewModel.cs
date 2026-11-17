using ChronographTool.Core;
using ChronographTool.Models;
using ChronographTool.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChronographTool.ViewModels;

public sealed partial class RecordItemViewModel : ObservableObject
{
    public RecordItemViewModel(SessionRecord record)
    {
        Record = record;
        Note = record.Note;
        var items = record.Laps.Select(l => new LapItemViewModel(l)).ToList();
        LapItemViewModel.MarkExtremes(items, record.Laps);
        Laps = items;
    }

    public SessionRecord Record { get; }
    public Guid Id => Record.Id;
    public string KindText => Exporters.KindText(Record.Kind);
    public string StatusText => Exporters.StatusText(Record.Status);
    public bool IsAborted => Record.Status == SessionStatus.Aborted;
    public string StartText => TimeFormat.DateTime(Record.StartTime);
    public string EndText => TimeFormat.DateTime(Record.EndTime);
    public string DurationText => TimeFormat.Duration(Record.Duration);
    public string TargetText => Record.TargetDuration is { } t ? TimeFormat.Duration(t) : "—";
    public bool IsCountdown => Record.Kind == SessionKind.Countdown;
    public IReadOnlyList<LapItemViewModel> Laps { get; }
    public bool HasLaps => Laps.Count > 0;
    public string LapCountText => Record.Kind == SessionKind.Stopwatch ? $"{Laps.Count} 圈" : "";

    /// <summary>備註有尚未寫入檔案的變更。</summary>
    public bool IsNoteDirty { get; set; }

    [ObservableProperty]
    public partial string Note { get; set; }

    partial void OnNoteChanged(string value)
    {
        if (Record.Note == value) return;
        Record.Note = value;
        IsNoteDirty = true;
        OnPropertyChanged(nameof(NotePreview));
    }

    /// <summary>清單中顯示的單行備註。</summary>
    public string NotePreview => Note.ReplaceLineEndings(" ");

    /// <summary>清單項目的無障礙名稱（螢幕閱讀器與 UI 自動化會讀取）。</summary>
    public override string ToString() => $"{KindText} {StatusText} {StartText} {DurationText}";
}
