namespace resturanyar.Models
{
    using System.Security.Cryptography;
    using System.Text;

    public static class OtpHelper
    {
        public static string HashOtp(string otp)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(otp));
            return Convert.ToBase64String(bytes);
        }
    }

}
