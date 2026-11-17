namespace ChronographTool.Models;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    // 視窗一般狀態下的位置（實體像素）與工作區大小（DIP）。
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    public int SelectedTab { get; set; }
    public bool SoundEnabled { get; set; } = true;
    public int CountdownHours { get; set; }
    public int CountdownMinutes { get; set; } = 5;
    public int CountdownSeconds { get; set; }
}
