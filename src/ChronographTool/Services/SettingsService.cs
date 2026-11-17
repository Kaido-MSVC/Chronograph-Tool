using ChronographTool.Models;

namespace ChronographTool.Services;

public sealed class SettingsService(string path)
{
    private readonly JsonFileStore<AppSettings> store = new(path, AppJson.Context.AppSettings, () => new AppSettings());
    private bool canSave = true;

    public AppSettings Current { get; private set; } = new();

    public LoadResult<AppSettings> Load()
    {
        var result = store.Load();
        Current = result.Value;
        canSave = result.Outcome != LoadOutcome.Unreadable;
        return result;
    }

    public bool Save()
    {
        if (!canSave) return false;
        try
        {
            store.Save(Current);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
