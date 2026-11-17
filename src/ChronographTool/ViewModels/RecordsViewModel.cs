using System.Collections.ObjectModel;
using System.Globalization;
using ChronographTool.Models;
using ChronographTool.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChronographTool.ViewModels;

public sealed partial class RecordsViewModel : ObservableObject
{
    private readonly RecordRepository repository;
    private readonly IUiServices ui;
    private readonly Action<string> notify;
    private readonly List<RecordItemViewModel> all = [];
    private List<RecordItemViewModel> selected = [];

    public RecordsViewModel(RecordRepository repository, IUiServices ui, Action<string> notify)
    {
        this.repository = repository;
        this.ui = ui;
        this.notify = notify;
        all.AddRange(repository.Records.Select(r => new RecordItemViewModel(r)));
        ApplyFilter();
    }

    public IReadOnlyList<string> KindOptions { get; } = ["全部類型", "碼表", "計時器"];
    public IReadOnlyList<string> ExportScopeOptions { get; } = ["全部紀錄", "選取的紀錄", "目前篩選結果"];
    public IReadOnlyList<string> ExportFormatOptions { get; } = ["CSV（每筆紀錄一列）", "CSV（計次明細，每圈一列）", "JSON"];

    /// <summary>篩選後的清單，最新的在最上面。</summary>
    public ObservableCollection<RecordItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial int KindFilter { get; set; }

    [ObservableProperty]
    public partial DateTime? FromDate { get; set; }

    [ObservableProperty]
    public partial DateTime? ToDate { get; set; }

    [ObservableProperty]
    public partial int ExportScope { get; set; }

