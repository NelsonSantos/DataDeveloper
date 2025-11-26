using DataDeveloper.Enums;

namespace DataDeveloper.Models;

public class TabDocument : BaseTabContent
{
    public TabDocument(string name, bool canClose) 
        : base(TabType.QueryEditor, name, canClose)
    {
    }
}