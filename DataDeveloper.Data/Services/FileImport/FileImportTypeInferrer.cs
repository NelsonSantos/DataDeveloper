using System.Globalization;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models.TableDesigner;

namespace DataDeveloper.Data.Services.FileImport;

public enum FileImportInferredType
{
    Text,
    Integer,
    Decimal,
    DateTime,
    Boolean
}

/// <summary>
/// Infers a generic column type from a set of sample cell values, then resolves that generic
/// type to the closest matching entry in <see cref="ProviderDataTypeCatalog"/> for a given
/// provider. Every <see cref="DatabaseType"/> is handled explicitly (no provider falls back to a
/// shared default) per the project's provider-compatibility rule.
/// </summary>
public static class FileImportTypeInferrer
{
    public static FileImportInferredType InferType(IEnumerable<object?> sampleValues)
    {
        var nonEmptyValues = sampleValues
            .Where(value => value is not null)
            .Where(value => value is not string text || !string.IsNullOrWhiteSpace(text))
            .ToList();

        if (nonEmptyValues.Count == 0)
            return FileImportInferredType.Text;

        if (nonEmptyValues.All(IsBoolean))
            return FileImportInferredType.Boolean;

        if (nonEmptyValues.All(IsInteger))
            return FileImportInferredType.Integer;

        if (nonEmptyValues.All(IsDecimal))
            return FileImportInferredType.Decimal;

        if (nonEmptyValues.All(IsDateTime))
            return FileImportInferredType.DateTime;

        return FileImportInferredType.Text;
    }

    public static TableColumnDefinition SuggestColumn(DatabaseType databaseType, string columnName, IEnumerable<object?> sampleValues)
    {
        var inferredType = InferType(sampleValues);
        var dataTypeName = GetSuggestedDataTypeName(databaseType, inferredType);
        var typeOption = ProviderDataTypeCatalog.GetDataTypes(databaseType)
            .FirstOrDefault(option => string.Equals(option.Name, dataTypeName, StringComparison.OrdinalIgnoreCase));

        return new TableColumnDefinition
        {
            Name = columnName,
            DataType = dataTypeName,
            IsNullable = true,
            Length = typeOption?.SupportsLength == true ? typeOption.DefaultLength : null,
            Precision = typeOption?.SupportsPrecision == true ? typeOption.DefaultPrecision : null,
            Scale = typeOption?.SupportsScale == true ? typeOption.DefaultScale : null
        };
    }

    public static string GetSuggestedDataTypeName(DatabaseType databaseType, FileImportInferredType inferredType)
    {
        return inferredType switch
        {
            FileImportInferredType.Boolean => databaseType switch
            {
                DatabaseType.SqlServer => "bit",
                DatabaseType.MySql => "boolean",
                DatabaseType.PostgresSql => "boolean",
                DatabaseType.Oracle => "number",
                DatabaseType.SqLite => "integer",
                _ => GetTextTypeName(databaseType)
            },
            FileImportInferredType.Integer => databaseType switch
            {
                DatabaseType.SqlServer => "bigint",
                DatabaseType.MySql => "bigint",
                DatabaseType.PostgresSql => "bigint",
                DatabaseType.Oracle => "number",
                DatabaseType.SqLite => "integer",
                _ => GetTextTypeName(databaseType)
            },
            FileImportInferredType.Decimal => databaseType switch
            {
                DatabaseType.SqlServer => "decimal",
                DatabaseType.MySql => "decimal",
                DatabaseType.PostgresSql => "numeric",
                DatabaseType.Oracle => "number",
                DatabaseType.SqLite => "numeric",
                _ => GetTextTypeName(databaseType)
            },
            FileImportInferredType.DateTime => databaseType switch
            {
                DatabaseType.SqlServer => "datetime2",
                DatabaseType.MySql => "datetime",
                DatabaseType.PostgresSql => "timestamp",
                DatabaseType.Oracle => "timestamp",
                DatabaseType.SqLite => "text",
                _ => GetTextTypeName(databaseType)
            },
            _ => GetTextTypeName(databaseType)
        };
    }

    private static string GetTextTypeName(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => "nvarchar",
            DatabaseType.MySql => "varchar",
            DatabaseType.PostgresSql => "varchar",
            DatabaseType.Oracle => "varchar2",
            DatabaseType.SqLite => "text",
            _ => "varchar"
        };
    }

    private static bool IsBoolean(object? value)
    {
        return value switch
        {
            bool => true,
            string text => bool.TryParse(text, out _),
            _ => false
        };
    }

    private static bool IsInteger(object? value)
    {
        return value switch
        {
            bool => false,
            double d => !double.IsInfinity(d) && !double.IsNaN(d) && d == Math.Truncate(d),
            int or long or short => true,
            string text => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            _ => false
        };
    }

    private static bool IsDecimal(object? value)
    {
        return value switch
        {
            bool => false,
            double or float or decimal => true,
            string text => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            _ => false
        };
    }

    private static bool IsDateTime(object? value)
    {
        return value switch
        {
            DateTime => true,
            string text => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => false
        };
    }
}
