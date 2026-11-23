using System.Globalization;
using Avalonia.Data.Converters;

namespace ChronographTool.Views;

/// <summary>把「HH:MM:SS.mmm」拆成主體與毫秒兩段，讓進度環中央可以大小字分開顯示。</summary>
public static class TimeParts
{
    public static readonly IValueConverter Main =
        new FuncValueConverter<string?, string>(s => s is null ? "" : s.LastIndexOf('.') is var i and >= 0 ? s[..i] : s);

    public static readonly IValueConverter Fraction =
        new FuncValueConverter<string?, string>(s => s is null ? "" : s.LastIndexOf('.') is var i and >= 0 ? s[i..] : "");
}

/// <summary>進度環的弧度換算（度，從 12 點鐘方向順時針）。</summary>
public static class RingAngles
{
    /// <summary>倒數：Progress 是已經過的百分比，弧線顯示剩餘部分。</summary>
    public static readonly IValueConverter Remaining =
        new FuncValueConverter<double, double>(progress => Math.Clamp(100 - progress, 0, 100) * 3.6);

    /// <summary>碼表：依目前這一分鐘內走到的秒數畫弧，像秒針繞一圈。</summary>
    public static readonly IValueConverter SecondHand = new FuncValueConverter<string?, double>(s =>
    {
        if (s is null || s.LastIndexOf(':') is not (var i and >= 0)) return 0;
        return double.TryParse(s.AsSpan(i + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
            ? Math.Clamp(seconds, 0, 60) * 6
            : 0;
    });
}
