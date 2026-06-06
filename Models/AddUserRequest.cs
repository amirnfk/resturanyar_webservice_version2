namespace resturanyar.Models
{
    public class AddUserRequest
    {
        public string name { get; set; }
        public int role_id { get; set; }
        public string password { get; set; }
        public int restaurant_id { get; set; }
        public bool? order_management_permission { get; set; }
        public bool? kitchen_management_permission { get; set; }
        public bool? payment_management_permission { get; set; }
    }

}
