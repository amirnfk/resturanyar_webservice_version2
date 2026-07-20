using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace resturanyar.Utility
{
    public record PublicMenuQrResult(string MenuUrl, string QrCodeImageBase64);

    public static class PublicMenuQrHelper
    {
        public static string BuildMenuUrl(IUrlHelper urlHelper, HttpRequest request, string token)
        {
            return urlHelper.Action("PublicMenu", "Menu", new { token }, request.Scheme)
                ?? $"{request.Scheme}://{request.Host}/Menu/PublicMenu?token={Uri.EscapeDataString(token)}";
        }

        public static PublicMenuQrResult? Build(IUrlHelper urlHelper, HttpRequest request, string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var menuUrl = BuildMenuUrl(urlHelper, request, token);

            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(menuUrl, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new Base64QRCode(qrData);
            var qrCodeImageAsBase64 = qrCode.GetGraphic(20);

            return new PublicMenuQrResult(menuUrl, qrCodeImageAsBase64);
        }
    }
}
