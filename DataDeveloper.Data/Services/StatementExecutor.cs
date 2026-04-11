using System.Data;
using System.Diagnostics;
using System.Data.Common;
using System.Threading;
using Dapper;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Services;

public class StatementExecutor : IStatementExecutor
{
    private readonly IConnectionSettings _connectionSettings;
    private readonly IDatabaseProvider _databaseProvider;
    private readonly object _activeCommandLock = new();
    private DbCommand? _activeCommand;
    private int _cancelRequested;
    
    public StatementExecutor(IConnectionSettings connectionSettings)
    {
        _connectionSettings = connectionSettings;
        _databaseProvider = _connectionSettings.GetDatabaseProvider();
    }

    public async Task<IEnumerable<StatementResult>> ExecuteStatement(
        string sqlStatement,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new List<StatementResult>();
            var statements = StatementSplitter.SplitStatements(sqlStatement);

            foreach (var statement in statements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (StatementExecutionClassifier.RequiresMaterialization(statement))
                {
                    var materializedResults = await ExecuteMaterializedStatement(statement, parameters, commandTimeoutSeconds, cancellationToken);
                    result.AddRange(materializedResults);
                    continue;
                }

                var watcher = Stopwatch.StartNew();
                var connection = _databaseProvider.GetConnection();
                await connection.OpenAsync(cancellationToken);
                var command = CreateCommand(connection, statement, parameters, commandTimeoutSeconds);
                SetActiveCommand(command);
                var reader = await command.ExecuteReaderAsync(cancellationToken);
                watcher.Stop();

                result.Add(new StatementResult(reader, connection, command, statement, watcher, parameters: parameters));
                ClearActiveCommand(command);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            Cancel();
            throw;
        }
        catch (Exception e) when (IsCancellationException(e, cancellationToken))
        {
            throw new OperationCanceledException("Statement execution cancelled.", e, cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public void Cancel()
    {
        Interlocked.Exchange(ref _cancelRequested, 1);
        DbCommand? command;
        lock (_activeCommandLock)
        {
            command = _activeCommand;
        }

        try
        {
            command?.Cancel();
        }
        catch
        {
            // Provider cancellation support is best-effort.
        }
    }

    private DynamicParameters? CreateParameters(IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
            return null;

        var dapperParameters = new DynamicParameters();
        foreach (var parameter in parameters)
        {
            dapperParameters.Add(TrimParameterPrefix(parameter.Key), parameter.Value);
        }

        return dapperParameters;
    }

    private async Task<IEnumerable<StatementResult>> ExecuteMaterializedStatement(
        string statement,
        IReadOnlyDictionary<string, object?>? parameters,
        int? commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var results = new List<StatementResult>();
        await using var connection = _databaseProvider.GetConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, statement, parameters, commandTimeoutSeconds);
        SetActiveCommand(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var watcher = Stopwatch.StartNew();

            if (reader.FieldCount > 0)
            {
                var table = ResultSetMaterializer.MaterializeCurrentResult(reader);
                watcher.Stop();
                results.Add(new StatementResult(table.CreateDataReader(), null, null, statement, watcher, table.Rows.Count, parameters));
            }
            else
            {
                watcher.Stop();
                results.Add(new StatementResult(null, null, null, statement, watcher, reader.RecordsAffected, parameters));
            }
        } while (await reader.NextResultAsync(cancellationToken));

        ClearActiveCommand(command);

        return results;
    }

    private DbCommand CreateCommand(
        DbConnection connection,
        string statement,
        IReadOnlyDictionary<string, object?>? parameters,
        int? commandTimeoutSeconds = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = statement;
        command.CommandType = CommandType.Text;
        if (commandTimeoutSeconds.HasValue)
            command.CommandTimeout = commandTimeoutSeconds.Value;

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

    private string NormalizeParameterName(string name)
    {
        var trimmedName = TrimParameterPrefix(name);
        return _connectionSettings.DatabaseType == DatabaseType.Oracle
            ? trimmedName
            : $"@{trimmedName}";
    }

    private static string TrimParameterPrefix(string name)
    {
        return name.TrimStart('@', ':');
    }

    private void SetActiveCommand(DbCommand command)
    {
        Interlocked.Exchange(ref _cancelRequested, 0);
        lock (_activeCommandLock)
        {
            _activeCommand = command;
        }
    }

    private void ClearActiveCommand(DbCommand command)
    {
        lock (_activeCommandLock)
        {
            if (ReferenceEquals(_activeCommand, command))
                _activeCommand = null;
        }
    }

    private bool IsCancellationException(Exception exception, CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested && Interlocked.CompareExchange(ref _cancelRequested, 0, 0) == 0)
            return false;

        foreach (var current in EnumerateExceptions(exception))
        {
            if (current is OperationCanceledException or TaskCanceledException)
                return true;

            var message = current.Message;
            if (message.Contains("The operation was canceled", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Operation cancelled", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Operation canceled", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Command cancelled", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Command canceled", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Query execution was interrupted", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("A severe error occurred on the current command", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            yield return current;
    }
}
