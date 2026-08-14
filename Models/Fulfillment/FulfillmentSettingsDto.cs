namespace resturanyar.Models.Fulfillment
{
    public class FulfillmentSettingsDto
    {
        public bool EnableTakeaway { get; set; }
        public bool EnableDelivery { get; set; }
        public bool GlobalEnabled { get; set; }
        public bool AutoAssignDeliveryDriver { get; set; }
        public int? DefaultDeliveryDriverUserId { get; set; }
    }

    public class UpdateFulfillmentSettingsRequest
    {
        public int RestaurantId { get; set; }
        public bool EnableTakeaway { get; set; }
        public bool EnableDelivery { get; set; }
        public bool AutoAssignDeliveryDriver { get; set; }
        public int? DefaultDeliveryDriverUserId { get; set; }
    }
}
