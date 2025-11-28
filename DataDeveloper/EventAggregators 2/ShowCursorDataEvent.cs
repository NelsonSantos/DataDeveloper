namespace DataDeveloper.EventAggregators;

public class ShowCursorDataEvent
{
    public ShowCursorDataEvent(int cursorOffSet, int cursorLine, int cursorColumn)
    {
        CursorOffSet = cursorOffSet;
        CursorLine = cursorLine;
        CursorColumn = cursorColumn;
    }
    public int CursorOffSet { get; }
    public int CursorLine { get; }
    public int CursorColumn { get; }
}