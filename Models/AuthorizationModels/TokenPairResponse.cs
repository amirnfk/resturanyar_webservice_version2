namespace resturanyar.Models.AuthorizationModels
{
    public class TokenPairResponse
    {
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
