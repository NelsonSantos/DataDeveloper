namespace DataDeveloper.EventAggregators;

public class ShowExecutionStatusEvent
{
    public ShowExecutionStatusEvent(bool isVisible, string message)
    {
        IsVisible = isVisible;
        Message = message;
    }

    public bool IsVisible { get; }
    public string Message { get; }
}
