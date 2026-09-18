namespace resturanyar.Models.CustomerAccounts
{
    public static class CustomerAccountTransactionTypes
    {
        public const string Debt = "Debt";
        public const string Payment = "Payment";
        public const string Adjustment = "Adjustment";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Debt,
            Payment,
            Adjustment
        };

        public static bool IsValid(string? type) =>
            !string.IsNullOrWhiteSpace(type) && All.Contains(type.Trim());
    }
}
