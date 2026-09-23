using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Interfaces;

/// <summary>
/// Provider-specific SQL for reading database object metadata.
/// </summary>
public interface IObjectCatalog
{
    /// <summary>
    /// Returns how to read the object's native DDL, or null when the provider has no such object.
    /// </summary>
    DdlRetrieval? GetDdlRetrieval(DbObjectRef databaseObject);
}
