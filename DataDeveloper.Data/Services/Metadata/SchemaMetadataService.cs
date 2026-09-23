using System.Data.Common;
using Dapper;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.SqlDialects;

namespace DataDeveloper.Data.Services.Metadata;

/// <summary>
/// Runs the provider's <see cref="IObjectCatalog"/> SQL against a connection.
/// </summary>
public sealed class SchemaMetadataService
{
    private readonly IConnectionSettings _connectionSettings;
    private readonly IDatabaseProvider? _databaseProvider;
    private readonly IObjectCatalog _catalog;

    public SchemaMetadataService(IConnectionSettings connectionSettings)
        : this(connectionSettings, databaseProvider: null, catalog: null)
    {
    }

    /// <param name="databaseProvider">Connection source; defaults to the registered provider for the settings.</param>
    /// <param name="catalog">Metadata SQL; defaults to the catalog of the settings' database type.</param>
    public SchemaMetadataService(IConnectionSettings connectionSettings, IDatabaseProvider? databaseProvider, IObjectCatalog? catalog)
    {
        _connectionSettings = connectionSettings;
        _databaseProvider = databaseProvider;
        _catalog = catalog ?? ObjectCatalog.For(connectionSettings.DatabaseType);
    }

    public IReadOnlyList<DbObjectKind> RootObjectKinds => _catalog.RootObjectKinds;

    public async Task<IReadOnlyList<DatabaseObjectModel>> ListObjectsAsync(DbObjectKind kind, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        var command = new CommandDefinition(_catalog.GetObjectListStatement(kind), cancellationToken: cancellationToken);
        return (await connection.QueryAsync<DatabaseObjectModel>(command)).ToList();
    }

    /// <summary>
    /// Reads the columns of a table or view. Without a schema, the default schema is used.
    /// </summary>
    public async Task<IReadOnlyList<ColumnModel>> GetColumnsAsync(DbObjectRef tableOrView, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        return await QueryTableAsync<ColumnModel>(connection, _catalog.GetColumnsStatement(), tableOrView, cancellationToken);
    }

    public async Task<IReadOnlyList<RoutineParameterModel>> GetRoutineParametersAsync(DatabaseObjectModel routine, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        var command = new CommandDefinition(
            _catalog.GetRoutineParametersStatement(),
            new { SpecificName = routine.SpecificName ?? routine.Name },
            cancellationToken: cancellationToken);
        return (await connection.QueryAsync<RoutineParameterModel>(command)).ToList();
    }

