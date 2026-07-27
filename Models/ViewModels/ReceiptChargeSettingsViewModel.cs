namespace resturanyar.Models.ViewModels
{
    public class ReceiptChargeSettingsViewModel
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public List<resturanyar.Models.Receipt.ChargeDefinitionDto> Definitions { get; set; } = new();
    }
}
