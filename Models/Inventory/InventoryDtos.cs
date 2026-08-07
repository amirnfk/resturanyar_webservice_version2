using System.ComponentModel.DataAnnotations;

namespace resturanyar.Models.Inventory
{
    public class InventorySettingsDto
    {
        public int RestaurantId { get; set; }
        public bool IsEnabled { get; set; }
        public int AutoDeductStatusId { get; set; } = 4;
    }

    public class SetInventoryEnabledRequest
    {
        [Required]
        public int RestaurantId { get; set; }

        /// <summary>When null, leaves IsEnabled unchanged.</summary>
        public bool? IsEnabled { get; set; }

        /// <summary>Optional; when provided updates deduct status (3, 4, or 5).</summary>
        public int? AutoDeductStatusId { get; set; }
    }

    public class InventoryUnitDto
    {
        public int UnitId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NameFa { get; set; } = string.Empty;
        public string Dimension { get; set; } = string.Empty;
        public decimal ToDimensionBaseFactor { get; set; }
        public bool AllowsCrossUnitConversion { get; set; }
        public int SortOrder { get; set; }
    }

    public class InventoryRecipeLineDto
    {
        public int InventoryItemId { get; set; }
        public string? InventoryItemName { get; set; }
        public decimal Quantity { get; set; }
        public int UnitId { get; set; }
        public string? UnitCode { get; set; }
        public string? UnitNameFa { get; set; }
        public decimal QuantityInBase { get; set; }
        public string? BaseUnitCode { get; set; }
        /// <summary>Legacy alias for UnitCode.</summary>
        public string? Unit { get; set; }
    }

    public class InventoryRecipeDto
    {
        public int? RecipeId { get; set; }
        public int RestaurantId { get; set; }
        public int FoodItemId { get; set; }
        public string? FoodName { get; set; }
        public bool IsActive { get; set; }
        public List<InventoryRecipeLineDto> Lines { get; set; } = new();
    }

    public class SaveInventoryRecipeRequest
    {
        [Required]
        public int RestaurantId { get; set; }

        public List<SaveInventoryRecipeLineRequest> Lines { get; set; } = new();
    }

    public class SaveInventoryRecipeLineRequest
    {
        [Required]
        public int InventoryItemId { get; set; }

        [Range(0.001, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int UnitId { get; set; }
    }

    public class InventoryCategoryDto
    {
        public int InventoryCategoryId { get; set; }
        public int RestaurantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CreateInventoryCategoryRequest
    {
        [Required]
        public int RestaurantId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateInventoryCategoryRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class InventoryMovementDto
    {
        public int MovementId { get; set; }
        public int InventoryItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public string? UnitNameFa { get; set; }
        public decimal DeltaQuantity { get; set; }
        public decimal QuantityAfter { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Note { get; set; }
        /// <summary>Related order when movement came from sale consumption / cancel restore.</summary>
        public int? OrderId { get; set; }
        public decimal? UnitPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CreatedByOwnerId { get; set; }
    }

    public class InventoryItemDto
    {
        public int InventoryItemId { get; set; }
        public int RestaurantId { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string Name { get; set; } = string.Empty;
        public int BaseUnitId { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string? UnitNameFa { get; set; }
        public decimal CurrentQuantity { get; set; }
        public decimal MinimumQuantity { get; set; }
        public decimal? LastPurchasePrice { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public bool IsLowStock { get; set; }
    }

    public class CreateInventoryItemRequest
    {
        [Required]
        public int RestaurantId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue)]
        public int BaseUnitId { get; set; }

        public int? CategoryId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal CurrentQuantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal MinimumQuantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? LastPurchasePrice { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }

    public class UpdateInventoryItemRequest
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue)]
        public int BaseUnitId { get; set; }

        public int? CategoryId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal MinimumQuantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? LastPurchasePrice { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }

    public class AddStockRequest
    {
        [Range(0.001, double.MaxValue)]
        public decimal Quantity { get; set; }

        /// <summary>Entry unit; defaults to item base unit when null.</summary>
        public int? UnitId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? UnitPrice { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }
    }

    public class AdjustStockRequest
    {
        /// <summary>Absolute new quantity. Prefer this for MVP UI.</summary>
        [Range(0, double.MaxValue)]
        public decimal? NewQuantity { get; set; }

        /// <summary>Signed delta if NewQuantity is not provided.</summary>
        public decimal? DeltaQuantity { get; set; }

        [Required]
        [MaxLength(30)]
        public string Reason { get; set; } = InventoryMovementReasons.Adjustment;

        [MaxLength(500)]
        public string? Note { get; set; }
    }

    public class InventoryLowStockHintDto
    {
        public int InventoryItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string? UnitNameFa { get; set; }
        public decimal CurrentQuantity { get; set; }
        public decimal MinimumQuantity { get; set; }
        public decimal Shortage { get; set; }
    }

    public class InventorySummaryDto
    {
        public bool IsEnabled { get; set; }
        public int ItemCount { get; set; }
        public int LowStockCount { get; set; }
        public IReadOnlyList<InventoryLowStockHintDto> LowStockItems { get; set; } = Array.Empty<InventoryLowStockHintDto>();
    }
}
