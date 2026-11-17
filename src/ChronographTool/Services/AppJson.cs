using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChronographTool.Models;

namespace ChronographTool.Services;

[JsonSerializable(typeof(RecordsDocument))]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

internal static class AppJson
{
    /// <summary>
    /// 縮排、camelCase、列舉存成字串。檔案只在本機讀寫、不嵌入 HTML，
    /// 因此用寬鬆跳脫，讓中文與引號保持可讀（不轉成 \uXXXX）。
    /// </summary>
    public static AppJsonContext Context { get; } = new(new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    });
}
