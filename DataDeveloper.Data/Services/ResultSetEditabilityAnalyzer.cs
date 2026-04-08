using System.Text.RegularExpressions;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Services;

public static class ResultSetEditabilityAnalyzer
{
    private const string IdentifierPattern = @"(?:\[[^\]]+\]|`[^`]+`|""[^""]+""|[A-Za-z_][A-Za-z0-9_$]*)";

    private static readonly Regex EditableSelectRegex = new(
        $"""
        ^\s*
        select
        \s+
        (?:
            \*
            |
            (?<projectionAlias>{IdentifierPattern})
            \s*\.\s*
            \*
        )
        \s+
        from
        \s+
        (?<table>
            {IdentifierPattern}
            (?:\s*\.\s*{IdentifierPattern})?
        )
        (?:
            \s+
            (?:as\s+)?
            (?<tableAlias>{IdentifierPattern})
        )?
        (?:
            \s+
            where
            \b
            .*?
        )?
        (?:
            \s+
            order
            \s+
            by
            \b
            .*?
        )?
        \s*
        ;?
        \s*$
        """,
        RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    public static ResultSetEditabilityInfo Analyze(
        string statement,
        IReadOnlyCollection<string>? resultColumns = null,
        IReadOnlyCollection<ColumnModel>? tableColumns = null)
    {
        if (string.IsNullOrWhiteSpace(statement))
            return new ResultSetEditabilityInfo(false, null, "Statement is empty.");

        var trimmed = TrimLeadingTrivia(statement);
        var match = EditableSelectRegex.Match(trimmed);
        if (!match.Success)
        {
            return new ResultSetEditabilityInfo(
                false,
                null,
                "Only simple single-table 'select *' statements are editable.");
        }

        var projectionAlias = match.Groups["projectionAlias"].Value;
        var tableAlias = match.Groups["tableAlias"].Value;
        if (!string.IsNullOrWhiteSpace(projectionAlias) &&
            !string.IsNullOrWhiteSpace(tableAlias) &&
            !string.Equals(projectionAlias, tableAlias, StringComparison.OrdinalIgnoreCase))
        {
            return new ResultSetEditabilityInfo(
                false,
                null,
                "Only simple single-table 'select *' statements are editable.");
        }

        var tableName = NormalizeQualifiedName(match.Groups["table"].Value);

        if (tableColumns is null || resultColumns is null)
            return new ResultSetEditabilityInfo(true, tableName, null);

        var primaryKeyColumns = tableColumns
            .Where(column => column.IsPrimaryKey)
            .Select(column => column.Name)
            .ToList();

        if (primaryKeyColumns.Count == 0)
            return new ResultSetEditabilityInfo(false, tableName, "The target table does not have a primary key.");

        var visibleColumns = new HashSet<string>(resultColumns, StringComparer.OrdinalIgnoreCase);
        var missingKeyColumns = primaryKeyColumns
            .Where(column => !visibleColumns.Contains(column))
            .ToList();

        if (missingKeyColumns.Count > 0)
        {
            return new ResultSetEditabilityInfo(
                false,
                tableName,
                $"Primary key columns are missing from the result: {string.Join(", ", missingKeyColumns)}.");
        }

        return new ResultSetEditabilityInfo(true, tableName, null);
    }

    private static string TrimLeadingTrivia(string statement)
    {
        var index = 0;
        while (index < statement.Length)
        {
            while (index < statement.Length && char.IsWhiteSpace(statement[index]))
                index++;

            if (index + 1 < statement.Length && statement[index] == '-' && statement[index + 1] == '-')
            {
                index += 2;
                while (index < statement.Length && statement[index] != '\n')
                    index++;

                continue;
            }

            if (index + 1 < statement.Length && statement[index] == '/' && statement[index + 1] == '*')
            {
                index += 2;
                while (index + 1 < statement.Length && !(statement[index] == '*' && statement[index + 1] == '/'))
                    index++;

                if (index + 1 < statement.Length)
                    index += 2;

                continue;
            }

            break;
        }

        return statement[index..];
    }

    private static string NormalizeQualifiedName(string value)
    {
        var parts = value
            .Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim());

        return string.Join(".", parts);
    }
}
