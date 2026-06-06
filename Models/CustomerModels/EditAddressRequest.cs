namespace resturanyar.Models.CustomerModels
{
    public class EditAddressRequest
    {
        public int AddressId { get; set; }
        public int CustomerId { get; set; }
        public string Title { get; set; }
        public string AddressText { get; set; }
        public string Unit { get; set; }
        public string Floor { get; set; }
        public string PlateNumber { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public bool IsDefault { get; set; }
        public string Description { get; set; }
    }
}
