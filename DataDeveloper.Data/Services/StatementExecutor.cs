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
    private readonly IProviderSqlAnalyzer _sqlAnalyzer;
    private readonly object _activeCommandLock = new();
    private readonly SemaphoreSlim _transactionLock = new(1, 1);
    private DbCommand? _activeCommand;
    private DbConnection? _transactionConnection;
    private DbTransaction? _transaction;
    private int _cancelRequested;
    
    public StatementExecutor(IConnectionSettings connectionSettings)
    {
        _connectionSettings = connectionSettings;
        _databaseProvider = _connectionSettings.GetDatabaseProvider();
        _sqlAnalyzer = _connectionSettings.GetSqlAnalyzer();
    }

    public bool HasActiveTransaction => _transaction is not null;

    public async Task<IEnumerable<StatementResult>> ExecuteStatement(
        string sqlStatement,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int? commandTimeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new List<StatementResult>();
            var statements = _sqlAnalyzer.SplitStatements(sqlStatement);

            foreach (var statement in statements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_sqlAnalyzer.IsTransactionControlStatement(statement))
                {
                    var transactionControlWatcher = Stopwatch.StartNew();
                    if (_sqlAnalyzer.IsBeginTransactionStatement(statement))
                        await BeginTransaction(cancellationToken);
                    else if (IsCommitStatement(statement))
                        await CommitTransaction(cancellationToken);
                    else
                        await RollbackTransaction(cancellationToken);

                    transactionControlWatcher.Stop();
                    result.Add(new StatementResult(null, null, null, statement, transactionControlWatcher, 0, parameters));
                    continue;
                }

                if (_sqlAnalyzer.IsDmlStatement(statement))
                {
                    var dmlResult = await ExecuteTransactionalDmlStatement(statement, parameters, commandTimeoutSeconds, cancellationToken);
                    result.Add(dmlResult);
                    continue;
                }

                if (HasActiveTransaction)
                {
                    var transactionResults = await ExecuteMaterializedStatementInActiveTransaction(
                        statement,
                        parameters,
                        commandTimeoutSeconds,
                        cancellationToken);
                    result.AddRange(transactionResults);
                    continue;
                }

                if (_sqlAnalyzer.RequiresMaterialization(statement))
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
                DbDataReader reader;
                try
                {
                    reader = await command.ExecuteReaderAsync(cancellationToken);
                    watcher.Stop();
                }
                catch
                {
                    ClearActiveCommand(command);
                    await command.DisposeAsync();
                    await connection.DisposeAsync();
                    throw;
                }

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

    public async Task CommitTransaction(CancellationToken cancellationToken = default)
    {
        await CompleteTransaction(commit: true, cancellationToken);
    }

    public async Task BeginTransaction(CancellationToken cancellationToken = default)
    {
        await _transactionLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureTransaction(cancellationToken);
        }
        finally
        {
            _transactionLock.Release();
        }
    }

    public async Task RollbackTransaction(CancellationToken cancellationToken = default)
    {
        await CompleteTransaction(commit: false, cancellationToken);
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

    private async Task<StatementResult> ExecuteTransactionalDmlStatement(
        string statement,
        IReadOnlyDictionary<string, object?>? parameters,
        int? commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await _transactionLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureTransaction(cancellationToken);

            var watcher = Stopwatch.StartNew();
            await using var command = CreateCommand(_transactionConnection!, statement, parameters, commandTimeoutSeconds);
            command.Transaction = _transaction;
            SetActiveCommand(command);
            try
            {
                var recordsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
                watcher.Stop();
                return new StatementResult(null, null, null, statement, watcher, recordsAffected, parameters);
            }
            finally
            {
                ClearActiveCommand(command);
            }
        }
        finally
        {
            _transactionLock.Release();
        }
    }

    private async Task<IEnumerable<StatementResult>> ExecuteMaterializedStatementInActiveTransaction(
        string statement,
        IReadOnlyDictionary<string, object?>? parameters,
        int? commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await _transactionLock.WaitAsync(cancellationToken);
        try
        {
            if (_transaction is null || _transactionConnection is null)
                return await ExecuteMaterializedStatement(statement, parameters, commandTimeoutSeconds, cancellationToken);

            await using var command = CreateCommand(_transactionConnection, statement, parameters, commandTimeoutSeconds);
            command.Transaction = _transaction;
            return await ExecuteMaterializedCommand(command, statement, parameters, cancellationToken);
        }
        finally
        {
            _transactionLock.Release();
        }
    }

    private async Task<IEnumerable<StatementResult>> ExecuteMaterializedCommand(
        DbCommand command,
        string statement,
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken cancellationToken)
    {
        var results = new List<StatementResult>();
        SetActiveCommand(command);
        try
        {
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
        }
        finally
        {
            ClearActiveCommand(command);
        }

        return results;
    }

    private async Task EnsureTransaction(CancellationToken cancellationToken)
    {
        if (_transaction is not null && _transactionConnection is not null)
            return;

        _transactionConnection = _databaseProvider.GetConnection();
        await _transactionConnection.OpenAsync(cancellationToken);
        _transaction = await _transactionConnection.BeginTransactionAsync(cancellationToken);
    }

    private async Task CompleteTransaction(bool commit, CancellationToken cancellationToken)
    {
        await _transactionLock.WaitAsync(cancellationToken);
        try
        {
            if (_transaction is null)
                return;

            try
            {
                if (commit)
                    await _transaction.CommitAsync(cancellationToken);
                else
                    await _transaction.RollbackAsync(cancellationToken);
            }
            finally
            {
                await DisposeTransactionResources();
            }
        }
        finally
        {
            _transactionLock.Release();
        }
    }

    private async Task DisposeTransactionResources()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_transactionConnection is not null)
        {
            await _transactionConnection.DisposeAsync();
            _transactionConnection = null;
        }
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

    private static bool IsCommitStatement(string statement)
    {
        return statement.TrimStart().StartsWith("commit", StringComparison.OrdinalIgnoreCase);
    }
}
