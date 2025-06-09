using DataDeveloper.Enums;
using DataDeveloper.Models;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public class TabMessageViewModel : BaseTabContent
{
    public TabMessageViewModel(string name, bool canClose) 
        : base(TabType.Message, name, canClose)
    {
    }
    [Reactive] public string Message { get; set; }
}