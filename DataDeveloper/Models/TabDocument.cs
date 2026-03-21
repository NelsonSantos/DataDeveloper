using System;
using DataDeveloper.Enums;

namespace DataDeveloper.Models;

public class TabDocument : BaseTabContent
{
    public TabDocument(string name, bool canClose, IServiceProvider serviceProvider)
        : base(TabType.QueryEditor, name, canClose, serviceProvider)
    {
    }
}