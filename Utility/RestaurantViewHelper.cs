using Microsoft.EntityFrameworkCore;
using Resturanyar.Data;
using System.Security.Claims;

namespace resturanyar.Utility
{
    public static class RestaurantViewHelper
    {
        public static string ResolveRestaurantName(ClaimsPrincipal user, AppDbContext db, object? viewBagRestaurantName)
        {
            if (viewBagRestaurantName is string name && !string.IsNullOrWhiteSpace(name))
                return name;

            var restaurantId = user.GetRestaurantId();
            if (restaurantId == null)
                return string.Empty;

            return db.Restaurants
                .AsNoTracking()
                .Where(r => r.restaurant_id == restaurantId.Value)
                .Select(r => r.name)
                .FirstOrDefault() ?? string.Empty;
        }
    }
}
