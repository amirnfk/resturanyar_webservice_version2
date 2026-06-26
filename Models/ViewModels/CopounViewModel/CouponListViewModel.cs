namespace resturanyar.Models.ViewModels.CopounViewModel
{
   

    
        public class CouponListViewModel
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string DiscountType { get; set; }
            public decimal DiscountValue { get; set; }
            public decimal? MaxDiscountAmount { get; set; }
            public decimal? MinPurchaseAmount { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public bool IsActive { get; set; }
            public int UsedCount { get; set; }
            public int? UsageLimit { get; set; }
            public string SpecificOwnerName { get; set; }
            public string SpecificRestaurantName { get; set; }
            public bool IsExpired { get; set; }
            public int? DaysRemaining { get; set; }
            public double? UsagePercentage { get; set; }

            public string DiscountTypeDisplay => DiscountType == "Percentage" ? "درصدی" : "مبلغ ثابت";
            public string ScopeDisplay
            {
                get
                {
                    if (!string.IsNullOrEmpty(SpecificOwnerName) && SpecificOwnerName != "همه مالکان")
                        return $"مالک: {SpecificOwnerName}";
                    if (!string.IsNullOrEmpty(SpecificRestaurantName) && SpecificRestaurantName != "همه رستوران‌ها")
                        return $"رستوران: {SpecificRestaurantName}";
                    return "عمومی";
                }
            }
            public string StatusDisplay
            {
                get
                {
                    if (IsExpired) return "منقضی شده";
                    if (!IsActive) return "غیرفعال";
                    if (UsageLimit.HasValue && UsedCount >= UsageLimit.Value)
                        return "تکمیل شده";
                    return "فعال";
                }
            }
            public string StatusColor
            {
                get
                {
                    if (IsExpired) return "danger";
                    if (!IsActive) return "secondary";
                    if (UsageLimit.HasValue && UsedCount >= UsageLimit.Value)
                        return "warning";
                    return "success";
                }
            }
        }
    
}
