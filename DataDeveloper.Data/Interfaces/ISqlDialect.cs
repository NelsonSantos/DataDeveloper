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
    /// Writes the identifier in the form used by generated DDL. Same as <see cref="QuoteIdentifier"/>
    /// except on Oracle, where regular non-reserved identifiers are left unquoted and upper-cased.
    /// </summary>
    string FormatIdentifier(string identifier);

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
