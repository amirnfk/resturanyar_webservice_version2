namespace resturanyar.Models.ViewModels.Admin
{
    public class ReceiptChargeFeatureItemViewModel
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
        public bool ReceiptChargesEnabled { get; set; }
        public int ChargeDefinitionCount { get; set; }
        public int IssuedReceiptCount { get; set; }
    }

    public class ReceiptChargeFeaturesViewModel
    {
        public List<ReceiptChargeFeatureItemViewModel> Restaurants { get; set; } = new();
        public int EnabledCount { get; set; }
        public int TotalCount { get; set; }
    }
}
