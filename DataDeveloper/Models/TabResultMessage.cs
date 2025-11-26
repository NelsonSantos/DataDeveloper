using DataDeveloper.Enums;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.Models;

public class TabResultMessage : BaseTabContent
{
    public TabResultMessage(string name, bool canClose) 
        : base(TabType.Message, name, canClose)
    {
    }
    [Reactive] public string Message { get; set; }
}