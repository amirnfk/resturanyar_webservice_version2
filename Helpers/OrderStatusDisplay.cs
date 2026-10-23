using resturanyar.Models;
using resturanyar.Models.CustomerAccounts;

namespace resturanyar.Helpers
{
    /// <summary>
    /// Per-order status badge labels. Status id 8 is shared by cash and credit settlements;
    /// the badge text depends on settlement debt / type.
    /// </summary>
    public static class OrderStatusDisplay
    {
        public const string PaidLabel = "پرداخت‌شده";
        public const string CreditLabel = "بدهی";
        public const string PaidOrCreditFilterLabel = "پرداخت‌شده / بدهی";

        /// <summary>
        /// Badge text for an order. For status 8: fully paid → «پرداخت‌شده», otherwise → «بدهی».
        /// </summary>
        public static string BadgeLabel(OrderDto order, string? fallbackFromMap = null)
        {
            if (order == null) return fallbackFromMap ?? "نامشخص";
            return BadgeLabel(order.StatusId, order.SettlementDebtAmount, order.SettlementType, fallbackFromMap);
        }

        public static string BadgeLabel(
            int statusId,
            decimal? settlementDebtAmount,
            string? settlementType,
            string? fallbackFromMap = null)
        {
            if (statusId == 8)
                return IsCreditSettlement(settlementDebtAmount, settlementType) ? CreditLabel : PaidLabel;

            return string.IsNullOrWhiteSpace(fallbackFromMap) ? "نامشخص" : fallbackFromMap;
        }

        /// <summary>True when status-8 settlement left unpaid debt (credit / partial).</summary>
        public static bool IsCreditSettlement(decimal? settlementDebtAmount, string? settlementType)
        {
            // Explicit remaining debt wins (including 0 after later account payoff).
            if (settlementDebtAmount.HasValue)
                return settlementDebtAmount.Value > 0m;

            if (string.IsNullOrWhiteSpace(settlementType))
                return false;

            return settlementType.Equals(OrderSettlementTypes.Credit, StringComparison.OrdinalIgnoreCase)
                || settlementType.Equals(OrderSettlementTypes.Partial, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Badge after a settle API response (debtAmount / settlementType on data).</summary>
        public static string BadgeLabelAfterSettle(decimal debtAmount, string? settlementType)
            => IsCreditSettlement(debtAmount, settlementType) ? CreditLabel : PaidLabel;
    }
}
