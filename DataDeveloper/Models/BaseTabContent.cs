using System;
using System.Reactive;
using System.Threading.Tasks;
using DataDeveloper.Core;
using DataDeveloper.Enums;
using ReactiveUI;

namespace DataDeveloper.Models;

public abstract class BaseTabContent : ViewModelBase
{
    protected BaseTabContent(TabType type, string name, bool canClose)
    {
        Type = type;
        Name = name;
        CanClose = canClose;
        Id = Guid.NewGuid();
        CloseCommand = ReactiveCommand.CreateFromTask(OnClose);
    }
    public Guid Id { get; }
    public TabType Type { get; }
    public string Name { get; }
    public bool CanClose { get; }
    public ReactiveCommand<Unit, bool> CloseCommand { get; }
    protected virtual async Task<bool> OnClose()
    {
        if (!CanClose) return await Task.FromResult(false);
        return await Task.FromResult(true);
    }
}