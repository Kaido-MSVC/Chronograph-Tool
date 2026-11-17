using ChronographTool.Models;

namespace ChronographTool.Core;

/// <summary>
/// 碼表邏輯。經過時間一律由 <see cref="TimeProvider"/> 的單調時間戳差值計算，
/// UI 刷新頻率只影響顯示，不會造成時間漂移。
/// </summary>
public sealed class StopwatchEngine(TimeProvider time)
{
    private readonly List<LapRecord> laps = [];
    private TimeSpan accumulated;
    private long? runningSince;

    public bool IsRunning => runningSince is not null;
    public bool HasStarted => IsRunning || accumulated > TimeSpan.Zero;
    public DateTimeOffset? StartedAt { get; private set; }
    public IReadOnlyList<LapRecord> Laps => laps;

    public TimeSpan Elapsed => runningSince is { } since ? accumulated + time.GetElapsedTime(since) : accumulated;

    public void Start()
    {
        if (IsRunning) return;
        StartedAt ??= time.GetLocalNow();
        runningSince = time.GetTimestamp();
    }

    public void Pause()
    {
        if (runningSince is not { } since) return;
        accumulated += time.GetElapsedTime(since);
        runningSince = null;
    }

    /// <summary>記錄一圈；只在計時中有效。</summary>
    public LapRecord? Lap()
    {
        if (!IsRunning) return null;
        var total = Elapsed;
        var previous = laps.Count == 0 ? TimeSpan.Zero : laps[^1].Cumulative;
        var lap = new LapRecord(laps.Count + 1, total - previous, total);
        laps.Add(lap);
        return lap;
    }

    /// <summary>產生目前這次計時的紀錄；未開始時回傳 null。</summary>
    public SessionRecord? CreateRecord()
    {
        if (!HasStarted || StartedAt is not { } started) return null;
        return new SessionRecord
        {
            Kind = SessionKind.Stopwatch,
            Status = SessionStatus.Completed,
            StartTime = started,
            EndTime = time.GetLocalNow(),
            Duration = Elapsed,
            Laps = [.. laps],
        };
    }

    public void Reset()
    {
        runningSince = null;
        accumulated = TimeSpan.Zero;
        StartedAt = null;
        laps.Clear();
    }

    /// <summary>找出最快與最慢的一圈編號；少於兩圈時不標示。同秒數時取較早的一圈。</summary>
    public static (int? Fastest, int? Slowest) FindExtremes(IReadOnlyList<LapRecord> laps)
    {
        if (laps.Count < 2) return (null, null);
        var fastest = laps[0];
        var slowest = laps[0];
        foreach (var lap in laps)
        {
            if (lap.Split < fastest.Split) fastest = lap;
            if (lap.Split > slowest.Split) slowest = lap;
        }
        return fastest.Split == slowest.Split ? (null, null) : (fastest.Number, slowest.Number);
    }
}
