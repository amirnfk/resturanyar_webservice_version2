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

    public static class OrderSettlementTypes
    {
        public const string Cash = "Cash";
        public const string Credit = "Credit";
        public const string Partial = "Partial";

        public static string Resolve(decimal paidAmount, decimal debtAmount)
        {
            if (debtAmount <= 0m) return Cash;
            if (paidAmount <= 0m) return Credit;
            return Partial;
        }

        public static string SuccessMessage(decimal paidAmount, decimal debtAmount, decimal priorPaid = 0m)
        {
            var debt = ToFaMoney(debtAmount);
            var baseMsg = Resolve(paidAmount, debtAmount) switch
            {
                Credit => $"بدهی {debt} تومان ثبت شد.",
                Partial => $"پرداخت ثبت شد؛ بدهی {debt} تومان.",
                _ => "پرداخت ثبت شد."
            };

            if (priorPaid > 0m)
                return baseMsg + $" مانده قبلی: {ToFaMoney(priorPaid)} تومان.";

            return baseMsg;
        }

        private static string ToFaMoney(decimal amount)
        {
            var s = decimal.Round(amount, 0).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
            var chars = s.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] >= '0' && chars[i] <= '9') chars[i] = (char)('۰' + (chars[i] - '0'));
            }
            return new string(chars);
        }
    }
}
