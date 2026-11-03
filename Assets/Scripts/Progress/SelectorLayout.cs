public struct RowPlan
{
    public int Rows;
    public int FirstCount;
    public float CardWidth;
}

public static class SelectorLayout
{
    public static RowPlan Plan(
        float innerWidth,
        int cards,
        float gap,
        float minimumCard)
    {
        var plan = new RowPlan
        {
            Rows = 1,
            FirstCount = cards < 0 ? 0 : cards,
            CardWidth = innerWidth
        };

        if (cards <= 1 || innerWidth <= 0f)
        {
            return plan;
        }

        if (gap < 0f)
        {
            gap = 0f;
        }

        if (minimumCard < 1f)
        {
            minimumCard = 1f;
        }

        float single = (innerWidth - gap * (cards - 1)) / cards;

        if (single >= minimumCard)
        {
            plan.CardWidth = single;
            return plan;
        }

        int first = (cards + 1) / 2;
        int second = cards - first;
        int wide = first > second ? first : second;
        plan.Rows = 2;
        plan.FirstCount = first;
        plan.CardWidth = (innerWidth - gap * (wide - 1)) / wide;
        return plan;
    }
}
