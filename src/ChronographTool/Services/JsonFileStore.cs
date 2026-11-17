using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ChronographTool.Services;

public enum LoadOutcome
{
    /// <summary>檔案不存在，使用預設值。</summary>
    Missing,
    Loaded,
    /// <summary>主檔損壞，已改名保留並從 .bak 還原。</summary>
    RestoredFromBackup,
    /// <summary>主檔損壞且沒有可用的備份，已改名保留並以預設值開始。</summary>
    CorruptUsingDefault,
    /// <summary>檔案無法讀取（被鎖住或權限不足），以預設值開始，且停止寫回避免覆蓋。</summary>
    Unreadable,
}

public sealed record LoadResult<T>(T Value, LoadOutcome Outcome, string? PreservedPath = null, string? Error = null)
{
    /// <summary>是否應該提示使用者。</summary>
    public bool NeedsAttention => Outcome is LoadOutcome.RestoredFromBackup or LoadOutcome.CorruptUsingDefault or LoadOutcome.Unreadable;
}

/// <summary>
/// JSON 檔案的安全讀寫：先寫入 .tmp 再以 File.Replace 取代原檔並保留 .bak；
/// 解析失敗時不覆蓋，改名保留損壞檔後嘗試從 .bak 還原。
/// </summary>
public sealed class JsonFileStore<T>(string path, JsonTypeInfo<T> typeInfo, Func<T> createDefault, TimeProvider? time = null)
    where T : class
{
    private readonly TimeProvider time = time ?? TimeProvider.System;

    public string Path { get; } = path;
    public string BackupPath => Path + ".bak";
    private string TempPath => Path + ".tmp";

    public LoadResult<T> Load()
    {
        if (!File.Exists(Path)) return new(createDefault(), LoadOutcome.Missing);

        string text;
        try
        {
            text = File.ReadAllText(Path, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(createDefault(), LoadOutcome.Unreadable, Error: ex.Message);
        }

        if (TryParse(text, out var value, out var parseError)) return new(value, LoadOutcome.Loaded);

        // 主檔損壞：先改名保留，絕不用空資料覆蓋。
        string preserved;
        try
        {
            preserved = PreserveCorruptFile();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(createDefault(), LoadOutcome.Unreadable, Error: ex.Message);
        }

        if (File.Exists(BackupPath))
        {
            try
            {
                var backupText = File.ReadAllText(BackupPath, Encoding.UTF8);
                if (TryParse(backupText, out var restored, out _))
                {
                    File.Copy(BackupPath, Path, overwrite: false);
                    return new(restored, LoadOutcome.RestoredFromBackup, preserved, parseError);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 備份不可用時改用預設值；損壞檔已保留。
            }
        }

        return new(createDefault(), LoadOutcome.CorruptUsingDefault, preserved, parseError);
    }

    public void Save(T value)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);

        using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(Path))
            File.Replace(TempPath, Path, BackupPath, ignoreMetadataErrors: true);
        else
            File.Move(TempPath, Path);
    }

    private bool TryParse(string text, out T value, out string? error)
    {
        try
        {
            if (JsonSerializer.Deserialize(text, typeInfo) is { } parsed)
            {
                value = parsed;
                error = null;
                return true;
            }
            error = "檔案內容為 null";
        }
        catch (JsonException ex)
        {
            error = ex.Message;
        }
        value = null!;
        return false;
    }

    /// <summary>把損壞的主檔改名為 name.corrupt-yyyyMMdd-HHmmss.json。</summary>
    private string PreserveCorruptFile()
    {
        var directory = System.IO.Path.GetDirectoryName(Path)!;
        var name = System.IO.Path.GetFileNameWithoutExtension(Path);
        var extension = System.IO.Path.GetExtension(Path);
        var stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        var target = System.IO.Path.Combine(directory, $"{name}.corrupt-{stamp}{extension}");
        for (int i = 2; File.Exists(target); i++)
            target = System.IO.Path.Combine(directory, $"{name}.corrupt-{stamp}-{i}{extension}");

        File.Move(Path, target);
        return target;
    }
}
