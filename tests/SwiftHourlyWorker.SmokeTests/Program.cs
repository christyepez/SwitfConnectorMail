using SwiftHourlyWorker;

var w = HourWindow.PreviousCompleteHour(new DateTime(2026, 10, 15, 11, 41, 0));
Assert(w.Start == new DateTime(2026,10,15,10,0,0), "window start");
Assert(w.End == new DateTime(2026,10,15,11,0,0), "window end");
var r = new SqlReport(w, new Metrics(1,0,0),
    [new SummaryRow("<script>", 1)], [], [],
    [new Dictionary<string, object?> { ["Id"] = 1, ["MessageId"] = "<unsafe>" }]);
var html = HtmlReport.Render(r, 100);
Assert(html.Contains("&lt;unsafe&gt;"), "escaped details");
Assert(!html.Contains("<script>"), "escaped grouping");
var path = Path.Combine(Path.GetTempPath(), "swift-worker-test-" + Guid.NewGuid().ToString("N") + ".json");
var cp = new CheckpointStore();
await cp.WriteAsync(path, w.End, CancellationToken.None);
Assert(await cp.ReadAsync(path, CancellationToken.None) == w.End, "checkpoint roundtrip");
File.Delete(path);
Console.WriteLine("4 smoke checks passed");
static void Assert(bool condition, string name) { if (!condition) throw new Exception("Failed: " + name); }
