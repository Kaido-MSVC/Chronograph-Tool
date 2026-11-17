using ChronographTool.Models;
using ChronographTool.ViewModels;

namespace ChronographTool.Tests;

public class CountdownViewModelTests
{
    private sealed class FakeUi : IUiServices
    {
        public int Alerts { get; private set; }
        public Task<bool> ConfirmAsync(string title, string message, string confirmText) => Task.FromResult(true);
        public Task<UnsavedRecordsChoice> AskUnsavedRecordsAsync(string message, bool canRetry) => Task.FromResult(UnsavedRecordsChoice.Discard);
        public Task<Stream?> PickSaveFileAsync(string title, string suggestedName, string typeName, string extension) => Task.FromResult<Stream?>(null);
        public void AlertTimeUp(bool playSound) => Alerts++;
    }

    private static (CountdownViewModel Vm, FakeTimeProvider Time, FakeUi Ui, List<SessionRecord> Saved) Create()
    {
        var time = new FakeTimeProvider();
        var ui = new FakeUi();
        var saved = new List<SessionRecord>();
        var settings = new AppSettings { CountdownHours = 0, CountdownMinutes = 0, CountdownSeconds = 3 };
        var vm = new CountdownViewModel(time, settings, ui, saved.Add, () => { });
        return (vm, time, ui, saved);
    }

    [Fact]
    public void PauseAfterDeadline_BeforeUiTick_CompletesAndAlerts()
    {
        var (vm, time, ui, saved) = Create();
        vm.StartPauseCommand.Execute(null);
        time.AdvanceMs(3050);

        vm.StartPauseCommand.Execute(null);   // UI 還沒刷新就按暫停

        Assert.Equal(SessionStatus.Completed, Assert.Single(saved).Status);
        Assert.True(vm.IsTimeUp);
        Assert.Equal(1, ui.Alerts);
    }

    [Fact]
    public void ResetAfterDeadline_BeforeUiTick_SavesCompleted()
    {
        var (vm, time, _, saved) = Create();
        vm.StartPauseCommand.Execute(null);
        time.AdvanceMs(3050);

        vm.ResetCommand.Execute(null);

        Assert.Equal(SessionStatus.Completed, Assert.Single(saved).Status);
    }

    [Fact]
    public void ExitAfterDeadline_SavesCompleted_ButExitBeforeDeadlineSavesAborted()
    {
        var (vm, time, _, saved) = Create();
        vm.StartPauseCommand.Execute(null);
        time.AdvanceMs(3050);
        vm.SaveOnExit();
        Assert.Equal(SessionStatus.Completed, Assert.Single(saved).Status);

        vm.StartPauseCommand.Execute(null);
        time.AdvanceMs(1500);
        vm.SaveOnExit();
        Assert.Equal(SessionStatus.Aborted, saved[1].Status);
    }
}
