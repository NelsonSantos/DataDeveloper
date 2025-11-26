using System;
using DataDeveloper.Models;

namespace DataDeveloper.Events;

public class TabCanCloseEventArgs : EventArgs
{
    public TabCanCloseEventArgs(bool canClose, BaseTabContent tab)
    {
        CanTabBeClosed = canClose;
        Tab = tab;
    }
    public bool CanTabBeClosed { get; set; }
    public BaseTabContent Tab { get; }
}