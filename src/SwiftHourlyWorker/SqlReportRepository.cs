using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SwiftHourlyWorker;

public sealed class SqlReportRepository(IConfiguration configuration)
{
    private const string Filter = "((RowInfo_CreatedOn >= @start AND RowInfo_CreatedOn < @end) OR (RowInfo_ModifiedOn >= @start AND RowInfo_ModifiedOn < @end))";
    private const string Table = "[swift-connector].[SwiftMessages]";
    private readonly string _connection = configuration["Sql:ConnectionString"]
        ?? throw new InvalidOperationException("Missing Sql:ConnectionString");
    private readonly int _timeout = configuration.GetValue("Sql:CommandTimeoutSeconds", 90);

    public async Task<SqlReport> ReadAsync(HourWindow window, int detailLimit, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connection);
        await conn.OpenAsync(ct);
        var details = await ReadDetails(conn, window, detailLimit, ct);
        var status = await Group(conn, window, "Status", ct);
        var types = await Group(conn, window, "MessageType", ct);
        var responses = await Group(conn, window, "ResponseStatus", ct);
        var metrics = await ReadMetrics(conn, window, ct);
        return new SqlReport(window, metrics, status, types, responses, details);
    }

    private SqlCommand Command(SqlConnection connection, string sql, HourWindow w)
    {
        var cmd = new SqlCommand(sql, connection) { CommandTimeout = _timeout };
        cmd.Parameters.Add("@start", SqlDbType.DateTime2).Value = w.Start;
        cmd.Parameters.Add("@end", SqlDbType.DateTime2).Value = w.End;
        return cmd;
    }

    private async Task<IReadOnlyList<Dictionary<string, object?>>> ReadDetails(SqlConnection conn, HourWindow w, int max, CancellationToken ct)
    {
        const string columns = "Id, MessageId, Status, Sender, Receiver, TransactionId, MessageType, MessageFromSwift, MessageToPeopleSoft, AcknowledgeTimeStamp, RowInfo_CreatedOn, RowInfo_ModifiedOn, RowInfo_User, RowInfo_RowVersion, ResponseStatus, AccountNumber";
        using var cmd = Command(conn, $"SELECT TOP (@limit) {columns} FROM {Table} WHERE {Filter} ORDER BY Id DESC", w);
        cmd.Parameters.Add("@limit", SqlDbType.Int).Value = max;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
                row[reader.GetName(i)] = value switch
                {
                    byte[] bytes => Convert.ToHexString(bytes),
                    _ => value
                };
            }
            result.Add(row);
        }
        return result;
    }

    private async Task<IReadOnlyList<SummaryRow>> Group(SqlConnection conn, HourWindow w, string column, CancellationToken ct)
    {
        // Only values in this fixed allowlist can become SQL identifiers.
        if (column is not ("Status" or "MessageType" or "ResponseStatus")) throw new ArgumentOutOfRangeException(nameof(column));
        using var cmd = Command(conn, $"SELECT COALESCE(CONVERT(nvarchar(200), [{column}]),N'(NULL)') AS Label, COUNT_BIG(*) AS Total FROM {Table} WHERE {Filter} GROUP BY [{column}] ORDER BY Total DESC", w);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<SummaryRow>();
        while (await r.ReadAsync(ct)) rows.Add(new SummaryRow(r.GetString(0), r.GetInt64(1)));
        return rows;
    }

    private async Task<Metrics> ReadMetrics(SqlConnection conn, HourWindow w, CancellationToken ct)
    {
        var sql = $@"SELECT COUNT_BIG(*) TotalRegistros,
            COALESCE(SUM(CASE WHEN UPPER(COALESCE(CONVERT(nvarchar(200),Status),'')) LIKE '%PEND%'
               OR UPPER(COALESCE(CONVERT(nvarchar(200),Status),'')) IN ('WAITING','QUEUED','IN_PROGRESS') THEN 1 ELSE 0 END),0) PotencialesPendientes,
            COALESCE(SUM(CASE WHEN UPPER(COALESCE(CONVERT(nvarchar(200),Status),'')) LIKE '%ERR%'
                OR UPPER(COALESCE(CONVERT(nvarchar(200),Status),'')) IN ('FAILED','REJECTED')
                OR UPPER(COALESCE(CONVERT(nvarchar(200),ResponseStatus),'')) LIKE '%ERR%'
                OR UPPER(COALESCE(CONVERT(nvarchar(200),ResponseStatus),'')) IN ('FAILED','REJECTED') THEN 1 ELSE 0 END),0) PotencialesErrores
            FROM {Table} WHERE {Filter}";
        using var cmd = Command(conn, sql, w);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("No metrics row");
        return new Metrics(reader.GetInt64(0), Convert.ToInt64(reader.GetValue(1)), Convert.ToInt64(reader.GetValue(2)));
    }
}
