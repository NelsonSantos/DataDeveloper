using System;

namespace DataDeveloper.Events;

public class ShowMessageEventArgs : EventArgs
{
    public ShowMessageEventArgs(string messageToShow, bool focus)
    {
        MessageToShow = messageToShow;
        Focus = focus;
    }
    public string MessageToShow { get; }
    public bool Focus { get; }
}
