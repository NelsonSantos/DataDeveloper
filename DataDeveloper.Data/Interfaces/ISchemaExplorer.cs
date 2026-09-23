using System.Collections.ObjectModel;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Interfaces;

public interface ISchemaExplorer
{
    Task InitializeSchemaNode();
    ObservableCollection<SchemaNode> RootConnections { get; }
    Task LoadTableColumnsAsync(SchemaNode table);
    Task LoadNodeAsync(SchemaNode node);
    Task RefreshSchemaAsync();
    Task RefreshSchemaObjectAsync(string statement);

    /// <summary>
    /// Raised after the tree's objects are (re)loaded, so caches built from them can be discarded.
    /// </summary>
    event EventHandler? SchemaRefreshed;
}
