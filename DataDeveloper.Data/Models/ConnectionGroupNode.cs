using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DataDeveloper.Data.Models;

public class ConnectionGroupNode
{
    public ConnectionGroupNode(ConnectionGroup group, IEnumerable<ConnectionSettings> connections)
    {
        Group = group;
        Children = new ObservableCollection<ConnectionSettings>(connections);
    }

    // Expansion state lives on Group itself (persisted), so it survives tree
    // rebuilds within a session and across reopening the connection selector.
    public ConnectionGroup Group { get; }
    public string Name => Group.Name;
    public ObservableCollection<ConnectionSettings> Children { get; }
}
