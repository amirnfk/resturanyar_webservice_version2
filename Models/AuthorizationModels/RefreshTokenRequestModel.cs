namespace resturanyar.Models.AuthorizationModels
{
    public class RefreshTokenRequestModel
    {
        public string PhoneNumber { get; set; }  
        public string RefreshToken { get; set; }
    }
}
