namespace resturanyar.Models.AuthorizationModels
{
    public class RegisterRequest
    {
        public string PhoneNumber { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public string RegistrationToken { get; set; }
    }
}
