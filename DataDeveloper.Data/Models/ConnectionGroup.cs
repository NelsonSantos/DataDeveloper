using System;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Data.Models;

public class ConnectionGroup : ReactiveObject
{
    [Reactive] public Guid Id { get; set; }
    [Reactive] public string Name { get; set; } = string.Empty;
}
