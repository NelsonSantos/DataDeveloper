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
    private readonly IObjectCatalog _catalog;

    public SchemaMetadataService(IConnectionSettings connectionSettings)
    {
        _connectionSettings = connectionSettings;
        _catalog = ObjectCatalog.For(connectionSettings.DatabaseType);
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

        await using var connection = _connectionSettings.GetDatabaseProvider().GetConnection();
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
}
