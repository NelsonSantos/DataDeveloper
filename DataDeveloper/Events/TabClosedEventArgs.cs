using System;
using DataDeveloper.Models;

namespace DataDeveloper.Events;

public class TabClosedEventArgs : EventArgs
{
    public TabClosedEventArgs(BaseTabContent tab)
    {
        Tab = tab;
    }
    public BaseTabContent Tab { get; }
}
