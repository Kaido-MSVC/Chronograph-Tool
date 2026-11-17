using ChronographTool.Models;

namespace ChronographTool.Services;

/// <summary>紀錄的記憶體清單與 records.json 的同步。每次異動後立即存檔。</summary>
public sealed class RecordRepository
{
    private readonly JsonFileStore<RecordsDocument> store;
    private RecordsDocument document = new();

    public RecordRepository(string path, TimeProvider? time = null)
    {
        store = new(path, AppJson.Context.RecordsDocument, () => new RecordsDocument(), time);
    }

    public IReadOnlyList<SessionRecord> Records => document.Records;

    /// <summary>無法讀取原檔時為 false，避免以空資料覆蓋。</summary>
    public bool CanSave { get; private set; } = true;

    public LoadResult<RecordsDocument> Load()
    {
        var result = store.Load();
        document = result.Value;
        CanSave = result.Outcome != LoadOutcome.Unreadable;
        return result;
    }

    public bool Add(SessionRecord record)
    {
        document.Records.Add(record);
        return Save();
    }

    public bool Remove(IEnumerable<Guid> ids)
    {
        var set = ids.ToHashSet();
        document.Records.RemoveAll(r => set.Contains(r.Id));
        return Save();
    }

    /// <summary>寫入目前內容；失敗或停用寫入時回傳 false。</summary>
    public bool Save()
    {
        if (!CanSave) return false;
        try
        {
            store.Save(document);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
