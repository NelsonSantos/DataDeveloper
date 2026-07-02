using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Models;

public class ConnectionGroupNode : ReactiveObject
{
    public ConnectionGroupNode(ConnectionGroup group, IEnumerable<ConnectionSettings> connections)
    {
        Group = group;
        Children = new ObservableCollection<ConnectionSettings>(connections);
    }

    public ConnectionGroup Group { get; }
    public string Name => Group.Name;
    [Reactive] public bool IsExpanded { get; set; } = true;
    public ObservableCollection<ConnectionSettings> Children { get; }
}
