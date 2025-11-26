using System;
using System.Reactive;
using System.Threading.Tasks;
using DataDeveloper.Core;
using DataDeveloper.Enums;
using DataDeveloper.Events;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

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
    [Reactive] public string Name { get; set; }
    public bool CanClose { get; }
    protected IServiceProvider ServiceProvider { get; }
    [Reactive] public bool IsBusy { get; set; } 
}