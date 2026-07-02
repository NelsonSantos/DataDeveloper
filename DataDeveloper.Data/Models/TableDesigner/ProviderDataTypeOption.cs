using DataDeveloper.Data.Enums;

namespace DataDeveloper.Data.Models.TableDesigner;

public sealed class ProviderDataTypeOption
{
    public ProviderDataTypeOption(
        string name,
        bool supportsLength = false,
        int? defaultLength = null,
        bool supportsPrecision = false,
        int? defaultPrecision = null,
        bool supportsScale = false,
        int? defaultScale = null,
        bool supportsIdentity = false)
    {
        Name = name;
        SupportsLength = supportsLength;
        DefaultLength = defaultLength;
        SupportsPrecision = supportsPrecision;
        DefaultPrecision = defaultPrecision;
        SupportsScale = supportsScale;
        DefaultScale = defaultScale;
        SupportsIdentity = supportsIdentity;
    }

    public string Name { get; }
    public bool SupportsLength { get; }
    public int? DefaultLength { get; }
    public bool SupportsPrecision { get; }
    public int? DefaultPrecision { get; }
    public bool SupportsScale { get; }
    public int? DefaultScale { get; }
    public bool SupportsIdentity { get; }

    public override string ToString() => Name;
}

public static class ProviderDataTypeCatalog
{
    public static IReadOnlyList<ProviderDataTypeOption> GetDataTypes(DatabaseType databaseType)
    {
        var types = databaseType switch
        {
            DatabaseType.SqlServer => SqlServerTypes,
            DatabaseType.MySql => MySqlTypes,
            DatabaseType.PostgresSql => PostgresTypes,
            DatabaseType.Oracle => OracleTypes,
            DatabaseType.SqLite => SqLiteTypes,
            _ => Array.Empty<ProviderDataTypeOption>()
        };

        return types
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static ProviderDataTypeOption GetDefaultDataType(DatabaseType databaseType)
    {
        var types = GetDataTypes(databaseType);
        var defaultTypeName = databaseType switch
        {
            DatabaseType.SqlServer => "int",
            DatabaseType.MySql => "int",
            DatabaseType.PostgresSql => "integer",
            DatabaseType.Oracle => "number",
            DatabaseType.SqLite => "integer",
            _ => "varchar"
        };

        return types.FirstOrDefault(type => string.Equals(type.Name, defaultTypeName, StringComparison.OrdinalIgnoreCase))
               ?? types.FirstOrDefault()
               ?? new ProviderDataTypeOption("varchar", supportsLength: true, defaultLength: 255);
    }

    private static readonly ProviderDataTypeOption[] SqlServerTypes =
    [
        new("int", supportsIdentity: true),
        new("bigint", supportsIdentity: true),
        new("smallint", supportsIdentity: true),
        new("tinyint", supportsIdentity: true),
        new("bit"),
        new("decimal", supportsPrecision: true, defaultPrecision: 18, supportsScale: true, defaultScale: 2),
        new("numeric", supportsPrecision: true, defaultPrecision: 18, supportsScale: true, defaultScale: 2),
        new("money"),
        new("float"),
        new("real"),
        new("varchar", supportsLength: true, defaultLength: 255),
        new("varchar(max)"),
        new("nvarchar", supportsLength: true, defaultLength: 255),
        new("nvarchar(max)"),
        new("char", supportsLength: true, defaultLength: 1),
        new("nchar", supportsLength: true, defaultLength: 1),
        new("binary", supportsLength: true, defaultLength: 1),
        new("varbinary", supportsLength: true, defaultLength: 255),
        new("varbinary(max)"),
        new("text"),
        new("ntext"),
        new("xml"),
        new("smallmoney"),
        new("rowversion"),
        new("sql_variant"),
        new("date"),
        new("smalldatetime"),
        new("datetime"),
        new("datetime2", supportsScale: true, defaultScale: 7),
        new("datetimeoffset", supportsScale: true, defaultScale: 7),
        new("time", supportsScale: true, defaultScale: 7),
        new("uniqueidentifier")
    ];

    private static readonly ProviderDataTypeOption[] MySqlTypes =
    [
        new("int", supportsIdentity: true),
        new("int unsigned", supportsIdentity: true),
        new("bigint", supportsIdentity: true),
        new("bigint unsigned", supportsIdentity: true),
        new("mediumint", supportsIdentity: true),
        new("mediumint unsigned", supportsIdentity: true),
        new("smallint", supportsIdentity: true),
        new("smallint unsigned", supportsIdentity: true),
        new("tinyint", supportsIdentity: true),
        new("tinyint unsigned", supportsIdentity: true),
        new("bit"),
        new("bool"),
        new("boolean"),
        new("decimal", supportsPrecision: true, defaultPrecision: 18, supportsScale: true, defaultScale: 2),
        new("decimal unsigned", supportsPrecision: true, defaultPrecision: 18, supportsScale: true, defaultScale: 2),
        new("double"),
        new("double unsigned"),
        new("float"),
        new("float unsigned"),
        new("varchar", supportsLength: true, defaultLength: 255),
        new("char", supportsLength: true, defaultLength: 1),
        new("binary", supportsLength: true, defaultLength: 1),
        new("varbinary", supportsLength: true, defaultLength: 255),
        new("tinytext"),
        new("text"),
        new("mediumtext"),
        new("longtext"),
        new("enum"),
        new("set"),
        new("date"),
        new("datetime", supportsScale: true, defaultScale: 0),
        new("timestamp", supportsScale: true, defaultScale: 0),
        new("time", supportsScale: true, defaultScale: 0),
        new("year"),
        new("json"),
        new("blob"),
        new("tinyblob"),
        new("mediumblob"),
        new("longblob")
    ];

    private static readonly ProviderDataTypeOption[] PostgresTypes =
    [
        new("integer", supportsIdentity: true),
        new("bigint", supportsIdentity: true),
        new("smallint", supportsIdentity: true),
        new("boolean"),
        new("numeric", supportsPrecision: true, defaultPrecision: 18, supportsScale: true, defaultScale: 2),
        new("real"),
        new("double precision"),
        new("varchar", supportsLength: true, defaultLength: 255),
        new("char", supportsLength: true, defaultLength: 1),
        new("text"),
        new("date"),
        new("timestamp"),
        new("timestamp with time zone"),
        new("timestamptz"),
        new("time"),
        new("time with time zone"),
        new("uuid"),
        new("json"),
        new("jsonb"),
        new("bytea")
    ];

    private static readonly ProviderDataTypeOption[] OracleTypes =
    [
        new("number", supportsPrecision: true, defaultPrecision: 18, supportsScale: true, defaultScale: 0, supportsIdentity: true),
        new("integer", supportsIdentity: true),
        new("binary_float"),
        new("binary_double"),
        new("varchar2", supportsLength: true, defaultLength: 255),
        new("nvarchar2", supportsLength: true, defaultLength: 255),
        new("char", supportsLength: true, defaultLength: 1),
        new("nchar", supportsLength: true, defaultLength: 1),
        new("clob"),
        new("date"),
        new("timestamp"),
        new("timestamp with time zone"),
        new("timestamp with local time zone"),
        new("raw", supportsLength: true, defaultLength: 16),
        new("blob")
    ];

    private static readonly ProviderDataTypeOption[] SqLiteTypes =
    [
        new("integer", supportsIdentity: true),
        new("real"),
        new("text"),
        new("blob"),
        new("numeric")
    ];
}
