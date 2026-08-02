using Microsoft.IdentityModel.Tokens;
using resturanyar.Models;
using Resturanyar.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace resturanyar.Utility
{
    public class TokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateOwnerToken(Owner owner)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString()),
                new Claim(ClaimTypes.MobilePhone, owner.Phone),
                new Claim(ClaimTypes.Name, owner.Name ?? "کاربر"),
                new Claim(ClaimTypes.Role, "Owner")
            };

            var jwtDays = _configuration.GetValue<int>("Jwt:JwtExpirationDays");
            if (jwtDays <= 0)
                jwtDays = _configuration.GetValue<int>("JwtSettings:JwtExpirationDays");

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(jwtDays > 0 ? jwtDays : 1),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateStaffToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.user_id.ToString()),
                new Claim(ClaimTypes.Name, user.name ?? "کارمند"),
                new Claim(ClaimTypes.Role, "Staff"),
                new Claim("restaurant_id", user.restaurant_id.ToString()),
                new Claim("role_id", user.role_id.ToString()),
                new Claim("order_permission", user.order_management_permission ? "1" : "0"),
                new Claim("kitchen_permission", user.kitchen_management_permission ? "1" : "0"),
                new Claim("payment_permission", user.payment_management_permission ? "1" : "0")
            };

            var jwtDays = _configuration.GetValue<int>("Jwt:JwtExpirationDays");
            if (jwtDays <= 0)
                jwtDays = _configuration.GetValue<int>("JwtSettings:JwtExpirationDays");

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(jwtDays > 0 ? jwtDays : 1),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }
    }
}