using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DataDeveloper.NextGrid.Renderers;

namespace DataDeveloper.Services.GridExport;

/// <summary>
/// Writes a grid's headers/rows to a CSV or XLSX file. CSV is written line by line (streaming,
/// never holding the whole file in memory); XLSX goes through ClosedXML, which builds the whole
/// workbook in memory before saving it — there is no true streaming XLSX writer without a much
/// heavier library, so very large exports cost more memory in XLSX than in CSV.
/// </summary>
public static class GridExportWriter
{
    public static async Task WriteCsvAsync(
        string path,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows,
        IReadOnlyList<Type?> columnTypes,
        CancellationToken cancellationToken = default)
    {
        var formatter = new GridExportValueFormatter(new GridRendererRegistry());
        await using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
        writer.NewLine = "\r\n"; // RFC4180 line ending, independent of the host OS running the app.

        await writer.WriteLineAsync(string.Join(",", headers.Select(EscapeCsvField)));

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fields = new string[headers.Count];
            for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
            {
                var value = columnIndex < row.Count ? row[columnIndex] : null;
                var valueType = columnIndex < columnTypes.Count ? columnTypes[columnIndex] : value?.GetType();
                fields[columnIndex] = EscapeCsvField(formatter.Format(value, valueType));
            }

            await writer.WriteLineAsync(string.Join(",", fields));
        }
    }

    public static Task WriteXlsxAsync(
        string path,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows,
        IReadOnlyList<Type?> columnTypes,
        CancellationToken cancellationToken = default)
    {
        var formatter = new GridExportValueFormatter(new GridRendererRegistry());

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Result");

        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            var headerCell = worksheet.Cell(1, columnIndex + 1);
            headerCell.Value = headers[columnIndex];
            headerCell.Style.Font.Bold = true;
        }

        var rowNumber = 2;
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
            {
                var value = columnIndex < row.Count ? row[columnIndex] : null;
                var valueType = columnIndex < columnTypes.Count ? columnTypes[columnIndex] : value?.GetType();
                SetCellValue(worksheet.Cell(rowNumber, columnIndex + 1), value, valueType, formatter);
            }

            rowNumber++;
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents();

        workbook.SaveAs(path);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Numbers/dates/booleans are written as real typed Excel cells (sortable, summable) rather
    /// than the on-screen formatted text; everything else (null, byte[], unrecognized types)
    /// falls back to <see cref="GridExportValueFormatter"/>'s display text.
    /// </summary>
    private static void SetCellValue(IXLCell cell, object? value, Type? valueType, GridExportValueFormatter formatter)
    {
        switch (value)
        {
            case null or DBNull:
                break;
            case bool boolValue:
                cell.Value = boolValue;
                break;
            case DateTime dateTimeValue:
                cell.Value = dateTimeValue;
                break;
            case DateTimeOffset dateTimeOffsetValue:
                cell.Value = dateTimeOffsetValue.DateTime;
                break;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                break;
            default:
                cell.Value = formatter.Format(value, valueType);
                break;
        }
    }

    private static string EscapeCsvField(string field)
    {
        var needsQuoting = field.IndexOfAny([',', '"', '\r', '\n']) >= 0;
        if (!needsQuoting)
            return field;

        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
