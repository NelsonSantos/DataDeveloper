using DataDeveloper.Enums;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Models;

public class TabResultMessage : TabResult
{
    public TabResultMessage(string name, bool canClose) 
        : base(TabResultType.Message, name, canClose)
    {
    }
    [Reactive] public string Message { get; set; }
}