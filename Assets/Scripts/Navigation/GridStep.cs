public readonly struct GridStep
{
    public int X { get; }
    public int Y { get; }
    public int CumulativeCost { get; }

    public GridStep(int x, int y, int cumulativeCost)
    {
        X = x;
        Y = y;
        CumulativeCost = cumulativeCost;
    }
}
