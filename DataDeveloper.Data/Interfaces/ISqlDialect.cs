namespace DataDeveloper.Data.Interfaces;

/// <summary>
/// Provider-specific rules for writing identifiers and parameter references into SQL text.
/// </summary>
public interface ISqlDialect
{
    /// <summary>
    /// Always delimits the identifier and keeps its case exactly as given, which is the right
    /// form for names read back from the database catalog.
    /// </summary>
    string QuoteIdentifier(string identifier);

    /// <summary>
    /// Writes a name typed by the user (for example in the table designer) in the form used by
    /// generated DDL. Same as <see cref="QuoteIdentifier"/> except on Oracle, where regular
    /// non-reserved identifiers are left unquoted and upper-cased, and a name already written
    /// in double quotes is kept exactly as typed.
    /// </summary>
    string FormatIdentifier(string identifier);

    /// <summary>
    /// Returns the name to pass to <see cref="FormatIdentifier"/> so that it refers to
    /// <paramref name="storedName"/>, a name as stored in the database catalog. On Oracle, a
    /// name with lower-case letters comes back in double quotes; otherwise it is unchanged.
    /// </summary>
    string ToFormattableName(string storedName);

    /// <summary>
    /// Splits a name as written in SQL text and returns each part as the database stores it:
    /// delimited parts unchanged, undelimited parts folded the way the database folds them
    /// (upper case on Oracle, lower case on PostgreSQL, unchanged elsewhere).
    /// </summary>
    IReadOnlyList<string> ResolveQualifiedName(string sqlName);

    /// <summary>
    /// Splits a possibly qualified name (<c>schema.table</c>) and delimits each part with
    /// <see cref="QuoteIdentifier"/>.
    /// </summary>
    string QuoteQualifiedName(string qualifiedName);

    /// <summary>
    /// Splits a qualified name on dots outside <c>[]</c>, <c>""</c> or <c>``</c> delimiters,
    /// removing the delimiters and surrounding whitespace and skipping empty parts.
    /// </summary>
    IReadOnlyList<string> SplitQualifiedName(string qualifiedName);

    string FormatParameterReference(string parameterName);
}
