using ChronographTool.Models;

namespace ChronographTool.Core;

/// <summary>
/// 倒數邏輯。剩餘時間＝設定時長－實際經過時間（單調時間戳），不靠計時器累加。
/// 完成時刻會扣除偵測延遲，回推成實際到時的時間點。
/// </summary>
public sealed class CountdownEngine(TimeProvider time)
{
    private TimeSpan accumulated;
    private long? runningSince;

    public TimeSpan Duration { get; private set; }
    public bool IsRunning => runningSince is not null;
    public bool IsCompleted { get; private set; }
    public bool HasStarted => IsRunning || IsCompleted || accumulated > TimeSpan.Zero;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private TimeSpan RawElapsed => runningSince is { } since ? accumulated + time.GetElapsedTime(since) : accumulated;

    public TimeSpan Elapsed => IsCompleted ? Duration : Min(RawElapsed, Duration);
    public TimeSpan Remaining => Duration - Elapsed;

    /// <summary>設定時長；只能在尚未開始時設定。</summary>
    public bool SetDuration(TimeSpan duration)
    {
        if (HasStarted || duration <= TimeSpan.Zero) return false;
        Duration = duration;
        return true;
    }

    public void Start()
    {
        if (IsRunning || IsCompleted || Duration <= TimeSpan.Zero) return;
        StartedAt ??= time.GetLocalNow();
        runningSince = time.GetTimestamp();
    }

    public void Pause()
    {
        if (runningSince is not { } since) return;
        accumulated += time.GetElapsedTime(since);
        runningSince = null;
    }

    /// <summary>檢查是否已到時；第一次偵測到時回傳 true 並停止計時。</summary>
    public bool CheckCompleted()
    {
        if (!IsRunning) return false;
        var now = time.GetLocalNow();
        var elapsed = RawElapsed;
        if (elapsed < Duration) return false;

        var overshoot = elapsed - Duration;
        runningSince = null;
        accumulated = Duration;
        IsCompleted = true;
        CompletedAt = now - overshoot;
        return true;
    }

    /// <summary>產生目前這次倒數的紀錄；未開始時回傳 null。</summary>
    public SessionRecord? CreateRecord()
    {
        if (!HasStarted || StartedAt is not { } started) return null;
        return new SessionRecord
        {
            Kind = SessionKind.Countdown,
            Status = IsCompleted ? SessionStatus.Completed : SessionStatus.Aborted,
            StartTime = started,
            EndTime = CompletedAt ?? time.GetLocalNow(),
            Duration = Elapsed,
            TargetDuration = Duration,
        };
    }

    /// <summary>回到未開始狀態，保留已設定的時長。</summary>
    public void Reset()
    {
        runningSince = null;
        accumulated = TimeSpan.Zero;
        IsCompleted = false;
        StartedAt = null;
        CompletedAt = null;
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
