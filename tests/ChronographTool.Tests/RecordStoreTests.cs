using ChronographTool.Models;
using ChronographTool.Services;

namespace ChronographTool.Tests;

public sealed class RecordStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ChronographTool.Tests", Guid.NewGuid().ToString("N"));
    private string RecordsPath => Path.Combine(directory, "records.json");

    public RecordStoreTests() => Directory.CreateDirectory(directory);

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private static SessionRecord Sample(string note) => new()
    {
        Kind = SessionKind.Stopwatch,
        Status = SessionStatus.Completed,
        StartTime = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.FromHours(8)),
        EndTime = new DateTimeOffset(2026, 11, 17, 9, 0, 5, TimeSpan.FromHours(8)),
        Duration = TimeSpan.FromMilliseconds(4321),
        Laps = [new(1, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)), new(2, TimeSpan.FromMilliseconds(2321), TimeSpan.FromMilliseconds(4321))],
        Note = note,
    };

    [Fact]
    public void SaveAndLoad_RoundTrips_AndKeepsBackupOfPreviousVersion()
    {
        var repo = new RecordRepository(RecordsPath);
        Assert.Equal(LoadOutcome.Missing, repo.Load().Outcome);
        var first = Sample("第一筆，含 \"引號\"");
        Assert.True(repo.Add(first));
        Assert.True(repo.Add(Sample("第二筆")));

        var reloaded = new RecordRepository(RecordsPath);
        Assert.Equal(LoadOutcome.Loaded, reloaded.Load().Outcome);
        Assert.Equal(2, reloaded.Records.Count);
        var r = reloaded.Records[0];
        Assert.Equal(first.Id, r.Id);
        Assert.Equal(first.Note, r.Note);
        Assert.Equal(first.StartTime, r.StartTime);
        Assert.Equal(first.Duration, r.Duration);
        Assert.Equal(first.Laps, r.Laps);

        // 中文直接存成 UTF-8，不轉成 \uXXXX；第二次寫入時已產生 .bak，且不留暫存檔。
        Assert.Contains("第一筆", File.ReadAllText(RecordsPath));
        Assert.True(File.Exists(RecordsPath + ".bak"));
        Assert.False(File.Exists(RecordsPath + ".tmp"));
    }

    [Fact]
    public void CorruptFile_IsPreserved_AndRestoredFromBackup_NeverOverwrittenWithEmptyData()
    {
        var repo = new RecordRepository(RecordsPath);
        repo.Load();
        repo.Add(Sample("備份中的資料"));
        repo.Add(Sample("第二筆"));               // 此時 .bak 只有第一筆
        File.WriteAllText(RecordsPath, "{ \"records\": [ 壞掉");

        var reloaded = new RecordRepository(RecordsPath);
        var result = reloaded.Load();

        Assert.Equal(LoadOutcome.RestoredFromBackup, result.Outcome);
        Assert.Equal("備份中的資料", Assert.Single(reloaded.Records).Note);
        Assert.Equal("{ \"records\": [ 壞掉", File.ReadAllText(result.PreservedPath!));
        Assert.Matches(@"records\.corrupt-\d{8}-\d{6}\.json$", result.PreservedPath);
        Assert.Contains("備份中的資料", File.ReadAllText(RecordsPath));
    }

    [Fact]
    public void CorruptFile_WithoutBackup_IsPreserved_AndNotReplacedUntilNextSave()
    {
        File.WriteAllText(RecordsPath, "not json");

        var repo = new RecordRepository(RecordsPath);
        var result = repo.Load();

        Assert.Equal(LoadOutcome.CorruptUsingDefault, result.Outcome);
        Assert.Empty(repo.Records);
        Assert.Equal("not json", File.ReadAllText(result.PreservedPath!));
        Assert.False(File.Exists(RecordsPath));
    }
}
