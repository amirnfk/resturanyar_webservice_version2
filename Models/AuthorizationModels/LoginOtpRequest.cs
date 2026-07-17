namespace resturanyar.Models.AuthorizationModels
{
    public class LoginOtpRequest
    {
        public string PhoneNumber { get; set; }
        public string Code { get; set; }
    }
}
