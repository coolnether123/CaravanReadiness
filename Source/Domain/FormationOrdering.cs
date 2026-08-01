namespace CaravanReadiness.Domain
{
    public static class FormationOrdering
    {
        public static int CompareLoadIds(int left, int right)
        {
            return left.CompareTo(right);
        }
    }
}
