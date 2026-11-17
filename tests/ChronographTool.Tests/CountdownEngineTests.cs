using ChronographTool.Core;
using ChronographTool.Models;

namespace ChronographTool.Tests;

public class CountdownEngineTests
{
    [Fact]
    public void Completes_WhenElapsedReachesDuration_WithAccurateEndTime()
    {
        var time = new FakeTimeProvider();
        var cd = new CountdownEngine(time);
        Assert.True(cd.SetDuration(TimeSpan.FromSeconds(3)));
        var startedAt = time.GetLocalNow();
        cd.Start();

        time.AdvanceMs(2999);
        Assert.False(cd.CheckCompleted());
        Assert.Equal(TimeSpan.FromMilliseconds(1), cd.Remaining);

        // UI 晚 15 ms 才偵測到：結束時間仍應是開始後剛好 3 秒。
        time.AdvanceMs(16);
        Assert.True(cd.CheckCompleted());
        Assert.False(cd.CheckCompleted());
        Assert.Equal(TimeSpan.Zero, cd.Remaining);

        var record = cd.CreateRecord()!;
        Assert.Equal(SessionStatus.Completed, record.Status);
        Assert.Equal(startedAt.AddSeconds(3), record.EndTime);
        Assert.Equal(TimeSpan.FromSeconds(3), record.Duration);
    }

    [Fact]
    public void Pause_StopsCountdown_AndResetBeforeEndIsAborted()
    {
        var time = new FakeTimeProvider();
        var cd = new CountdownEngine(time);
        cd.SetDuration(TimeSpan.FromSeconds(10));
        cd.Start();
        time.AdvanceMs(4000);
        cd.Pause();
        time.AdvanceMs(60_000);

        Assert.False(cd.CheckCompleted());
        Assert.Equal(TimeSpan.FromSeconds(6), cd.Remaining);

        cd.Start();
        time.AdvanceMs(1000);
        var record = cd.CreateRecord()!;
        Assert.Equal(SessionStatus.Aborted, record.Status);
        Assert.Equal(TimeSpan.FromSeconds(5), record.Duration);
        Assert.Equal(TimeSpan.FromSeconds(10), record.TargetDuration);
    }

    [Fact]
    public void Duration_CannotChangeAfterStart_OrBeZero()
    {
        var cd = new CountdownEngine(new FakeTimeProvider());
        Assert.False(cd.SetDuration(TimeSpan.Zero));
        cd.SetDuration(TimeSpan.FromMinutes(1));
        cd.Start();
        Assert.False(cd.SetDuration(TimeSpan.FromMinutes(5)));
        Assert.Equal(TimeSpan.FromMinutes(1), cd.Duration);
    }
}
