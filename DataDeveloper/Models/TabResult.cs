using System;
using DataDeveloper.Enums;

namespace DataDeveloper.Models;

public abstract class TabResult
{
    protected TabResult(TabResultType type, string name, bool canClose)
    {
        Type = type;
        Name = name;
        CanClose = canClose;
        Id = Guid.NewGuid();
    }
    public Guid Id { get; }
    public TabResultType Type { get; }
    public string Name { get; }
    public bool CanClose { get; }
}