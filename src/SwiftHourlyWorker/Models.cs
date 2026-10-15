namespace SwiftHourlyWorker;

public sealed record HourWindow(DateTime Start, DateTime End)
{
    public static HourWindow PreviousCompleteHour(DateTime now)
    {
        var end = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Kind);
        return new HourWindow(end.AddHours(-1), end);
    }
}
public sealed record SummaryRow(string Name, long Total);
public sealed record Metrics(long TotalRegistros, long PotencialesPendientes, long PotencialesErrores);
public sealed record SqlReport(HourWindow Window, Metrics Metrics,
    IReadOnlyList<SummaryRow> Statuses, IReadOnlyList<SummaryRow> Types,
    IReadOnlyList<SummaryRow> Responses, IReadOnlyList<Dictionary<string, object?>> Details);
public sealed class ReportOptions
{
    public string[] Recipients { get; set; } = [];
    public string SenderUserId { get; set; } = "";
    public bool RunOnStartup { get; set; }
    public bool DryRun { get; set; } = true;
    public int DetailLimit { get; set; } = 5000;
    public int MaxEmailDetailRows { get; set; } = 100;
    public string TimeZoneId { get; set; } = "UTC";
    public string SqlTimeZoneId { get; set; } = "UTC";
    public string StateFile { get; set; } = "state/checkpoint.json";
    public int MaxCatchUpHours { get; set; } = 24;
    public bool SendEmptyReports { get; set; } = true;
    public int ExecutionMinute { get; set; } = 0;
    public bool TestModeEnabled { get; set; } = false;
    public int TestExecutionHour { get; set; } = 16;
    public int TestExecutionMinute { get; set; } = 10;
    public string TestStateFile { get; set; } =  "state/checkpoint-prueba.json";

    public bool TestSendEmail { get; set; } = false;
    public string TestEmailRecipient { get; set; } = string.Empty;
}
