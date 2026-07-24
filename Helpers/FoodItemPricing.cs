namespace resturanyar.Helpers
{
    public static class FoodItemPricing
    {
        public static decimal? NormalizeDiscountPrice(decimal price, decimal? discountPrice)
            => discountPrice is > 0 and var d && d < price ? d : null;

        public static decimal NormalizeCostPrice(decimal? costPrice)
            => costPrice ?? 0;

        public static decimal GetEffectiveSellingPrice(decimal price, decimal? discountPrice)
            => NormalizeDiscountPrice(price, discountPrice) ?? price;
    }
}
