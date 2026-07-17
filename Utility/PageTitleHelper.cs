namespace resturanyar.Utility
{
    public static class PageTitleHelper
    {
        public static string WithRestaurant(string? pageTitle, string? restaurantName)
        {
            pageTitle = pageTitle?.Trim() ?? string.Empty;
            restaurantName = restaurantName?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(restaurantName))
                return pageTitle;

            if (string.IsNullOrEmpty(pageTitle))
                return restaurantName;

            if (pageTitle.Contains(restaurantName, StringComparison.Ordinal))
                return pageTitle;

            return $"{pageTitle} - {restaurantName}";
        }
    }
}