    /// <summary>
    /// Reads the object's DDL as the database reports it. Returns an empty string when the
    /// provider has no such object or the database returns nothing.
    /// </summary>
    public async Task<string> GetDdlAsync(DbObjectRef databaseObject, CancellationToken cancellationToken = default)
    {
        var retrieval = _catalog.GetDdlRetrieval(databaseObject);
        if (retrieval is null)
            return string.Empty;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        if (retrieval.SessionSetup is not null)
        {
            await using var setupCommand = connection.CreateCommand();
            setupCommand.CommandText = retrieval.SessionSetup;
            await setupCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = retrieval.Query;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ddl = await DdlResultReader.ReadAsync(reader, cancellationToken);

        return string.IsNullOrWhiteSpace(ddl) || retrieval.PostProcess is null
            ? ddl
            : retrieval.PostProcess(ddl);
    }

    /// <summary>
    /// Reads the table's column defaults, primary key, foreign keys and indexes from the catalog.
    /// A table without a schema is looked up in the connection's default schema.
    /// </summary>
    public async Task<TableStructure> GetTableStructureAsync(DbObjectRef table, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return new TableStructure
        {
            ColumnDefaults = await QueryTableAsync<ColumnDefaultValueModel>(connection, _catalog.GetColumnDefaultsStatement(), table, cancellationToken),
            PrimaryKeyColumns = await QueryTableAsync<PrimaryKeyColumnModel>(connection, _catalog.GetPrimaryKeyStatement(), table, cancellationToken),
            ForeignKeyColumns = await QueryTableAsync<ForeignKeyColumnModel>(connection, _catalog.GetForeignKeysStatement(), table, cancellationToken),
            IndexColumns = await QueryTableAsync<IndexColumnModel>(connection, _catalog.GetIndexesStatement(), table, cancellationToken)
        };
    }

    /// <summary>
    /// Reads the table's primary key, unique constraints and foreign keys.
    /// </summary>
    public async Task<TableKeys> GetTableKeysAsync(DbObjectRef table, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return new TableKeys
        {
            PrimaryKeyColumns = await QueryTableAsync<PrimaryKeyColumnModel>(connection, _catalog.GetPrimaryKeyStatement(), table, cancellationToken),
            UniqueConstraintColumns = await QueryTableAsync<UniqueConstraintColumnModel>(connection, _catalog.GetUniqueConstraintsStatement(), table, cancellationToken),
            ForeignKeyColumns = await QueryTableAsync<ForeignKeyColumnModel>(connection, _catalog.GetForeignKeysStatement(), table, cancellationToken)
        };
    }

    /// <summary>
    /// Reads the table's indexes, excluding the ones backing primary key or unique constraints.
    /// </summary>
    public async Task<IReadOnlyList<IndexColumnModel>> GetIndexesAsync(DbObjectRef table, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var indexColumns = await QueryTableAsync<IndexColumnModel>(connection, _catalog.GetIndexesStatement(), table, cancellationToken);
        var uniqueConstraintNames = (await QueryTableAsync<UniqueConstraintColumnModel>(connection, _catalog.GetUniqueConstraintsStatement(), table, cancellationToken))
            .Select(row => row.ConstraintName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // MySQL and Oracle list the index backing a unique constraint, which is already shown as a key.
        return indexColumns.Where(row => !uniqueConstraintNames.Contains(row.IndexName)).ToList();
    }

    /// <summary>
    /// Reads the table's check constraints, excluding NOT NULL constraints. Empty on server
    /// versions that do not store check constraints.
    /// </summary>
    public async Task<IReadOnlyList<CheckConstraintModel>> GetCheckConstraintsAsync(DbObjectRef table, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var query = _catalog.GetCheckConstraintsQuery(connection.ServerVersion);
        if (query is null)
            return [];

        if (query.ParseTableDdl is null)
            return await QueryTableAsync<CheckConstraintModel>(connection, query.Statement, table, cancellationToken);

        var tableDdl = await QueryTableAsync<string>(connection, query.Statement, table, cancellationToken);
        return tableDdl.SelectMany(ddl => query.ParseTableDdl(ddl)).ToList();
    }

    /// <summary>
    /// Reads the triggers defined on a table.
    /// </summary>
    public async Task<IReadOnlyList<TriggerModel>> GetTriggersAsync(DbObjectRef table, CancellationToken cancellationToken = default)
    {
        var query = _catalog.GetTriggersQuery();
        await using var connection = CreateConnection();
        var triggers = await QueryTableAsync<TriggerModel>(connection, query.Statement, table, cancellationToken);

        if (query.CompleteFromDefinition is not null)
        {
            foreach (var trigger in triggers)
                query.CompleteFromDefinition(trigger);
        }

        return triggers;
    }

    private static async Task<IReadOnlyList<T>> QueryTableAsync<T>(
        DbConnection connection, string statement, DbObjectRef table, CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(
            statement,
            new { SchemaName = table.Schema, TableName = table.Name },
            cancellationToken: cancellationToken);
        return (await connection.QueryAsync<T>(command)).ToList();
    }

    /// <summary>
    /// Reads the DDL of the table, view, procedure or function a schema tree node represents.
    /// Returns an empty string for any other node.
    /// </summary>
    public Task<string> GetDdlAsync(SchemaNode node, CancellationToken cancellationToken = default)
    {
        var databaseObject = DbObjectRef.FromSchemaNode(node, SqlDialect.For(_connectionSettings.DatabaseType));
        return databaseObject is null
            ? Task.FromResult(string.Empty)
            : GetDdlAsync(databaseObject, cancellationToken);
    }

    private DbConnection CreateConnection()
    {
        return (_databaseProvider ?? _connectionSettings.GetDatabaseProvider()).GetConnection();
    }
}
