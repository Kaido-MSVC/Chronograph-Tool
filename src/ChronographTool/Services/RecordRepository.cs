using System.Text.Json;
using ChronographTool.Models;

namespace ChronographTool.Services;

/// <summary>紀錄的記憶體清單與 records.json 的同步。每次異動後立即存檔，失敗時保留「未儲存」狀態供之後重試。</summary>
public sealed class RecordRepository
{
    private readonly JsonFileStore<RecordsDocument> store;
    private RecordsDocument document = new();

    public RecordRepository(string path, TimeProvider? time = null)
    {
        store = new(path, AppJson.Context.RecordsDocument, () => new RecordsDocument(), RecordsDocument.Validate, time);
    }

    public IReadOnlyList<SessionRecord> Records => document.Records;

    /// <summary>無法讀取原檔時為 false，避免以空資料覆蓋。</summary>
    public bool CanSave { get; private set; } = true;

    /// <summary>記憶體中的內容有尚未成功寫入 records.json 的變更。</summary>
    public bool HasUnsavedChanges { get; private set; }

    public LoadResult<RecordsDocument> Load()
    {
        var result = store.Load();
        document = result.Value;
        CanSave = result.Outcome != LoadOutcome.Unreadable;
        HasUnsavedChanges = false;
        return result;
    }

    public bool Add(SessionRecord record)
    {
        document.Records.Add(record);
        HasUnsavedChanges = true;
        return Save();
    }

    public bool Remove(IEnumerable<Guid> ids)
    {
        var set = ids.ToHashSet();
        if (document.Records.RemoveAll(r => set.Contains(r.Id)) > 0) HasUnsavedChanges = true;
        return Save();
    }

    /// <summary>標記有變更（例如直接修改了紀錄的備註）。</summary>
    public void MarkChanged() => HasUnsavedChanges = true;

    /// <summary>寫入目前內容；失敗或停用寫入時回傳 false，並維持未儲存狀態。</summary>
    public bool Save()
    {
        if (!CanSave) return false;
        try
        {
            store.Save(document);
            HasUnsavedChanges = false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// 把目前的完整內容另存到指定串流（格式與 records.json 相同，可改名後放回資料夾還原）。
    /// 不會清除未儲存狀態，因為 records.json 本身仍未更新。
    /// </summary>
    public async Task WriteCopyToAsync(Stream stream)
    {
        if (stream.CanSeek) stream.SetLength(0);
        await JsonSerializer.SerializeAsync(stream, document, AppJson.Context.RecordsDocument);
        await stream.FlushAsync();
    }
}
