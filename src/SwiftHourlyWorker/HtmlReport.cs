using System.Globalization;
using System.Net;
using System.Text;

namespace SwiftHourlyWorker;

public static class HtmlReport
{
    private static string Esc(object? text) => WebUtility.HtmlEncode(Convert.ToString(text, CultureInfo.InvariantCulture) ?? "");

    private static string Table(IEnumerable<string> headings, IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder("<table style='border-collapse:collapse;font-size:12px' border='1' cellpadding='6'><thead><tr>");
        foreach (var h in headings) sb.Append("<th style='background:#002D62;color:#fff'>").Append(Esc(h)).Append("</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            sb.Append("<tr>");
            foreach (var cell in row) sb.Append("<td>").Append(Esc(cell)).Append("</td>");
            sb.Append("</tr>");
        }
        return sb.Append("</tbody></table>").ToString();
    }

    public static string Render(SqlReport r, int maxRows)
    {
        var sb = new StringBuilder("<html><body style='font-family:Arial,sans-serif;color:#253047'>");
        sb.Append("<h2 style='background:#002D62;color:white;padding:16px'>SWIFT Connector - Hourly Report</h2>");
        sb.Append("<p>Período SQL: ").Append(Esc(r.Window.Start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)))
          .Append(" - ").Append(Esc(r.Window.End.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(" (fin exclusivo)</p>");
        sb.Append("<p><b>Registros: ").Append(r.Metrics.TotalRegistros).Append("</b></p>");
        sb.Append("<p><b>Pendientes potenciales:</b> ").Append(r.Metrics.PotencialesPendientes)
          .Append(" | <b>Errores potenciales:</b> ").Append(r.Metrics.PotencialesErrores).Append("</p>");
        foreach (var (heading, data) in new[] { ("Por Status", r.Statuses), ("Por MessageType", r.Types), ("Por ResponseStatus", r.Responses) })
        {
            sb.Append("<h3>").Append(heading).Append("</h3>");
            sb.Append(Table(["Valor", "Total"], data.Select(x => new object?[] { x.Name, x.Total })));
        }
        sb.Append("<h3>Detalle de mensajes SWIFT</h3>");
        var selected = new[] { "Id", "MessageId", "Status", "Sender", "Receiver", "TransactionId", "MessageType", "AcknowledgeTimeStamp", "RowInfo_CreatedOn", "RowInfo_ModifiedOn", "ResponseStatus" };
        sb.Append("<p>Mostrando máximo ").Append(maxRows).Append(" filas; campos sensibles excluidos del correo.</p>");
        sb.Append(Table(selected, r.Details.Take(maxRows).Select(row => selected.Select(k => row.GetValueOrDefault(k)))));
        return sb.Append("<p>Reporte automático.</p></body></html>").ToString();
    }
}
