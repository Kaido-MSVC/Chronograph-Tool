using ChronographTool.Core;
using ChronographTool.Models;

namespace ChronographTool.Tests;

public class StopwatchEngineTests
{
    [Fact]
    public void Laps_HaveSplitAndCumulative_AndMarkFastestSlowest()
    {
        var time = new FakeTimeProvider();
        var sw = new StopwatchEngine(time);
        sw.Start();

        time.AdvanceMs(1500); sw.Lap();
        time.AdvanceMs(900); sw.Lap();
        time.AdvanceMs(2100); sw.Lap();

        Assert.Collection(sw.Laps,
            l => Assert.Equal((1, 1500, 1500), (l.Number, (int)l.Split.TotalMilliseconds, (int)l.Cumulative.TotalMilliseconds)),
            l => Assert.Equal((2, 900, 2400), (l.Number, (int)l.Split.TotalMilliseconds, (int)l.Cumulative.TotalMilliseconds)),
            l => Assert.Equal((3, 2100, 4500), (l.Number, (int)l.Split.TotalMilliseconds, (int)l.Cumulative.TotalMilliseconds)));

        Assert.Equal((2, 3), StopwatchEngine.FindExtremes(sw.Laps));
    }

    [Fact]
    public void Pause_ExcludesPausedTime_AndLapIsIgnoredWhilePaused()
    {
        var time = new FakeTimeProvider();
        var sw = new StopwatchEngine(time);
        sw.Start();
        time.AdvanceMs(1000);
        sw.Pause();
        time.AdvanceMs(5000);
        Assert.Null(sw.Lap());
        sw.Start();
        time.AdvanceMs(250);

        Assert.Equal(TimeSpan.FromMilliseconds(1250), sw.Elapsed);
        var record = sw.CreateRecord()!;
        Assert.Equal(TimeSpan.FromMilliseconds(1250), record.Duration);
        Assert.Equal(SessionKind.Stopwatch, record.Kind);
        Assert.Equal(time.GetLocalNow().AddMilliseconds(-6250), record.StartTime);
    }

    [Fact]
    public void FindExtremes_NeedsAtLeastTwoDifferentLaps()
    {
        LapRecord one = new(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        Assert.Equal((null, null), StopwatchEngine.FindExtremes([one]));
    }

    [Fact]
    public void ShortSessions_AreNotKept()
    {
        Assert.False(SessionRecord.ShouldKeep(TimeSpan.FromMilliseconds(999)));
        Assert.True(SessionRecord.ShouldKeep(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Format_IsHoursMinutesSecondsMilliseconds()
    {
        Assert.Equal("01:02:03.045", TimeFormat.Duration(new TimeSpan(0, 1, 2, 3, 45)));
        Assert.Equal("00:00:00.000", TimeFormat.Duration(TimeSpan.Zero));
    }
}
