using System;
using ReactiveUI;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

namespace DataDeveloper.Data.Models;

public partial class ConnectionGroup : ReactiveObject
{
    [Reactive] private Guid _id;
    [Reactive] private string _name = string.Empty;
    [Reactive] private bool _isExpanded = true;
}
