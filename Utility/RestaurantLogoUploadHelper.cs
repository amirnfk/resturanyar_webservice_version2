namespace resturanyar.Utility
{
    public static class RestaurantLogoUploadHelper
    {
        public const int MaxFileSizeBytes = 512000;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp"
        };

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/png", "image/jpeg", "image/webp"
        };

        public static async Task<(bool Success, string? Url, string? ErrorMessage)> SaveLogoAsync(
            IWebHostEnvironment env,
            IFormFile file,
            int restaurantId)
        {
            if (file == null || file.Length == 0)
                return (false, null, "فایل لوگو ارسال نشده است.");

            if (file.Length > MaxFileSizeBytes)
                return (false, null, "حجم لوگو نباید بیشتر از ۵۰۰ کیلوبایت باشد.");

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                return (false, null, "فرمت لوگو مجاز نیست. فقط PNG، JPG و WebP پذیرفته می‌شود.");

            if (!string.IsNullOrEmpty(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
                return (false, null, "نوع فایل لوگو معتبر نیست.");

            var restaurantFolder = Path.Combine(env.WebRootPath, "uploads", "restaurants", restaurantId.ToString());
            Directory.CreateDirectory(restaurantFolder);

            foreach (var existing in Directory.GetFiles(restaurantFolder, "logo.*"))
            {
                try { File.Delete(existing); } catch { /* ignore stale files */ }
            }

            var safeExtension = extension.ToLowerInvariant();
            var fileName = "logo" + safeExtension;
            var physicalPath = Path.Combine(restaurantFolder, fileName);

            await using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var publicUrl = $"/uploads/restaurants/{restaurantId}/{fileName}";
            return (true, publicUrl, null);
        }

        public static void DeleteRestaurantLogo(IWebHostEnvironment env, int restaurantId)
        {
            var restaurantFolder = Path.Combine(env.WebRootPath, "uploads", "restaurants", restaurantId.ToString());
            if (!Directory.Exists(restaurantFolder))
                return;

            foreach (var existing in Directory.GetFiles(restaurantFolder, "logo.*"))
            {
                try { File.Delete(existing); } catch { /* ignore stale files */ }
            }
        }
    }
}
