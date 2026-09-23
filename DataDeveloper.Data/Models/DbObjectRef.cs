using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;

namespace DataDeveloper.Data.Models;

/// <summary>
/// Identifies a database object by kind, optional schema and name, with delimiters removed.
/// A null <see cref="Schema"/> means the name was not qualified and the provider's default applies.
/// </summary>
public sealed record DbObjectRef(DbObjectKind Kind, string? Schema, string Name)
{
    public string QualifiedName => Schema is null ? Name : $"{Schema}.{Name}";

    /// <summary>
    /// Parses a possibly qualified name; when it has more than two parts, the last two are kept
    /// as schema and name.
    /// </summary>
    public static DbObjectRef Parse(DbObjectKind kind, string qualifiedName, ISqlDialect dialect)
    {
        var parts = dialect.SplitQualifiedName(qualifiedName);
        return parts.Count switch
        {
            0 => new DbObjectRef(kind, null, qualifiedName.Trim()),
            1 => new DbObjectRef(kind, null, parts[0]),
            _ => new DbObjectRef(kind, parts[^2], parts[^1])
        };
    }

    public static DbObjectRef? FromSchemaNode(SchemaNode node, ISqlDialect dialect)
    {
        DbObjectKind? kind = node.NodeType switch
        {
            NodeType.Table => DbObjectKind.Table,
            NodeType.View => DbObjectKind.View,
            NodeType.Procedure => DbObjectKind.Procedure,
            NodeType.Function => DbObjectKind.Function,
            _ => null
        };

        return kind is null ? null : Parse(kind.Value, node.Name, dialect);
    }
}
