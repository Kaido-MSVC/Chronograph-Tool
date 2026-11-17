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
    /// <summary>主檔不存在但 .bak 完整，已從 .bak 還原。</summary>
    MissingRestoredFromBackup,
    /// <summary>主檔不存在且 .bak 損壞，已改名保留 .bak 並以預設值開始。</summary>
    MissingBackupCorrupt,
}

public sealed record LoadResult<T>(T Value, LoadOutcome Outcome, string? PreservedPath = null, string? Error = null)
{
    /// <summary>是否應該提示使用者。</summary>
    public bool NeedsAttention => Outcome is not (LoadOutcome.Missing or LoadOutcome.Loaded);
}

/// <summary>
/// JSON 檔案的安全讀寫：先寫入 .tmp 再以 File.Replace 取代原檔並保留 .bak；
/// 解析或結構驗證失敗時不覆蓋，改名保留損壞檔後嘗試從 .bak 還原；主檔不存在時也會先檢查 .bak。
/// </summary>
/// <param name="validate">反序列化後的結構檢查，可順便補正無害的欄位；回傳錯誤訊息表示資料損壞，null 表示通過。</param>
public sealed class JsonFileStore<T>(string path, JsonTypeInfo<T> typeInfo, Func<T> createDefault, Func<T, string?>? validate = null, TimeProvider? time = null)
    where T : class
{
    private readonly TimeProvider time = time ?? TimeProvider.System;

    public string Path { get; } = path;
    public string BackupPath => Path + ".bak";
    private string TempPath => Path + ".tmp";

    public LoadResult<T> Load()
    {
        if (!File.Exists(Path)) return LoadWithoutMainFile();

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
            preserved = PreserveCorruptFile(Path, "corrupt");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(createDefault(), LoadOutcome.Unreadable, Error: ex.Message);
        }

        if (File.Exists(BackupPath))
        {
            string backupText;
            try
            {
                backupText = File.ReadAllText(BackupPath, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 備份暫時讀不到（鎖定、權限）：不寫入，避免之後的存檔把可能完好的備份輪替掉。
                return new(createDefault(), LoadOutcome.Unreadable, preserved, ex.Message);
            }

            if (TryParse(backupText, out var restored, out _))
            {
                TryCopyBackupToMain();
                return new(restored, LoadOutcome.RestoredFromBackup, preserved, parseError);
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

    /// <summary>
    /// 主檔不存在（例如損壞檔已改名、但還原前程式中斷）：有 .bak 就先試著還原，
    /// 避免以空資料開始後，之後的存檔把備份輪替掉。
    /// </summary>
    private LoadResult<T> LoadWithoutMainFile()
    {
        if (!File.Exists(BackupPath)) return new(createDefault(), LoadOutcome.Missing);

        string backupText;
        try
        {
            backupText = File.ReadAllText(BackupPath, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 備份讀不到時不寫入，避免之後的存檔輪替掉備份。
            return new(createDefault(), LoadOutcome.Unreadable, Error: ex.Message);
        }

        if (TryParse(backupText, out var restored, out var parseError))
        {
            TryCopyBackupToMain();
            return new(restored, LoadOutcome.MissingRestoredFromBackup);
        }

        // 備份也損壞：改名保留，避免之後的存檔把它輪替掉。
        try
        {
            var preserved = PreserveCorruptFile(BackupPath, "bak.corrupt");
            return new(createDefault(), LoadOutcome.MissingBackupCorrupt, preserved, parseError);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(createDefault(), LoadOutcome.Unreadable, Error: ex.Message);
        }
    }

    /// <summary>
    /// 把已解析成功的 .bak 複製回主檔。失敗時仍沿用已讀出的還原內容：
    /// 主檔不存在，下次存檔會直接建立主檔，不會動到 .bak。
    /// </summary>
    private void TryCopyBackupToMain()
    {
        try
        {
            File.Copy(BackupPath, Path, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private bool TryParse(string text, out T value, out string? error)
    {
        try
        {
            if (JsonSerializer.Deserialize(text, typeInfo) is { } parsed)
            {
                error = validate?.Invoke(parsed);
                if (error is null)
                {
                    value = parsed;
                    return true;
                }
            }
            else
            {
                error = "檔案內容為 null";
            }
        }
        catch (JsonException ex)
        {
            error = ex.Message;
        }
        value = null!;
        return false;
    }

    /// <summary>把損壞的檔案改名為 name.{label}-yyyyMMdd-HHmmss.json（name 與副檔名取自主檔）。</summary>
    private string PreserveCorruptFile(string source, string label)
    {
        var directory = System.IO.Path.GetDirectoryName(Path)!;
        var name = System.IO.Path.GetFileNameWithoutExtension(Path);
        var extension = System.IO.Path.GetExtension(Path);
        var stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        var target = System.IO.Path.Combine(directory, $"{name}.{label}-{stamp}{extension}");
        for (int i = 2; File.Exists(target); i++)
            target = System.IO.Path.Combine(directory, $"{name}.{label}-{stamp}-{i}{extension}");

        File.Move(source, target);
        return target;
    }
}
