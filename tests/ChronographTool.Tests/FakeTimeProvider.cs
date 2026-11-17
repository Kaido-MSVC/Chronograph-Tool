namespace ChronographTool.Tests;

/// <summary>可手動推進的時間來源；時間戳以 Tick（100 ns）為單位。</summary>
internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private long ticks;

    public FakeTimeProvider() : this(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero)) { }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => ticks;
    public override DateTimeOffset GetUtcNow() => start.AddTicks(ticks);
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void Advance(TimeSpan by) => ticks += by.Ticks;
    public void AdvanceMs(int ms) => Advance(TimeSpan.FromMilliseconds(ms));
}
