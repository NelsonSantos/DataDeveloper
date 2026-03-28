namespace DataDeveloper.NextGrid;

public readonly record struct GridVisibleRange(int Start, int EndExclusive)
{
    public int Length => Math.Max(0, EndExclusive - Start);
}
