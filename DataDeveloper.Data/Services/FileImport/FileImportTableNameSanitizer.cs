using System.Text;

namespace DataDeveloper.Data.Services.FileImport;

/// <summary>
/// Turns an arbitrary file name into a safe suggested table name. File names routinely contain
/// characters SQL identifiers should not (dots, spaces, hyphens, ...). A literal dot is the most
/// important one to strip: <see cref="EditableResultSetCommandBuilder"/> treats any dot in a
/// table name as a schema/table separator, so a file like "cities.backup-2026.csv" would
/// otherwise suggest the table name "cities.backup-2026", which then gets misread as table
/// "backup-2026" inside schema "cities" when building INSERT statements.
/// </summary>
public static class FileImportTableNameSanitizer
{
    public static string Sanitize(string fileNameWithoutExtension)
    {
        var builder = new StringBuilder(fileNameWithoutExtension.Length);
        var lastAppendedWasUnderscore = false;

        foreach (var character in fileNameWithoutExtension)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                builder.Append(character);
                lastAppendedWasUnderscore = character == '_';
                continue;
            }

            if (!lastAppendedWasUnderscore)
            {
                builder.Append('_');
                lastAppendedWasUnderscore = true;
            }
        }

        var sanitized = builder.ToString().Trim('_');
        if (sanitized.Length == 0)
            return "imported_table";

        return char.IsDigit(sanitized[0]) ? "_" + sanitized : sanitized;
    }
}
