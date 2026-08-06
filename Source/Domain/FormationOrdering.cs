namespace CaravanReadiness.Domain
{
    /// <summary>
    /// Centralizes the stable ordering used when several caravan formations
    /// share a packing spot, avoiding dependence on lord enumeration order.
    /// </summary>
    public static class FormationOrdering
    {
        public static int CompareLoadIds(int left, int right)
        {
            return left.CompareTo(right);
        }
    }
}
