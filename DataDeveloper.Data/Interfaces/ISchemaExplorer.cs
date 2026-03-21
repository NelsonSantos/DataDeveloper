using System.Collections.ObjectModel;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Data.Interfaces;

public interface ISchemaExplorer
{
    Task InitializeSchemaNode();
    ObservableCollection<SchemaNode> RootConnections { get; }
    Task LoadTableColumnsAsync(SchemaNode table);
}