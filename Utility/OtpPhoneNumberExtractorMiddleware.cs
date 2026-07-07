using System.Text;
using System.Text.Json;

namespace resturanyar.Utility
{
    // OtpPhoneNumberExtractorMiddleware.cs
   

    public class OtpPhoneNumberExtractorMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public OtpPhoneNumberExtractorMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only run for the OTP request endpoint
            if (context.Request.Path.StartsWithSegments("/api/UserApi/otprequest"))
            {
                context.Request.EnableBuffering(); // Allow reading the body multiple times

                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0; // Reset for later reads

                if (!string.IsNullOrEmpty(body))
                {
                    try
                    {
                        var json = JsonDocument.Parse(body);
                        if (json.RootElement.TryGetProperty("PhoneNumber", out var phoneProp))
                        {
                            var phone = phoneProp.GetString();
                            if (!string.IsNullOrEmpty(phone))
                            {
                                // Store for later use in rate limiter
                                context.Items["OtpPhoneNumber"] = phone;
                            }
                        }
                    }
                    catch { /* ignore malformed JSON */ }
                }
            }

            await _next(context);
        }
    }
}
