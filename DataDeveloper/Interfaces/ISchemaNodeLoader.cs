using System.Threading.Tasks;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Interfaces;

/// <summary>
/// Loads a schema tree node's children when it is expanded. Implementations report load
/// failures to the user instead of throwing.
/// </summary>
public interface ISchemaNodeLoader
{
    Task LoadSchemaNodeAsync(SchemaNode node);
}
