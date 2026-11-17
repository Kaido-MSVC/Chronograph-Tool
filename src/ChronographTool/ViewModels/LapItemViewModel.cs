using ChronographTool.Core;
using ChronographTool.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChronographTool.ViewModels;

public sealed partial class LapItemViewModel(LapRecord lap) : ObservableObject
{
    public int Number => lap.Number;
    public string NumberText => $"#{lap.Number}";
    public string SplitText { get; } = TimeFormat.Duration(lap.Split);
    public string CumulativeText { get; } = TimeFormat.Duration(lap.Cumulative);

    [ObservableProperty]
    public partial bool IsFastest { get; set; }

    [ObservableProperty]
    public partial bool IsSlowest { get; set; }

    /// <summary>依最快／最慢圈號更新標示。</summary>
    public static void MarkExtremes(IEnumerable<LapItemViewModel> items, IReadOnlyList<LapRecord> laps)
    {
        var (fastest, slowest) = StopwatchEngine.FindExtremes(laps);
        foreach (var item in items)
        {
            item.IsFastest = item.Number == fastest;
            item.IsSlowest = item.Number == slowest;
        }
    }
}
