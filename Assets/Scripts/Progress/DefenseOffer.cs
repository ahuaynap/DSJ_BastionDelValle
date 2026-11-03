public static class DefenseOffer
{
    public static int CostAt(int[] costs, int selected)
    {
        if (costs == null || selected < 0 || selected >= costs.Length)
        {
            return -1;
        }

        return costs[selected];
    }

    public static bool TryCharge(
        int balance,
        int[] costs,
        int selected,
        out int nextBalance,
        out int charged)
    {
        int cost = CostAt(costs, selected);

        if (cost < 0 || balance < cost)
        {
            nextBalance = balance;
            charged = 0;
            return false;
        }

        nextBalance = balance - cost;
        charged = cost;
        return true;
    }
}
