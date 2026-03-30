using System.Data;
using System.Diagnostics;
using System.Data.Common;
using Dapper;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Services;

public class StatementExecutor : IStatementExecutor
{
    private readonly IConnectionSettings _connectionSettings;
    private readonly IDatabaseProvider _databaseProvider;
    
    public StatementExecutor(IConnectionSettings connectionSettings)
    {
        _connectionSettings = connectionSettings;
        _databaseProvider = _connectionSettings.GetDatabaseProvider();
    }

    public async Task<IEnumerable<StatementResult>> ExecuteStatement(string sqlStatement, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        try
        {
            var result = new List<StatementResult>();
            var statements = StatementSplitter.SplitStatements(sqlStatement);
            var dapperParameters = CreateParameters(parameters);

            foreach (var statement in statements)
            {
                if (StatementExecutionClassifier.RequiresMaterialization(statement))
                {
                    var materializedResults = await ExecuteMaterializedStatement(statement, sqlStatement, parameters);
                    result.AddRange(materializedResults);
                    continue;
                }

                var watcher = Stopwatch.StartNew();
                var connection = _databaseProvider.GetConnection();
                var reader = await connection.ExecuteReaderAsync(statement, param: dapperParameters, commandType: CommandType.Text);
                watcher.Stop();

                result.Add(new StatementResult(reader, connection, sqlStatement, watcher));
            }

            return result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static DynamicParameters? CreateParameters(IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
            return null;

        var dapperParameters = new DynamicParameters();
        foreach (var parameter in parameters)
        {
            dapperParameters.Add(parameter.Key.TrimStart('@'), parameter.Value);
        }

        return dapperParameters;
    }

    private async Task<IEnumerable<StatementResult>> ExecuteMaterializedStatement(string statement, string originalStatement, IReadOnlyDictionary<string, object?>? parameters)
    {
        var results = new List<StatementResult>();
        await using var connection = _databaseProvider.GetConnection();
        await connection.OpenAsync();
        await using var command = CreateCommand(connection, statement, parameters);
        await using var reader = await command.ExecuteReaderAsync();

        do
        {
            var watcher = Stopwatch.StartNew();

            if (reader.FieldCount > 0)
            {
                var table = ResultSetMaterializer.MaterializeCurrentResult(reader);
                watcher.Stop();
                results.Add(new StatementResult(table.CreateDataReader(), null, originalStatement, watcher, table.Rows.Count));
            }
            else
            {
                watcher.Stop();
                results.Add(new StatementResult(null, null, originalStatement, watcher, reader.RecordsAffected));
            }
        } while (await reader.NextResultAsync());

        return results;
    }

    private static DbCommand CreateCommand(DbConnection connection, string statement, IReadOnlyDictionary<string, object?>? parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = statement;
        command.CommandType = CommandType.Text;

        if (parameters is null)
            return command;

        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = NormalizeParameterName(parameter.Key);
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }

        return command;
    }

    private static string NormalizeParameterName(string name)
    {
        return name.StartsWith("@", StringComparison.Ordinal) ? name : $"@{name}";
    }
}
