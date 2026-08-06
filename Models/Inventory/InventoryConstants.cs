namespace resturanyar.Models.Inventory
{
    public static class InventoryMovementReasons
    {
        public const string Opening = "Opening";
        public const string Purchase = "Purchase";
        public const string Adjustment = "Adjustment";
        public const string Waste = "Waste";
        public const string Correction = "Correction";
        public const string SaleConsumption = "SaleConsumption";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Opening, Purchase, Adjustment, Waste, Correction, SaleConsumption
        };

        public static readonly HashSet<string> AdjustAllowed = new(StringComparer.OrdinalIgnoreCase)
        {
            Opening, Adjustment, Waste, Correction
        };
    }

    // InventoryUnits catalog moved to DB table InventoryUnit (see IUnitConversionService).
}
