using System.Text.Json;

namespace SwiftHourlyWorker;

public sealed class CheckpointStore
{
    public async Task<DateTime?> ReadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return null;
        var data = JsonSerializer.Deserialize<Checkpoint>(await File.ReadAllTextAsync(path, ct));
        return data?.LastCompletedEnd;
    }

    public async Task WriteAsync(string path, DateTime end, CancellationToken ct)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var tmp = full + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(new Checkpoint(end)), ct);
        File.Move(tmp, full, true);
    }
    private sealed record Checkpoint(DateTime LastCompletedEnd);
}
