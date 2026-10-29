using System.Text;
using System.Text.RegularExpressions;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.Metadata;

namespace DataDeveloper.Data.Ai;

/// <summary>Reads table/view structure for the prompt context; abstracted so the builder is testable without a database.</summary>
public interface IAiSchemaSource
{
    Task<IReadOnlyList<DbObjectRef>> ListTablesAndViewsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ColumnModel>> GetColumnsAsync(DbObjectRef table, CancellationToken cancellationToken = default);
    Task<TableKeys> GetKeysAsync(DbObjectRef table, CancellationToken cancellationToken = default);
}

public sealed class SchemaMetadataAiSchemaSource(SchemaMetadataService metadata) : IAiSchemaSource
{
    public async Task<IReadOnlyList<DbObjectRef>> ListTablesAndViewsAsync(CancellationToken cancellationToken = default)
    {
        var objects = new List<DbObjectRef>();
        foreach (var kind in new[] { DbObjectKind.Table, DbObjectKind.View })
        {
            foreach (var item in await metadata.ListObjectsAsync(kind, cancellationToken))
                objects.Add(new DbObjectRef(kind, item.IsDefaultSchema ? null : item.SchemaName, item.Name));
        }

        return objects;
    }

    public Task<IReadOnlyList<ColumnModel>> GetColumnsAsync(DbObjectRef table, CancellationToken cancellationToken = default) =>
        metadata.GetColumnsAsync(table, cancellationToken);

    public Task<TableKeys> GetKeysAsync(DbObjectRef table, CancellationToken cancellationToken = default) =>
        metadata.GetTableKeysAsync(table, cancellationToken);
}

/// <summary>
/// Builds the system prompt: the dialect, a list of the object names and the full structure (columns, keys) of the
/// objects that matter to this message. Only metadata is sent, never row data, and the same code serves every provider
/// because it relies on the provider-agnostic catalog.
/// </summary>
public sealed partial class AiSqlContextBuilder
{
    public const int MaxListedObjects = 300;
    public const int MaxDetailedObjects = 15;

    private readonly IAiSchemaSource _source;
    private readonly DatabaseType _databaseType;
    private IReadOnlyList<DbObjectRef>? _objects;

    public AiSqlContextBuilder(IAiSchemaSource source, DatabaseType databaseType)
    {
        _source = source;
        _databaseType = databaseType;
    }

    /// <summary>Forget the cached object list (call after a schema refresh).</summary>
    public void Invalidate() => _objects = null;

    public async Task<IReadOnlyList<DbObjectRef>> GetObjectsAsync(CancellationToken cancellationToken = default) =>
        _objects ??= (await _source.ListTablesAndViewsAsync(cancellationToken)).OrderBy(o => o.QualifiedName, StringComparer.OrdinalIgnoreCase).ToList();

    /// <param name="relevantText">Text whose identifiers pick the objects described in detail (question, failed statement, attachments).</param>
    /// <param name="explicitObjects">Objects the user attached on purpose; always described.</param>
    public async Task<string> BuildSystemPromptAsync(
        string relevantText,
        IEnumerable<DbObjectRef> explicitObjects,
        CancellationToken cancellationToken = default)
    {
        var prompt = await BuildStableSystemPromptAsync(cancellationToken);
        var structure = await BuildStructureContextAsync(relevantText, explicitObjects, null, cancellationToken);
        return structure.Length == 0 ? prompt : prompt + "\n" + structure;
    }

    /// <summary>
    /// The system prompt that does not depend on the message: the dialect, the rules and the list of object names. It only
    /// changes when the schema does, so the CLI can keep reading it from its cache on every call.
    /// </summary>
    public async Task<string> BuildStableSystemPromptAsync(CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"You are an assistant inside Data Developer, a SQL client. The connected database is {DatabaseName(_databaseType)}; write SQL in that dialect.");
        builder.AppendLine("Be concise. Put every SQL script in a ```sql fenced block. Answer in the language of the user's message.");
        builder.AppendLine("Use only the objects and columns listed below; if you need something that is not listed, say so instead of guessing.");
        builder.AppendLine("Columns are known only for objects described under \"Structure of the objects relevant to this message\" in the conversation. If the request needs the columns of a table that was never described, do not guess them: ask which tables to use and tell the user to mention their names or add them with + > Tables / Views.");
        builder.AppendLine("You cannot run anything: the user reviews and runs scripts themselves.");

        IReadOnlyList<DbObjectRef> objects;
        try
        {
            objects = await GetObjectsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            builder.AppendLine();
            builder.AppendLine($"(The database structure could not be read: {exception.Message})");
            return builder.ToString();
        }

        if (objects.Count == 0)
            return builder.ToString();

        builder.AppendLine();
        builder.AppendLine("Tables and views:");
        builder.AppendLine(string.Join(", ", objects.Take(MaxListedObjects).Select(o => o.QualifiedName)));
        if (objects.Count > MaxListedObjects)
            builder.AppendLine($"(and {objects.Count - MaxListedObjects} more)");

