using System.Text;
using System.Text.Json;
using ChronographTool.Core;
using ChronographTool.Models;

namespace ChronographTool.Services;

public enum ExportFormat
{
    /// <summary>CSV，每筆紀錄一列，計次合併成一欄。</summary>
    CsvRecords,
    /// <summary>CSV，計次明細每圈一列。</summary>
    CsvLaps,
    Json,
}

public static class Exporters
{
    /// <summary>UTF-8 with BOM，讓 Excel 正確辨識中文。</summary>
    public static readonly Encoding CsvEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static string KindText(SessionKind kind) => kind switch
    {
        SessionKind.Stopwatch => "碼表",
        SessionKind.Countdown => "計時器",
        _ => kind.ToString(),
    };

    public static string StatusText(SessionStatus status) => status switch
    {
        SessionStatus.Completed => "完成",
        SessionStatus.Aborted => "中止",
        _ => status.ToString(),
    };

    public static string LapSummary(IEnumerable<LapRecord> laps) =>
        string.Join("; ", laps.Select(l => $"#{l.Number} {TimeFormat.Duration(l.Split)} / {TimeFormat.Duration(l.Cumulative)}"));

    public static string RecordsCsv(IEnumerable<SessionRecord> records)
    {
        var sb = new StringBuilder();
        AppendRow(sb, ["紀錄 ID", "類型", "狀態", "開始時間", "結束時間", "總時長", "設定時長", "計次數", "計次明細", "備註"]);
        foreach (var r in records)
        {
            AppendRow(sb,
            [
                r.Id.ToString(),
                KindText(r.Kind),
                StatusText(r.Status),
                TimeFormat.DateTimePrecise(r.StartTime),
                TimeFormat.DateTimePrecise(r.EndTime),
                TimeFormat.Duration(r.Duration),
                r.TargetDuration is { } target ? TimeFormat.Duration(target) : "",
                r.Kind == SessionKind.Stopwatch ? r.Laps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) : "",
                LapSummary(r.Laps),
                r.Note,
            ]);
        }
        return sb.ToString();
    }

    public static string LapsCsv(IEnumerable<SessionRecord> records)
    {
        var sb = new StringBuilder();
        AppendRow(sb, ["紀錄 ID", "開始時間", "圈號", "單圈時間", "累計時間"]);
        foreach (var r in records)
        {
            foreach (var lap in r.Laps)
            {
                AppendRow(sb,
                [
                    r.Id.ToString(),
                    TimeFormat.DateTimePrecise(r.StartTime),
                    lap.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    TimeFormat.Duration(lap.Split),
                    TimeFormat.Duration(lap.Cumulative),
                ]);
            }
        }
        return sb.ToString();
    }

    public static string Json(IEnumerable<SessionRecord> records) =>
        JsonSerializer.Serialize(new RecordsDocument { Records = [.. records] }, AppJson.Context.RecordsDocument);

    /// <summary>依格式產生要寫入檔案的位元組（CSV 含 BOM，JSON 為 UTF-8 無 BOM）。</summary>
    public static byte[] ToBytes(ExportFormat format, IEnumerable<SessionRecord> records) => format switch
    {
        ExportFormat.CsvRecords => [.. CsvEncoding.GetPreamble(), .. CsvEncoding.GetBytes(RecordsCsv(records))],
        ExportFormat.CsvLaps => [.. CsvEncoding.GetPreamble(), .. CsvEncoding.GetBytes(LapsCsv(records))],
        ExportFormat.Json => new UTF8Encoding(false).GetBytes(Json(records)),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    /// <summary>RFC 4180：含逗號、引號或換行的欄位加上引號，內含的引號重複一次。</summary>
    public static string EscapeCsv(string field)
    {
        if (field.AsSpan().IndexOfAny(",\"\r\n") < 0) return field;
        return "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    /// <summary>
    /// 防止試算表把欄位當成公式（CSV／公式注入）：以 = + - @ 或 Tab、CR 開頭的欄位前面加上單引號。
    /// Excel 開啟時單引號不會顯示，儲存格會以文字呈現。
    /// </summary>
    public static string NeutralizeFormula(string field) =>
        field.Length > 0 && field[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + field : field;

    private static void AppendRow(StringBuilder sb, IReadOnlyList<string> fields)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(EscapeCsv(NeutralizeFormula(fields[i])));
        }
        sb.Append("\r\n");
    }
}
