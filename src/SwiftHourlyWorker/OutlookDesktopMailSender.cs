using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SwiftHourlyWorker;

/// <summary>
/// Runs Outlook Classic automation in the signed-in Windows desktop session.
/// Not suitable for Session 0 Windows Services or containers.
/// </summary>
public sealed class OutlookDesktopMailSender(ILogger<OutlookDesktopMailSender> log)
{
    public async Task SendAsync(SqlReport report, ReportOptions options, CancellationToken ct)
    {
        if (options.DryRun)
        {
            log.LogInformation("DRY RUN: {Start:o}-{End:o}, {Count} registros. Sin envío.",
                report.Window.Start, report.Window.End, report.Metrics.TotalRegistros);
            return;
        }
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Outlook Classic COM requiere Windows interactivo.");
        if (!Environment.UserInteractive)
            throw new InvalidOperationException("Se requiere sesión de Windows interactiva, no servicio Session 0.");
        if (options.Recipients.Length == 0 || string.IsNullOrWhiteSpace(options.SenderUserId))
            throw new InvalidOperationException("Configura Report:Recipients y Report:SenderUserId.");

        var script = Path.Combine(AppContext.BaseDirectory, "scripts", "send-outlook-classic.ps1");
        if (!File.Exists(script)) throw new FileNotFoundException("No existe script de Outlook", script);
        var payloadPath = Path.Combine(Path.GetTempPath(), $"swift-outlook-{Guid.NewGuid():N}.json");
        try
        {
            var payload = new
            {
                sender = options.SenderUserId,
                recipients = options.Recipients,
                subject = $"SWIFT | Reporte horario | {report.Metrics.TotalRegistros} registros",
                html = HtmlReport.Render(report, options.MaxEmailDetailRows)
            };
            await File.WriteAllTextAsync(payloadPath, JsonSerializer.Serialize(payload), ct);
            var start = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-STA");
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(script);
            start.ArgumentList.Add("-PayloadPath");
            start.ArgumentList.Add(payloadPath);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo iniciar PowerShell");
            var stdOut = process.StandardOutput.ReadToEndAsync(ct);
            var stdErr = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var output = await stdOut;
            var error = await stdErr;
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Outlook no aceptó el envío (código {process.ExitCode}). {error}");
            log.LogInformation("Outlook Classic aceptó el correo para {Window:o}. {Output}", report.Window.Start, output.Trim());
        }
        finally
        {
            if (File.Exists(payloadPath)) File.Delete(payloadPath);
        }
    }
}
