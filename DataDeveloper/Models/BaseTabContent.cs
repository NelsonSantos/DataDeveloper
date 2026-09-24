using System;
using System.Reactive;
using System.Threading.Tasks;
using DataDeveloper.Core;
using DataDeveloper.Enums;
using DataDeveloper.Events;
using ReactiveUI;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

namespace DataDeveloper.Models;

public abstract partial class BaseTabContent : ViewModelBase
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
    [Reactive] private string _name;
    public bool CanClose { get; }
    protected IServiceProvider ServiceProvider { get; }
    [Reactive] private bool _isBusy;
}