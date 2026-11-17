using System.Text;
using ChronographTool.Models;
using ChronographTool.Services;

namespace ChronographTool.Tests;

public class CsvExportTests
{
    private static readonly SessionRecord Stopwatch = new()
    {
        Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        Kind = SessionKind.Stopwatch,
        Status = SessionStatus.Completed,
        StartTime = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 11, 17))),
        EndTime = new DateTimeOffset(2026, 11, 17, 9, 0, 3, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 11, 17))),
        Duration = TimeSpan.FromMilliseconds(3500),
        Laps = [new(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)), new(2, TimeSpan.FromMilliseconds(2500), TimeSpan.FromMilliseconds(3500))],
        Note = "跑步, 第 \"1\" 組\r\n第二行",
    };

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("中文備註", "中文備註")]
    public void EscapeCsv_FollowsRfc4180(string input, string expected) =>
        Assert.Equal(expected, Exporters.EscapeCsv(input));

    [Fact]
    public void RecordsCsv_HasBom_Header_AndEscapedNote()
    {
        var bytes = Exporters.ToBytes(ExportFormat.CsvRecords, [Stopwatch]);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);

        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.StartsWith("紀錄 ID,類型,狀態,開始時間,結束時間,總時長,設定時長,計次數,計次明細,備註\r\n", text);
        Assert.Contains(
            "11111111-2222-3333-4444-555555555555,碼表,完成,2026-11-17 09:00:00.000,2026-11-17 09:00:03.000,00:00:03.500,,2," +
            "#1 00:00:01.000 / 00:00:01.000; #2 00:00:02.500 / 00:00:03.500," +
            "\"跑步, 第 \"\"1\"\" 組\r\n第二行\"\r\n", text);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+SUM(A1)", "'+SUM(A1)")]
    [InlineData("-5 分鐘", "'-5 分鐘")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("\tx", "'\tx")]
    [InlineData("一般 = 備註", "一般 = 備註")]
    [InlineData("", "")]
    public void NeutralizeFormula_PrefixesFormulaTriggers(string input, string expected) =>
        Assert.Equal(expected, Exporters.NeutralizeFormula(input));

    [Fact]
    public void RecordsCsv_NeutralizesFormulaInNote()
    {
        var record = new SessionRecord { Kind = SessionKind.Countdown, Note = "=HYPERLINK(\"x\",\"y\")" };
        var text = Encoding.UTF8.GetString(Exporters.ToBytes(ExportFormat.CsvRecords, [record])[3..]);
        Assert.EndsWith(",\"'=HYPERLINK(\"\"x\"\",\"\"y\"\")\"\r\n", text);
    }

    [Fact]
    public void LapsCsv_HasOneRowPerLap()
    {
        var text = Encoding.UTF8.GetString(Exporters.ToBytes(ExportFormat.CsvLaps, [Stopwatch])[3..]);
        Assert.Equal(
            "紀錄 ID,開始時間,圈號,單圈時間,累計時間\r\n" +
            "11111111-2222-3333-4444-555555555555,2026-11-17 09:00:00.000,1,00:00:01.000,00:00:01.000\r\n" +
            "11111111-2222-3333-4444-555555555555,2026-11-17 09:00:00.000,2,00:00:02.500,00:00:03.500\r\n",
            text);
    }
}
