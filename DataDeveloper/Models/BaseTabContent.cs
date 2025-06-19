using System;
using System.Reactive;
using System.Threading.Tasks;
using DataDeveloper.Core;
using DataDeveloper.Enums;
using DataDeveloper.Events;
using ReactiveUI;

namespace DataDeveloper.Models;

public abstract class BaseTabContent : ViewModelBase
{
    protected BaseTabContent(TabType type, string name, bool canClose, IServiceProvider serviceProvider)
    {
        Type = type;
        Name = name;
        CanClose = canClose;
        ServiceProvider = serviceProvider;
        Id = Guid.NewGuid();
    }
    public Guid Id { get; }
    public TabType Type { get; }
    public string Name { get; }
    public bool CanClose { get; }
    protected IServiceProvider ServiceProvider { get; }
}