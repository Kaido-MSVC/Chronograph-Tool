namespace ChronographTool.Models;

public enum SessionKind
{
    Stopwatch,
    Countdown,
}

public enum SessionStatus
{
    /// <summary>碼表正常結束（重置或關閉程式），或倒數完整跑完。</summary>
    Completed,

    /// <summary>倒數在時間到之前被重置或因關閉程式而中斷。</summary>
    Aborted,
}

public sealed record LapRecord(int Number, TimeSpan Split, TimeSpan Cumulative);

/// <summary>一次碼表或倒數的完整紀錄。</summary>
public sealed class SessionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public SessionKind Kind { get; set; }
    public SessionStatus Status { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }

    /// <summary>實際計時時長（以單調時間戳計算，不含暫停時間）。</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>倒數設定的時長；碼表為 null。</summary>
    public TimeSpan? TargetDuration { get; set; }

    public List<LapRecord> Laps { get; set; } = [];
    public string Note { get; set; } = "";

    /// <summary>總時長少於這個值的計時不存紀錄。</summary>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(1);

    public static bool ShouldKeep(TimeSpan duration) => duration >= MinimumDuration;
}

/// <summary>records.json 的根物件。</summary>
public sealed class RecordsDocument
{
    public int Version { get; set; } = 1;
    public List<SessionRecord> Records { get; set; } = [];

    /// <summary>
    /// 檢查反序列化後的結構。C# 的非 null 宣告不保證 JSON 內容符合，
    /// 例如 {"records":null} 或 "laps":null 都能反序列化成功。
    /// 清單或元素為 null 時視為損壞（回傳錯誤訊息）；備註為 null 時補成空字串。
    /// </summary>
    public static string? Validate(RecordsDocument doc)
    {
        if (doc.Records is null) return "records 欄位不是清單";
        for (int i = 0; i < doc.Records.Count; i++)
        {
            var r = doc.Records[i];
            if (r is null) return $"第 {i + 1} 筆紀錄為 null";
            if (r.Laps is null) return $"第 {i + 1} 筆紀錄的 laps 不是清單";
            if (r.Laps.Any(l => l is null)) return $"第 {i + 1} 筆紀錄的計次含有 null";
            r.Note ??= "";
        }
        return null;
    }
}
