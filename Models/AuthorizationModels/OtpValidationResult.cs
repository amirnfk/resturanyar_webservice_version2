namespace resturanyar.Models.AuthorizationModels
{
    public class OtpValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public resturanyar.Models.Owner Owner { get; set; }
        public bool NeedsRegistration { get; set; }
        public string RegistrationToken { get; set; }
    }
}
