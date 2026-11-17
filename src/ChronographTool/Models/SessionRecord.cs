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
}
