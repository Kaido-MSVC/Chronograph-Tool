using ChronographTool.Models;
using ChronographTool.Services;
using ChronographTool.ViewModels;

namespace ChronographTool.Tests;

public sealed class RecordsViewModelTests : IDisposable
{
    private sealed class FakeUi(bool confirm) : IUiServices
    {
        public string? LastMessage { get; private set; }
        public Task<bool> ConfirmAsync(string title, string message, string confirmText)
        {
            LastMessage = message;
            return Task.FromResult(confirm);
        }
        public Task<UnsavedRecordsChoice> AskUnsavedRecordsAsync(string message, bool canRetry) => Task.FromResult(UnsavedRecordsChoice.Discard);
        public Task<Stream?> PickSaveFileAsync(string title, string suggestedName, string typeName, string extension) => Task.FromResult<Stream?>(null);
        public void AlertTimeUp(bool playSound) { }
    }

    private readonly string directory = Path.Combine(Path.GetTempPath(), "ChronographTool.Tests", Guid.NewGuid().ToString("N"));
    private string RecordsPath => Path.Combine(directory, "records.json");

    public RecordsViewModelTests() => Directory.CreateDirectory(directory);

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private static SessionRecord Sample(SessionKind kind) => new()
    {
        Kind = kind,
        Status = SessionStatus.Completed,
        StartTime = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.FromHours(8)),
        EndTime = new DateTimeOffset(2026, 11, 17, 9, 0, 5, TimeSpan.FromHours(8)),
        Duration = TimeSpan.FromSeconds(5),
    };

    private (RecordsViewModel Vm, RecordRepository Repo, FakeUi Ui) Create(bool confirm)
    {
        var repo = new RecordRepository(RecordsPath);
        repo.Load();
        repo.Add(Sample(SessionKind.Stopwatch));
        repo.Add(Sample(SessionKind.Stopwatch));
        repo.Add(Sample(SessionKind.Countdown));
        var ui = new FakeUi(confirm);
        return (new RecordsViewModel(repo, ui, _ => { }), repo, ui);
    }

    [Fact]
    public async Task DeleteAllShown_WithFilter_RemovesOnlyShownRecords()
    {
        var (vm, repo, ui) = Create(confirm: true);
        vm.KindFilter = 1;   // 只顯示碼表
        Assert.Equal(2, vm.Items.Count);

        await vm.DeleteAllShownCommand.ExecuteAsync(null);

        Assert.Contains("目前篩選顯示的 2 筆", ui.LastMessage);
        Assert.Empty(vm.Items);
        Assert.False(vm.DeleteAllShownCommand.CanExecute(null));
        Assert.Equal(SessionKind.Countdown, Assert.Single(repo.Records).Kind);

        vm.KindFilter = 0;
        Assert.Single(vm.Items);
        Assert.True(vm.DeleteAllShownCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeleteAllShown_Cancelled_KeepsRecords()
    {
        var (vm, repo, ui) = Create(confirm: false);
        vm.Current = vm.Items[0];

        await vm.DeleteAllShownCommand.ExecuteAsync(null);

        Assert.Contains("全部 3 筆", ui.LastMessage);
        Assert.Equal(3, vm.Items.Count);
        Assert.Equal(3, repo.Records.Count);
        Assert.NotNull(vm.Current);
    }

    [Fact]
    public async Task DeleteAllShown_WithoutFilter_ClearsEverything_AndCurrent()
    {
        var (vm, repo, _) = Create(confirm: true);
        vm.Current = vm.Items[0];

        await vm.DeleteAllShownCommand.ExecuteAsync(null);

        Assert.Empty(vm.Items);
        Assert.Empty(repo.Records);
        Assert.Null(vm.Current);
        Assert.Equal("顯示 0 筆（共 0 筆）", vm.SummaryText);
    }
}
