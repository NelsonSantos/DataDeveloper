using System.Data.Common;

namespace DataDeveloper.Data.Services.Metadata;

public static class DdlResultReader
{
    /// <summary>
    /// Joins the DDL text of every row in every result set. Rows carrying a "Create ..." column
    /// (MySQL's SHOW CREATE) take the DDL from it; other rows take it from the first column.
    /// </summary>
    public static async Task<string> ReadAsync(DbDataReader reader, CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();

        do
        {
            var createColumnIndex = Enumerable.Range(0, reader.FieldCount)
                .FirstOrDefault(index => reader.GetName(index).StartsWith("Create ", StringComparison.OrdinalIgnoreCase), -1);
            // SHOW CREATE puts the object name in the first column, and the "Create ..." column is
            // NULL when the user cannot see a routine's body, so that row has no DDL to return.
            var ddlColumnIndex = createColumnIndex >= 0 ? createColumnIndex : 0;

            while (await reader.ReadAsync(cancellationToken))
            {
                if (!await reader.IsDBNullAsync(ddlColumnIndex, cancellationToken))
                    parts.Add(reader.GetString(ddlColumnIndex));
            }
        } while (await reader.NextResultAsync(cancellationToken));

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
