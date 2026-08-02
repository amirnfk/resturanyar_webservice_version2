using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

using resturanyar.Models;

using Resturanyar.Data;

namespace resturanyar.Utility
{
    public static class RestaurantSettingsHelper
    {
        public const string DefaultPrimaryColor = "#f97316";
        public const string DefaultSecondaryColor = "#f97316";
        public const string DefaultLogoPath = "/images/logo.png";
        public const int MaxMenuHeroBadgeLength = 80;
        public const int MaxMenuTaglineLength = 160;

        public static string DefaultBackgroundPath => RestaurantBackgroundOptions.DefaultUrl;

        public static async Task<RestaurantSetting?> GetSettingsAsync(AppDbContext context, int restaurantId)
        {
            return await context.RestaurantSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);
        }

        public static async Task<RestaurantSettingDto> GetSettingsDtoSafeAsync(AppDbContext context, int restaurantId)
        {
            try
            {
                var settings = await GetSettingsAsync(context, restaurantId);
                return ToResolvedDto(settings);
            }
            catch
            {
                return ToResolvedDto(null);
            }
        }

        public static async Task<RestaurantSetting> GetOrCreateSettingsAsync(AppDbContext context, int restaurantId)
        {
            var settings = await context.RestaurantSettings
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId);

            if (settings != null)
                return settings;

            settings = new RestaurantSetting
            {
                RestaurantId = restaurantId,
                PrimaryColor = DefaultPrimaryColor,
                SecondaryColor = DefaultSecondaryColor
            };

            context.RestaurantSettings.Add(settings);
            await context.SaveChangesAsync();
            return settings;
        }

        public static RestaurantSettingDto ToDto(RestaurantSetting? settings)
        {
            return new RestaurantSettingDto
            {
                PrimaryColor = string.IsNullOrWhiteSpace(settings?.PrimaryColor)
                    ? DefaultPrimaryColor
                    : settings.PrimaryColor,
                SecondaryColor = string.IsNullOrWhiteSpace(settings?.SecondaryColor)
                    ? DefaultSecondaryColor
                    : settings.SecondaryColor,
                BackgroundImageUrl = settings?.BackgroundImageUrl,
                LogoUrl = settings?.LogoUrl,
                MenuHeroBadgeCustom = NormalizeOptionalText(settings?.MenuHeroBadge, MaxMenuHeroBadgeLength),
                MenuTaglineCustom = NormalizeOptionalText(settings?.MenuTagline, MaxMenuTaglineLength)
            };
        }

        public static RestaurantSettingDto ToResolvedDto(RestaurantSetting? settings)
        {
            var dto = ToDto(settings);
            dto.BackgroundImageUrl = RestaurantBackgroundOptions.ResolveUrl(dto.BackgroundImageUrl);
            dto.LogoUrl = ResolveAssetUrl(dto.LogoUrl, DefaultLogoPath);

            var template = RestaurantMenuTemplates.FromBackgroundUrl(dto.BackgroundImageUrl);
            dto.MenuHeroBadgeDefault = template.HeroBadge;
            dto.MenuTaglineDefault = template.Tagline;
            dto.MenuHeroBadge = ResolveMenuHeroBadge(settings?.MenuHeroBadge, template);
            dto.MenuTagline = ResolveMenuTagline(settings?.MenuTagline, template);

            return dto;
        }

        public static string ResolveAssetUrl(string? url, string defaultPath)
        {
            return string.IsNullOrWhiteSpace(url) ? defaultPath : url.Trim();
        }

        public static string ResolveMenuHeroBadge(string? customValue, RestaurantMenuTemplate template)
        {
            var normalized = NormalizeOptionalText(customValue, MaxMenuHeroBadgeLength);
            return normalized ?? template.HeroBadge;
        }

        public static string ResolveMenuTagline(string? customValue, RestaurantMenuTemplate template)
        {
            var normalized = NormalizeOptionalText(customValue, MaxMenuTaglineLength);
            return normalized ?? template.Tagline;
        }

        public static void PopulateMenuPresentation(
            ViewDataDictionary viewData,
            RestaurantSettingDto settingsDto,
            string restaurantName)
        {
            var menuTemplate = RestaurantMenuTemplates.FromBackgroundUrl(settingsDto.BackgroundImageUrl);

            viewData["RestaurantName"] = restaurantName;
            viewData["LogoUrl"] = ResolveAssetUrl(settingsDto.LogoUrl, DefaultLogoPath);
            viewData["MenuTemplateId"] = menuTemplate.Id;
            viewData["MenuTemplateTagline"] = settingsDto.MenuTagline;
            viewData["MenuTemplateBadge"] = settingsDto.MenuHeroBadge;
            viewData["MenuTemplateEmoji"] = menuTemplate.HeroEmoji;
            viewData["MenuThemeColor"] = menuTemplate.PrimaryColor;
            viewData["MenuTypeBackgroundUrl"] = settingsDto.BackgroundImageUrl;
        }

        public static string? NormalizeOptionalText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            if (trimmed.Length > maxLength)
                trimmed = trimmed[..maxLength];

            return trimmed;
        }

        public static bool IsValidHexColor(string? color)
        {
            return !string.IsNullOrWhiteSpace(color)
                && System.Text.RegularExpressions.Regex.IsMatch(color.Trim(), @"^#[0-9A-Fa-f]{6}$");
        }

        public static async Task<(bool Success, string? ErrorMessage, RestaurantSettingDto? Data)> SaveSettingsAsync(
            AppDbContext context,
            IWebHostEnvironment env,
            int restaurantId,
            string? backgroundImageUrl,
            string? menuHeroBadge,
            string? menuTagline,
            IFormFile? logo)
        {
            var normalizedBackground = string.IsNullOrWhiteSpace(backgroundImageUrl)
                ? null
                : backgroundImageUrl.Trim();

            if (normalizedBackground != null)
            {
                normalizedBackground = RestaurantBackgroundOptions.ResolveUrl(normalizedBackground);
                if (!RestaurantBackgroundOptions.IsAllowed(normalizedBackground))
                    return (false, "پس‌زمینه انتخاب‌شده معتبر نیست.", null);
            }

            var normalizedBadge = NormalizeOptionalText(menuHeroBadge, MaxMenuHeroBadgeLength);
            var normalizedTagline = NormalizeOptionalText(menuTagline, MaxMenuTaglineLength);

            var settings = await GetOrCreateSettingsAsync(context, restaurantId);
            settings.BackgroundImageUrl = normalizedBackground;
            settings.MenuHeroBadge = normalizedBadge;
            settings.MenuTagline = normalizedTagline;

            if (logo != null && logo.Length > 0)
            {
                var uploadResult = await RestaurantLogoUploadHelper.SaveLogoAsync(env, logo, restaurantId);
                if (!uploadResult.Success)
                    return (false, uploadResult.ErrorMessage, null);

                settings.LogoUrl = uploadResult.Url;
            }

            await context.SaveChangesAsync();
            return (true, null, ToResolvedDto(settings));
        }

        public static async Task<RestaurantSettingDto> ResetToDefaultsAsync(
            AppDbContext context,
            IWebHostEnvironment env,
            int restaurantId)
        {
            var settings = await GetOrCreateSettingsAsync(context, restaurantId);
            settings.BackgroundImageUrl = null;
            settings.LogoUrl = null;
            settings.MenuHeroBadge = null;
            settings.MenuTagline = null;

            RestaurantLogoUploadHelper.DeleteRestaurantLogo(env, restaurantId);

            await context.SaveChangesAsync();
            return ToResolvedDto(settings);
        }
    }
}
