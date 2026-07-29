using DataDeveloper.Data.Models.TableDesigner;

namespace DataDeveloper.Data.Models.FileImport;

public enum FileImportTargetMode
{
    NewTable,
    ExistingTable
}

public sealed class FileImportPreview
{
    public IReadOnlyList<string> Headers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyList<object?>> SampleRows { get; init; } = Array.Empty<IReadOnlyList<object?>>();
}

public sealed class FileImportColumnMapping
{
    public FileImportColumnMapping(int sourceColumnIndex, string sourceColumnName)
    {
        SourceColumnIndex = sourceColumnIndex;
        SourceColumnName = sourceColumnName;
    }

    public int SourceColumnIndex { get; }
    public string SourceColumnName { get; }
    public bool IsIncluded { get; set; } = true;

    /// <summary>
    /// Used when the target mode is <see cref="FileImportTargetMode.NewTable"/>: the suggested
    /// (and user-editable) column definition to create for this file column.
    /// </summary>
    public TableColumnDefinition? NewColumn { get; set; }

    /// <summary>
    /// Used when the target mode is <see cref="FileImportTargetMode.ExistingTable"/>: the name of
    /// the existing table column this file column maps to, or null/empty to skip the column.
    /// </summary>
    public string? TargetColumnName { get; set; }
}

public sealed class FileImportResult
{
    public int RowsImported { get; init; }
    public int RowsFailed { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
