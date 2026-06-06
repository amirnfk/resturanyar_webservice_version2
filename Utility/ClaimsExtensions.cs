namespace resturanyar.Utility
{
    using System.Security.Claims;

    public static class ClaimsPrincipalExtensions
    {
        public static int? GetRestaurantId(this ClaimsPrincipal user)
        {
            var v = user?.FindFirst("RestaurantId")?.Value;
            if (int.TryParse(v, out var id)) return id;
            return null;
        }

        public static int? GetOwnerId(this ClaimsPrincipal user)
        {
            var v = user?.FindFirst("OwnerId")?.Value;
            if (int.TryParse(v, out var id)) return id;
            return null;
        }

        public static int? GetUserId(this ClaimsPrincipal user)
        {
            var v = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(v, out var id)) return id;
            return null;
        }
    }

}
