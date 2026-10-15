
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SwiftHourlyWorker;

public sealed class HourlyWorker(
    SqlReportRepository sql,
    OutlookDesktopMailSender mail,
    CheckpointStore checkpoints,
    IConfiguration configuration,
    ILogger<HourlyWorker> logger) : BackgroundService
{
    private readonly ReportOptions _options =
        configuration.GetSection("Report").Get<ReportOptions>()
        ?? new ReportOptions();

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        ValidateOptions();

        logger.LogInformation(
            "Worker started. TestMode={TestMode}",
            _options.TestModeEnabled);

        // Ejecucion inmediata opcional.
        if (_options.RunOnStartup)
            await TryRunAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            var next = _options.TestModeEnabled
                ? GetNextTestExecution(now)
                : GetNextNormalExecution(now);

            logger.LogInformation(
                "Next execution: {Next:o}, TestMode={TestMode}",
                next,
                _options.TestModeEnabled);

            try
            {
                await Task.Delay(next - now, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await TryRunAsync(stoppingToken);
        }
    }

    // Modo normal: cada hora al minuto configurado.
    private DateTimeOffset GetNextNormalExecution(
        DateTimeOffset now)
    {
        var next = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            now.Hour,
            _options.ExecutionMinute,
            0,
            TimeSpan.Zero);

        if (next <= now)
            next = next.AddHours(1);

        return next;
    }

    // Modo pruebas: hora y minuto configurados
    // en la zona horaria seleccionada.
    private DateTimeOffset GetNextTestExecution(
        DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(
            _options.TimeZoneId);

        var localNow = TimeZoneInfo.ConvertTime(
            now, zone);

        var nextLocal = new DateTime(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            _options.TestExecutionHour,
            _options.TestExecutionMinute,
            0,
            DateTimeKind.Unspecified);

        if (nextLocal <= localNow.DateTime)
            nextLocal = nextLocal.AddDays(1);

        while (zone.IsInvalidTime(nextLocal))
            nextLocal = nextLocal.AddMinutes(1);

        var offset = zone.IsAmbiguousTime(nextLocal)
            ? zone.GetAmbiguousTimeOffsets(nextLocal).Max()
            : zone.GetUtcOffset(nextLocal);

        return new DateTimeOffset(
            nextLocal,
            offset).ToUniversalTime();
    }

    private void ValidateOptions()
    {
        if (_options.DetailLimit is < 1 or > 5000 ||
            _options.MaxEmailDetailRows is < 0 or > 5000 ||
            _options.MaxCatchUpHours is < 1 or > 168)
        {
            throw new InvalidOperationException(
                "Invalid report limits");
        }

        if (!_options.TestModeEnabled &&
            _options.ExecutionMinute is < 0 or > 59)
        {
            throw new InvalidOperationException(
                "ExecutionMinute must be between 0 and 59");
        }

        if (_options.TestModeEnabled &&
            (_options.TestExecutionHour is < 0 or > 23 ||
             _options.TestExecutionMinute is < 0 or > 59))
        {
            throw new InvalidOperationException(
                "Invalid test execution hour or minute");
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(
            _options.TimeZoneId);

        _ = TimeZoneInfo.FindSystemTimeZoneById(
            _options.SqlTimeZoneId);
    }

    private async Task TryRunAsync(CancellationToken ct)
    {
        try
        {
            await RunAsync(ct);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Hourly run failed; checkpoint unchanged for retry");
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        // Zona horaria en la que SQL almacena fechas.
        var zone = TimeZoneInfo.FindSystemTimeZoneById(
            _options.SqlTimeZoneId);

        var sqlNow = TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow,
            zone).DateTime;

        // Ultima hora completa.
        var latest = HourWindow.PreviousCompleteHour(sqlNow);

        var stateFile = _options.TestModeEnabled
            ? _options.TestStateFile
            : _options.StateFile;

        // Modo pruebas: ignorar checkpoint.
        // Modo normal: recuperar checkpoint existente.
        DateTime? checkpoint = _options.TestModeEnabled
            ? null
            : await checkpoints.ReadAsync(stateFile, ct);

        var start = checkpoint ?? latest.Start;

        if (start > latest.Start)
        {
            logger.LogInformation(
                "No pending hourly windows");
            return;
        }

        var hours = 0;

        while (start <= latest.Start &&
               hours++ < _options.MaxCatchUpHours)
        {
            ct.ThrowIfCancellationRequested();

            var window = new HourWindow(
                start,
                start.AddHours(1));

            logger.LogInformation(
                "Querying SQL: {Start:o} - {End:o}",
                window.Start,
                window.End);

            var report = await RetryAsync(
                () => sql.ReadAsync(
                    window,
                    _options.DetailLimit,
                    ct),
                ct);

            var total = report.Metrics.TotalRegistros;

            logger.LogInformation(
                "SQL returned {Total} records",
                total);

            if (_options.TestModeEnabled)
            {
                // Nunca enviar correo en modo pruebas.
                logger.LogInformation(
                    "TEST MODE: {Start:o}-{End:o}, " +
                    "{Total} registros. Sin envio.",
                    window.Start,
                    window.End,
                    total);
            }
            else if (_options.SendEmptyReports || total > 0)
            {
                // Mantener envio original en modo normal.
                await RetryAsync(
                    async () =>
                    {
                        await mail.SendAsync(
                            report,
                            _options,
                            ct);

                        return true;
                    },
                    ct);
            }

            // El modo pruebas NO modifica checkpoints.
            if (!_options.TestModeEnabled)
            {
                await checkpoints.WriteAsync(
                    stateFile,
                    window.End,
                    ct);
            }

            logger.LogInformation(
                "Completed {Start:o}-{End:o} with {Total} rows",
                window.Start,
                window.End,
                total);

            start = window.End;
        }
    }

    private static async Task<T> RetryAsync<T>(
        Func<Task<T>> action,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception)
                when (attempt < 3 &&
                      !ct.IsCancellationRequested)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(attempt * 5),
                    ct);
            }
        }
    }
}
