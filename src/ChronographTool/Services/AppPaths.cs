namespace ChronographTool.Services;

public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chronograph Tool");

    public static string RecordsFile => Path.Combine(DataDirectory, "records.json");
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
}
