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
        var command = new CommandDefinition(
            _catalog.GetColumnsStatement(),
            new { SchemaName = tableOrView.Schema, TableName = tableOrView.Name },
            cancellationToken: cancellationToken);
        return (await connection.QueryAsync<ColumnModel>(command)).ToList();
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

        var parameters = new { SchemaName = table.Schema, TableName = table.Name };

        return new TableStructure
        {
            ColumnDefaults = await QueryAsync<ColumnDefaultValueModel>(_catalog.GetColumnDefaultsStatement()),
            PrimaryKeyColumns = await QueryAsync<PrimaryKeyColumnModel>(_catalog.GetPrimaryKeyStatement()),
            ForeignKeyColumns = await QueryAsync<ForeignKeyColumnModel>(_catalog.GetForeignKeysStatement()),
            IndexColumns = await QueryAsync<IndexColumnModel>(_catalog.GetIndexesStatement())
        };

        async Task<IReadOnlyList<T>> QueryAsync<T>(string statement)
        {
            var command = new CommandDefinition(statement, parameters, cancellationToken: cancellationToken);
            return (await connection.QueryAsync<T>(command)).ToList();
        }
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
