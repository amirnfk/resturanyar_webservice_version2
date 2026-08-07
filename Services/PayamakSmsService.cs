using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using resturanyar.Models;
using resturanyar.Models.Settings;

namespace resturanyar.Services
{
    public interface IPayamakSmsService
    {
        Task<bool> SendPatternAsync(string to, string bodyId, string text, CancellationToken ct = default);
        Task NotifyAdminPriceListAsync(string fullName, string phoneNumber, CancellationToken ct = default);
        Task<bool> NotifyAdminSupportChatAsync(string text, CancellationToken ct = default);
    }

    public class PayamakSmsService : IPayamakSmsService
    {
        private readonly PayamakSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PayamakSmsService> _logger;

        public PayamakSmsService(
            IOptions<PayamakSettings> options,
            IHttpClientFactory httpClientFactory,
            ILogger<PayamakSmsService> logger)
        {
            _settings = options.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task NotifyAdminPriceListAsync(string fullName, string phoneNumber, CancellationToken ct = default)
        {
            var text = $"{fullName} - {phoneNumber}";
            await SendPatternAsync(_settings.AdminPhoneNumber, _settings.PriceListToAdminBodyId, text, ct);
        }

        public async Task<bool> NotifyAdminSupportChatAsync(string text, CancellationToken ct = default)
        {
            // Reuse resturanyar-pricelist admin pattern; fixed payload is enough to signal "new chat message".
            var bodyId = !string.IsNullOrWhiteSpace(_settings.SupportChatToAdminBodyId)
                ? _settings.SupportChatToAdminBodyId
                : _settings.PriceListToAdminBodyId;

            if (string.IsNullOrWhiteSpace(bodyId))
            {
                _logger.LogWarning("No Payamak bodyId configured for support chat SMS; skipping.");
                return false;
            }

            // Same pattern variables as pricelist admin SMS: "{name} - {phone}"
            const string payload = "امیر - 091";
            return await SendPatternAsync(_settings.AdminPhoneNumber, bodyId, payload, ct);
        }

        public async Task<bool> SendPatternAsync(string to, string bodyId, string text, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(bodyId) || string.IsNullOrWhiteSpace(_settings.BaseUrl))
                return false;

            try
            {
                var smsRequest = new
                {
                    username = _settings.Username,
                    password = _settings.Password,
                    text,
                    to,
                    bodyId
                };

                var client = _httpClientFactory.CreateClient(nameof(PayamakSmsService));
                var json = JsonSerializer.Serialize(smsRequest);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(_settings.BaseUrl, content, ct);
                var responseContent = await response.Content.ReadAsStringAsync(ct);
                var jsonResponse = JsonSerializer.Deserialize<PayamakResponse>(responseContent);

                var ok = response.IsSuccessStatusCode
                         && jsonResponse != null
                         && jsonResponse.RetStatus == 1
                         && string.Equals(jsonResponse.StrRetStatus, "Ok", StringComparison.OrdinalIgnoreCase);

                if (!ok)
                    _logger.LogWarning("Payamak SMS failed. Status={Status} Body={Body}", response.StatusCode, responseContent);

                return ok;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payamak SMS send failed to {To}", to);
                return false;
            }
        }
    }
}
