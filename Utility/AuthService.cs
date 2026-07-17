using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using resturanyar.Models;
using resturanyar.Models.AuthorizationModels;
using Resturanyar.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace resturanyar.Utility
{
    public class AuthService
    {
        private const string RegistrationProofPrefix = "reg_proof:";
        private static readonly TimeSpan RegistrationProofTtl = TimeSpan.FromMinutes(10);

        private readonly AppDbContext _context;
        private readonly TokenService _tokenService;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthService(
            AppDbContext context,
            TokenService tokenService,
            IConfiguration configuration,
            IMemoryCache cache,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _tokenService = tokenService;
            _configuration = configuration;
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;
        }

        public static string NormalizePhone(string phone)
        {
            return phone?.Trim().Replace(" ", "") ?? string.Empty;
        }

        public static string EncodePassword(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword)) return null;
            byte[] bytes = Encoding.UTF8.GetBytes(plainPassword);
            return Convert.ToBase64String(bytes);
        }

        public static string DecodePassword(string encodedPassword)
        {
            if (string.IsNullOrEmpty(encodedPassword)) return null;
            byte[] bytes = Convert.FromBase64String(encodedPassword);
            return Encoding.UTF8.GetString(bytes);
        }

        public async Task<TokenPairResponse> IssueTokenPairAsync(Owner owner)
        {
            string jwtToken = _tokenService.GenerateOwnerToken(owner);
            string refreshTokenString = _tokenService.GenerateRefreshToken();

            var jwtDays = _configuration.GetValue<int>("Jwt:JwtExpirationDays");
            if (jwtDays <= 0)
                jwtDays = _configuration.GetValue<int>("JwtSettings:JwtExpirationDays");

            var refreshDays = _configuration.GetValue<int>("Jwt:RefreshExpirationDays");
            if (refreshDays <= 0)
                refreshDays = _configuration.GetValue<int>("JwtSettings:RefreshExpirationDays");

            var jwtExpiration = DateTime.UtcNow.AddDays(jwtDays > 0 ? jwtDays : 1);
            var refreshExpiration = DateTime.UtcNow.AddDays(refreshDays > 0 ? refreshDays : 30);

            _context.RefreshTokens.Add(new RefreshToken
            {
                Token = refreshTokenString,
                ExpiryTime = refreshExpiration,
                OwnerId = owner.Id
            });
            await _context.SaveChangesAsync();

            return new TokenPairResponse
            {
                Token = jwtToken,
                RefreshToken = refreshTokenString,
                ExpiresAt = jwtExpiration
            };
        }

        public async Task SignInOwnerCookieAsync(Owner owner)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, owner.Name ?? "مدیر رستوران"),
                new Claim("OwnerId", owner.Id.ToString()),
                new Claim(ClaimTypes.Role, "Owner")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        public bool ShouldSignInCookie(HttpContext httpContext)
        {
            if (httpContext == null) return false;
            return httpContext.Request.Headers["X-Client"]
                .ToString()
                .Equals("web", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<Owner?> ValidatePasswordAsync(string phone, string password)
        {
            phone = NormalizePhone(phone);
            if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
                return null;

            var owner = await _context.Owners.FirstOrDefaultAsync(o => o.Phone == phone);
            if (owner == null) return null;

            if (DecodePassword(owner.Password) != password)
                return null;

            return owner;
        }

        public async Task<OtpValidationResult> ValidateOtpAsync(string phone, string code)
        {
            phone = NormalizePhone(phone);
            if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(code))
            {
                return new OtpValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "شماره موبایل و کد تایید الزامی است."
                };
            }

            var hashedInput = OtpHelper.HashOtp(code);
            var otpEntry = await _context.OtpEntries
                .Where(x => x.PhoneNumber == phone
                            && x.CodeHash == hashedInput
                            && !x.Used)
                .OrderByDescending(x => x.ExpireAt)
                .FirstOrDefaultAsync();

            if (otpEntry == null || otpEntry.ExpireAt < DateTime.UtcNow)
            {
                return new OtpValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "کد تایید منقضی شده یا اشتباه است."
                };
            }

            otpEntry.Used = true;
            await _context.SaveChangesAsync();

            var owner = await _context.Owners.FirstOrDefaultAsync(o => o.Phone == phone);
            if (owner == null)
            {
                var registrationToken = CreateRegistrationProof(phone);
                return new OtpValidationResult
                {
                    IsValid = true,
                    NeedsRegistration = true,
                    RegistrationToken = registrationToken
                };
            }

            return new OtpValidationResult
            {
                IsValid = true,
                Owner = owner
            };
        }

        public string CreateRegistrationProof(string phone)
        {
            phone = NormalizePhone(phone);
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            _cache.Set(RegistrationProofCacheKey(phone), token, RegistrationProofTtl);
            return token;
        }

        public bool ConsumeRegistrationProof(string phone, string registrationToken)
        {
            phone = NormalizePhone(phone);
            if (string.IsNullOrWhiteSpace(registrationToken))
                return false;

            var cacheKey = RegistrationProofCacheKey(phone);
            if (!_cache.TryGetValue(cacheKey, out string storedToken) || storedToken != registrationToken)
                return false;

            _cache.Remove(cacheKey);
            return true;
        }

        public async Task<(Owner? Owner, string? ErrorMessage)> RegisterOwnerAsync(
            string phone,
            string name,
            string password,
            string registrationToken)
        {
            phone = NormalizePhone(phone);
            if (string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(registrationToken))
            {
                return (null, "اطلاعات ناقص است.");
            }

            if (!ConsumeRegistrationProof(phone, registrationToken))
                return (null, "تایید OTP منقضی شده یا نامعتبر است. لطفاً دوباره کد تایید دریافت کنید.");

            var existing = await _context.Owners.FirstOrDefaultAsync(o => o.Phone == phone);
            if (existing != null)
                return (null, "این شماره قبلاً ثبت شده است.");

            var owner = new Owner
            {
                Phone = phone,
                Name = name,
                Password = EncodePassword(password)
            };
            _context.Owners.Add(owner);
            await _context.SaveChangesAsync();

            return (owner, null);
        }

        private static string RegistrationProofCacheKey(string phone) =>
            RegistrationProofPrefix + phone;
    }
}
