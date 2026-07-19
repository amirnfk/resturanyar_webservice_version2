using Microsoft.EntityFrameworkCore;

using resturanyar.Models;

using Resturanyar.Data;



namespace resturanyar.Utility

{

    public static class RestaurantSettingsHelper

    {

        public const string DefaultPrimaryColor = "#f97316";

        public const string DefaultSecondaryColor = "#fff7ed";

        public const string DefaultLogoPath = "/images/logo.png";

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

                LogoUrl = settings?.LogoUrl

            };

        }



        public static RestaurantSettingDto ToResolvedDto(RestaurantSetting? settings)

        {

            var dto = ToDto(settings);

            dto.BackgroundImageUrl = ResolveAssetUrl(dto.BackgroundImageUrl, DefaultBackgroundPath);

            dto.LogoUrl = ResolveAssetUrl(dto.LogoUrl, DefaultLogoPath);

            return dto;

        }



        public static string ResolveAssetUrl(string? url, string defaultPath)

        {

            return string.IsNullOrWhiteSpace(url) ? defaultPath : url.Trim();

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

            string primaryColor,

            string secondaryColor,

            string? backgroundImageUrl,

            IFormFile? logo)

        {

            if (!IsValidHexColor(primaryColor) || !IsValidHexColor(secondaryColor))

                return (false, "فرمت رنگ معتبر نیست. از #RRGGBB استفاده کنید.", null);



            var normalizedBackground = string.IsNullOrWhiteSpace(backgroundImageUrl)

                ? null

                : backgroundImageUrl.Trim();



            if (normalizedBackground != null && !RestaurantBackgroundOptions.IsAllowed(normalizedBackground))

                return (false, "پس‌زمینه انتخاب‌شده معتبر نیست.", null);



            var settings = await GetOrCreateSettingsAsync(context, restaurantId);

            settings.PrimaryColor = primaryColor.Trim();

            settings.SecondaryColor = secondaryColor.Trim();

            settings.BackgroundImageUrl = normalizedBackground;



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

            settings.PrimaryColor = DefaultPrimaryColor;

            settings.SecondaryColor = DefaultSecondaryColor;

            settings.BackgroundImageUrl = null;

            settings.LogoUrl = null;



            RestaurantLogoUploadHelper.DeleteRestaurantLogo(env, restaurantId);



            await context.SaveChangesAsync();

            return ToResolvedDto(settings);

        }

    }

}

