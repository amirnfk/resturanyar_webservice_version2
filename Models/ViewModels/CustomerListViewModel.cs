namespace resturanyar.Models.ViewModels
{
    public class CustomerListViewModel
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; }
        public string Mobile { get; set; }
        public bool IsActive { get; set; }
        public string CreatedAtShamsi { get; set; }
        public int AddressCount { get; set; }
    }
}
