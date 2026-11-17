using System.Globalization;

namespace ChronographTool.Core;

public static class TimeFormat
{
    /// <summary>格式化為 HH:MM:SS.mmm；超過 99 小時時小時位數會自動加長。</summary>
    public static string Duration(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        long totalMs = (long)value.TotalMilliseconds;
        long hours = totalMs / 3_600_000;
        int minutes = (int)(totalMs / 60_000 % 60);
        int seconds = (int)(totalMs / 1000 % 60);
        int ms = (int)(totalMs % 1000);
        return string.Create(CultureInfo.InvariantCulture, $"{hours:00}:{minutes:00}:{seconds:00}.{ms:000}");
    }

    public static string DateTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static string DateTimePrecise(DateTimeOffset value) =>
        value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
}