        return builder.ToString();
    }

    /// <summary>
    /// The columns and keys of the objects that matter to this message, which travel with the message itself so the
    /// system prompt stays the same. Objects in <paramref name="alreadyDescribed"/> are skipped because the conversation
    /// already contains their structure. Empty when there is nothing to add.
    /// </summary>
    public async Task<string> BuildStructureContextAsync(
        string relevantText,
        IEnumerable<DbObjectRef> explicitObjects,
        ISet<string>? alreadyDescribed,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DbObjectRef> objects;
        try
        {
            objects = await GetObjectsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return $"(The database structure could not be read: {exception.Message})";
        }

        var pending = SelectDetailed(objects, relevantText, explicitObjects)
            .Where(table => alreadyDescribed is null || !alreadyDescribed.Contains(table.QualifiedName))
            .ToList();
        if (pending.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("Structure of the objects relevant to this message:");
        foreach (var table in pending)
        {
            builder.AppendLine();
            builder.Append(await DescribeAsync(table, cancellationToken));
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Names of the objects whose structure already appears in the given conversation texts.</summary>
    public static ISet<string> FindDescribedObjects(IEnumerable<string?> conversationTexts)
    {
        var described = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var text in conversationTexts)
        {
            if (string.IsNullOrEmpty(text))
                continue;

            foreach (Match match in DescribedHeaderRegex().Matches(text))
                described.Add(match.Groups[1].Value);
        }

        return described;
    }

    public static IReadOnlyList<DbObjectRef> SelectDetailed(
        IReadOnlyList<DbObjectRef> objects,
        string relevantText,
        IEnumerable<DbObjectRef> explicitObjects)
    {
        var selected = new List<DbObjectRef>();

        foreach (var explicitObject in explicitObjects)
        {
            var match = objects.FirstOrDefault(o => SameObject(o, explicitObject)) ?? explicitObject;
            if (!selected.Contains(match))
                selected.Add(match);
        }

        var words = IdentifierRegex().Matches(relevantText).Select(m => m.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in objects)
        {
            if (selected.Count >= MaxDetailedObjects)
                break;

            if (candidate.Name.Length >= 3 && words.Contains(candidate.Name) && !selected.Contains(candidate))
                selected.Add(candidate);
        }

        return selected.Take(MaxDetailedObjects).ToList();
    }

    private async Task<string> DescribeAsync(DbObjectRef table, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{(table.Kind == DbObjectKind.View ? "View" : "Table")} {table.QualifiedName}:");

        try
        {
            foreach (var column in await _source.GetColumnsAsync(table, cancellationToken))
            {
                builder.Append("  - ").Append(column.Name).Append(' ').Append(DescribeType(column));
                if (column.IsPrimaryKey)
                    builder.Append(", primary key");
                if (!column.IsNullable)
                    builder.Append(", not null");
                builder.AppendLine();
            }

            if (table.Kind == DbObjectKind.Table)
            {
                var keys = await _source.GetKeysAsync(table, cancellationToken);
                foreach (var foreignKey in keys.ForeignKeyColumns.GroupBy(k => k.ConstraintName))
                {
                    var ordered = foreignKey.OrderBy(k => k.OrdinalPosition).ToList();
                    var target = ordered[0];
                    var targetName = string.IsNullOrEmpty(target.ReferencedSchemaName)
                        ? target.ReferencedTableName
                        : $"{target.ReferencedSchemaName}.{target.ReferencedTableName}";
                    builder.AppendLine($"  foreign key ({string.Join(", ", ordered.Select(k => k.ColumnName))}) -> {targetName}({string.Join(", ", ordered.Select(k => k.ReferencedColumnName))})");
                }

                foreach (var unique in keys.UniqueConstraintColumns.GroupBy(k => k.ConstraintName))
                    builder.AppendLine($"  unique ({string.Join(", ", unique.OrderBy(k => k.OrdinalPosition).Select(k => k.ColumnName))})");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            builder.AppendLine($"  (structure could not be read: {exception.Message})");
        }

        return builder.ToString();
    }

    private static string DescribeType(ColumnModel column)
    {
        if (column.Length > 0 && column.Length < int.MaxValue)
            return $"{column.DataType}({column.Length})";

        return column.Precision > 0 ? $"{column.DataType}({column.Precision},{column.Scale})" : column.DataType;
    }

    private static bool SameObject(DbObjectRef left, DbObjectRef right) =>
        string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase) &&
        (left.Schema is null || right.Schema is null || string.Equals(left.Schema, right.Schema, StringComparison.OrdinalIgnoreCase));

    public static string DatabaseName(DatabaseType type) => type switch
    {
        DatabaseType.SqlServer => "SQL Server (T-SQL)",
        DatabaseType.Oracle => "Oracle (PL/SQL)",
        DatabaseType.PostgresSql => "PostgreSQL",
        DatabaseType.MySql => "MySQL",
        DatabaseType.SqLite => "SQLite",
        _ => type.ToString()
    };

    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_$#]*")]
    private static partial Regex IdentifierRegex();

    // The header line DescribeAsync writes before each object's columns.
    [GeneratedRegex(@"^(?:Table|View) (\S+):\s*$", RegexOptions.Multiline)]
    private static partial Regex DescribedHeaderRegex();
}