    [ObservableProperty]
    public partial int ExportFormatIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrent))]
    public partial RecordItemViewModel? Current { get; set; }

    public bool HasCurrent => Current is not null;
    public bool IsEmpty => Items.Count == 0;
    public bool HasAnyRecord => all.Count > 0;
    public string EmptyText => all.Count == 0 ? "還沒有紀錄。碼表重置或倒數結束後，會自動存在這裡。" : "沒有符合篩選條件的紀錄。";
    public string SummaryText => selected.Count > 0
        ? $"顯示 {Items.Count} 筆（共 {all.Count} 筆），已選取 {selected.Count} 筆"
        : $"顯示 {Items.Count} 筆（共 {all.Count} 筆）";

    partial void OnKindFilterChanged(int value) => ApplyFilter();
    partial void OnFromDateChanged(DateTime? value) => ApplyFilter();
    partial void OnToDateChanged(DateTime? value) => ApplyFilter();

    public void Add(SessionRecord record)
    {
        CommitNote();
        var item = new RecordItemViewModel(record);
        all.Add(item);
        if (!repository.Add(record)) notify(SaveFailedMessage);
        if (Matches(item)) Items.Insert(0, item);
        RaiseListChanged();
    }

    public void SetSelection(IEnumerable<RecordItemViewModel> items)
    {
        selected = [.. items];
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SummaryText));
    }

    /// <summary>
    /// 把尚未存檔的備註，以及先前寫入失敗的變更寫入 records.json
    /// （編輯框失去焦點、切換紀錄或關閉程式時呼叫）。
    /// </summary>
    public void CommitNote()
    {
        if (!TrySaveNow()) notify(SaveFailedMessage);
    }

    /// <summary>有尚未成功寫入 records.json 的變更（含還沒提交的備註）。</summary>
    public bool HasUnsavedChanges => repository.HasUnsavedChanges || all.Any(i => i.IsNoteDirty);

    /// <summary>records.json 是否允許寫入（讀取失敗時為 false，重試也不會寫入）。</summary>
    public bool CanSaveToFile => repository.CanSave;

    /// <summary>不顯示通知的重試存檔，回傳是否成功。</summary>
    public bool TrySaveNow()
    {
        var dirty = all.Where(i => i.IsNoteDirty).ToList();
        foreach (var item in dirty) item.IsNoteDirty = false;
        if (dirty.Count > 0) repository.MarkChanged();
        return !repository.HasUnsavedChanges || repository.Save();
    }

    /// <summary>
    /// 把全部紀錄另存成 JSON（與 records.json 同格式）。
    /// 回傳 Saved=false 且 Error=null 表示使用者取消。
    /// </summary>
    public async Task<(bool Saved, string? Error)> SaveCopyAsync()
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        try
        {
            await using var stream = await ui.PickSaveFileAsync("另存紀錄", $"records-{stamp}.json", "JSON 檔案", "json");
            if (stream is null) return (false, null);
            await repository.WriteCopyToAsync(stream);
            return (true, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return (false, ex.Message);
        }
    }

    partial void OnCurrentChanged(RecordItemViewModel? oldValue, RecordItemViewModel? newValue) => CommitNote();

    private bool CanDeleteSelected() => selected.Count > 0;

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        CommitNote();
        var targets = selected.ToList();
        if (targets.Count == 0) return;

        var message = targets.Count == 1
            ? $"確定要刪除 {targets[0].StartText} 的{targets[0].KindText}紀錄嗎？刪除後無法復原。"
            : $"確定要刪除選取的 {targets.Count} 筆紀錄嗎？刪除後無法復原。";
        if (!await ui.ConfirmAsync("刪除紀錄", message, "刪除")) return;

        foreach (var item in targets)
        {
            all.Remove(item);
            Items.Remove(item);
        }
        if (!repository.Remove(targets.Select(t => t.Id))) notify(SaveFailedMessage);
        SetSelection([]);
        RaiseListChanged();
    }

    [RelayCommand]
    private void ClearFilter()
    {
        KindFilter = 0;
        FromDate = null;
        ToDate = null;
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        CommitNote();
        IEnumerable<RecordItemViewModel> source = ExportScope switch
        {
            1 => selected,
            2 => Items,
            _ => all,
        };
        // 匯出時一律依開始時間由舊到新排序。
        var records = source.Select(i => i.Record).OrderBy(r => r.StartTime).ToList();
        if (records.Count == 0)
        {
            notify(ExportScope == 1 ? "請先在清單中選取要匯出的紀錄。" : "沒有可匯出的紀錄。");
            return;
        }

        var format = (ExportFormat)Math.Clamp(ExportFormatIndex, 0, 2);
        if (format == ExportFormat.CsvLaps && records.All(r => r.Laps.Count == 0))
        {
            notify("選取範圍內沒有計次明細可匯出。");
            return;
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var (name, typeName, extension) = format switch
        {
            ExportFormat.CsvRecords => ($"ChronographTool-{stamp}.csv", "CSV 檔案", "csv"),
            ExportFormat.CsvLaps => ($"ChronographTool-laps-{stamp}.csv", "CSV 檔案", "csv"),
            _ => ($"ChronographTool-{stamp}.json", "JSON 檔案", "json"),
        };

        try
        {
            await using var stream = await ui.PickSaveFileAsync("匯出紀錄", name, typeName, extension);
            if (stream is null) return;
            var bytes = Exporters.ToBytes(format, records);
            if (stream.CanSeek) stream.SetLength(0);
            await stream.WriteAsync(bytes);
            notify($"已匯出 {records.Count} 筆紀錄。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            notify($"匯出失敗：{ex.Message}");
        }
    }

    private const string SaveFailedMessage = "紀錄無法寫入 records.json，請確認檔案沒有被其他程式鎖定。資料仍保留在程式中，下次變更或關閉程式時會再嘗試寫入。";

    private bool Matches(RecordItemViewModel item)
    {
        var r = item.Record;
        if (KindFilter == 1 && r.Kind != SessionKind.Stopwatch) return false;
        if (KindFilter == 2 && r.Kind != SessionKind.Countdown) return false;
        var day = r.StartTime.ToLocalTime().Date;
        if (FromDate is { } from && day < from.Date) return false;
        if (ToDate is { } to && day > to.Date) return false;
        return true;
    }

    private void ApplyFilter()
    {
        Items.Clear();
        foreach (var item in all.Where(Matches).OrderByDescending(i => i.Record.StartTime))
            Items.Add(item);
        RaiseListChanged();
    }

    private void RaiseListChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasAnyRecord));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(SummaryText));
    }
}
