using System.Text;
using DataDeveloper.Data.Models.FileImport;
using ExcelDataReader;

namespace DataDeveloper.Data.Services.FileImport;

/// <summary>
/// Reads CSV, XLS, and XLSX files through ExcelDataReader. The first row of the file is always
/// treated as the header row. Row streaming (<see cref="ReadAllRows"/>) never materializes the
/// whole file in memory, so large imports do not blow up on file size alone.
/// </summary>
public static class FileImportReader
{
    private static readonly string[] SupportedExtensions = [".csv", ".xls", ".xlsx"];

    static FileImportReader()
    {
        // ExcelDataReader needs this for legacy .xls code pages and some non-UTF8 CSV files;
        // .NET Core does not register it by default the way .NET Framework did.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static bool IsSupportedFile(string filePath)
    {
        return SupportedExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());
    }

    public static FileImportPreview ReadPreview(string filePath, int sampleRowCount)
    {
        var headers = new List<string>();
        var sampleRows = new List<IReadOnlyList<object?>>();
        var isFirstRow = true;

        foreach (var row in ReadRawRows(filePath))
        {
            if (isFirstRow)
            {
                headers.AddRange(BuildHeaderNames(row));
                isFirstRow = false;
                continue;
            }

            if (sampleRows.Count >= sampleRowCount)
                break;

            sampleRows.Add(row);
        }

        return new FileImportPreview { Headers = headers, SampleRows = sampleRows };
    }

    /// <summary>Streams every data row (header row excluded) as they are read from disk.</summary>
    public static IEnumerable<object?[]> ReadAllRows(string filePath)
    {
        var isFirstRow = true;
        foreach (var row in ReadRawRows(filePath))
        {
            if (isFirstRow)
            {
                isFirstRow = false;
                continue;
            }

            yield return row;
        }
    }

    private static IEnumerable<string> BuildHeaderNames(IReadOnlyList<object?> headerRow)
    {
        for (var index = 0; index < headerRow.Count; index++)
        {
            var text = headerRow[index]?.ToString()?.Trim();
            yield return string.IsNullOrEmpty(text) ? $"Column{index + 1}" : text;
        }
    }

    private static IEnumerable<object?[]> ReadRawRows(string filePath)
    {
        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = CreateReader(filePath, stream);

        while (reader.Read())
        {
            var row = new object?[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
                row[index] = reader.IsDBNull(index) ? null : reader.GetValue(index);

            yield return row;
        }
    }

    private static IExcelDataReader CreateReader(string filePath, Stream stream)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".csv" => ExcelReaderFactory.CreateCsvReader(stream),
            ".xls" or ".xlsx" => ExcelReaderFactory.CreateReader(stream),
            _ => throw new NotSupportedException(
                $"Unsupported file extension '{extension}'. Only .csv, .xls, and .xlsx are supported.")
        };
    }
}
