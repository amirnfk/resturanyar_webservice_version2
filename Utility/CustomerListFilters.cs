using Microsoft.EntityFrameworkCore;
using Resturanyar.Data;
using resturanyar.Models.CustomerModels;

namespace resturanyar.Utility
{
    /// <summary>
    /// Shared customer directory filters for list + export endpoints.
    /// Debtor/creditor status is based on the sum of ledger signed amounts
    /// (source of truth), not a free-text description on the customer profile.
    /// </summary>
    public static class CustomerListFilters
    {
        public const string All = "all";
        public const string Debtors = "debtors";
        public const string Creditors = "creditors";
        public const string RealMobile = "realMobile";
        public const string AutoMobile = "autoMobile";
        public const string HasAddress = "hasAddress";
        public const string NoOrders = "noOrders";

        public static Task<IQueryable<Customer>> ApplyAsync(
            IQueryable<Customer> customersQuery,
            AppDbContext db,
            int restaurantId,
            string? filter,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Apply(customersQuery, db, restaurantId, filter));
        }

        public static IQueryable<Customer> Apply(
            IQueryable<Customer> customersQuery,
            AppDbContext db,
            int restaurantId,
            string? filter)
        {
            switch (Normalize(filter))
            {
                case Debtors:
                    // Positive signed total = still owes money
                    return customersQuery.Where(c =>
                        db.CustomerAccountTransactions
                            .Where(t => t.CustomerId == c.CustomerId && t.RestaurantId == restaurantId)
                            .Sum(t => (decimal?)t.SignedAmount) > 0m);

                case Creditors:
                    return customersQuery.Where(c =>
                        db.CustomerAccountTransactions
                            .Where(t => t.CustomerId == c.CustomerId && t.RestaurantId == restaurantId)
                            .Sum(t => (decimal?)t.SignedAmount) < 0m);

                case RealMobile:
                    return customersQuery.Where(c =>
                        c.Mobile != null &&
                        c.Mobile.Length == 11 &&
                        c.Mobile.StartsWith("09"));

                case AutoMobile:
                    return customersQuery.Where(c =>
                        c.Mobile == null ||
                        c.Mobile.Length != 11 ||
                        !c.Mobile.StartsWith("09"));

                case HasAddress:
                    return customersQuery.Where(c =>
                        db.CustomerAddresses.Any(a => a.CustomerId == c.CustomerId));

                case NoOrders:
                    return customersQuery.Where(c =>
                        !db.Orders.Any(o =>
                            o.CustomerId == c.CustomerId &&
                            o.RestaurantId == restaurantId &&
                            o.StatusId == 11));

                default:
                    return customersQuery;
            }
        }

        public static string Normalize(string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return All;

            return filter.Trim() switch
            {
                Debtors => Debtors,
                Creditors => Creditors,
                RealMobile => RealMobile,
                AutoMobile => AutoMobile,
                HasAddress => HasAddress,
                NoOrders => NoOrders,
                _ => All
            };
        }

        public static string GetLabel(string? filter) => Normalize(filter) switch
        {
            Debtors => "بدهکاران",
            Creditors => "بستانکاران",
            RealMobile => "شماره موبایل واقعی",
            AutoMobile => "بدون شماره واقعی",
            HasAddress => "دارای آدرس",
            NoOrders => "بدون سفارش",
            _ => "همه مشتریان"
        };
    }
}
