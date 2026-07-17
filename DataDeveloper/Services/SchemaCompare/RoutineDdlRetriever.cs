using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using DataDeveloper.Data;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Services.SchemaCompare;

public static class RoutineDdlRetriever
{
    public static async Task<string> GetDdlAsync(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var query = DatabaseObjectScriptBuilder.TryBuildNativeDdlRetrievalScript(connectionSettings, node)
                    ?? DatabaseObjectScriptBuilder.BuildObjectDdlRetrievalScript(connectionSettings, node);

        var results = await connectionSettings.GetStatementExecutor().ExecuteStatement(query);
        var result = results.First();

        var ddl = string.Empty;
        try
        {
            if (result.HasDataReader)
                ddl = await ReadDdlAsync(result.DataReader!);
        }
        finally
        {
            await result.CloseDataReader();
        }

        return DatabaseObjectScriptBuilder.PostProcessDdl(connectionSettings, node, ddl);
    }

    private static async Task<string> ReadDdlAsync(DbDataReader reader)
    {
        var parts = new List<string>();

        do
        {
            while (await reader.ReadAsync())
            {
                var createColumnIndex = Enumerable.Range(0, reader.FieldCount)
                    .FirstOrDefault(index => reader.GetName(index).StartsWith("Create ", StringComparison.OrdinalIgnoreCase), -1);

                if (createColumnIndex >= 0 && createColumnIndex < reader.FieldCount && !await reader.IsDBNullAsync(createColumnIndex))
                {
                    parts.Add(reader.GetString(createColumnIndex));
                    continue;
                }

                if (!await reader.IsDBNullAsync(0))
                    parts.Add(reader.GetString(0));
            }
        } while (await reader.NextResultAsync());

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
