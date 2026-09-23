namespace DataDeveloper.Data.Models;

/// <summary>
/// How to read a table's check constraints. Usually <see cref="Statement"/> returns them directly
/// (ConstraintName, Definition). When the database keeps them only in the table's DDL,
/// <see cref="Statement"/> returns that DDL text and <see cref="ParseTableDdl"/> extracts them.
/// </summary>
public sealed record CheckConstraintsQuery(
    string Statement,
    Func<string, IReadOnlyList<CheckConstraintModel>>? ParseTableDdl = null);
