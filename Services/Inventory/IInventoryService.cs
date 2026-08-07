using resturanyar.Models.Inventory;

namespace resturanyar.Services.Inventory
{
    public interface IInventoryService
    {
        Task<InventorySettingsDto> GetSettingsAsync(int restaurantId, CancellationToken ct = default);
        Task<InventorySettingsDto> SetEnabledAsync(int restaurantId, bool isEnabled, CancellationToken ct = default);
        Task<InventorySettingsDto> UpdateSettingsAsync(int restaurantId, bool? isEnabled, int? autoDeductStatusId, CancellationToken ct = default);
        Task<bool> IsEnabledAsync(int restaurantId, CancellationToken ct = default);
        Task<InventorySettingsDto?> GetSettingsIfExistsAsync(int restaurantId, CancellationToken ct = default);

        Task<IReadOnlyList<InventoryItemDto>> ListItemsAsync(int restaurantId, bool activeOnly = true, CancellationToken ct = default);
        Task<InventoryItemDto?> GetItemAsync(int restaurantId, int itemId, CancellationToken ct = default);
        Task<InventoryItemDto> CreateItemAsync(CreateInventoryItemRequest request, int? ownerId, CancellationToken ct = default);
        Task<InventoryItemDto?> UpdateItemAsync(int restaurantId, int itemId, UpdateInventoryItemRequest request, CancellationToken ct = default);
        Task<bool> DeactivateItemAsync(int restaurantId, int itemId, CancellationToken ct = default);

        Task<InventoryItemDto?> AddStockAsync(int restaurantId, int itemId, AddStockRequest request, int? ownerId, CancellationToken ct = default);
        Task<InventoryItemDto?> AdjustStockAsync(int restaurantId, int itemId, AdjustStockRequest request, int? ownerId, CancellationToken ct = default);

        Task<IReadOnlyList<InventoryItemDto>> GetLowStockItemsAsync(int restaurantId, CancellationToken ct = default);
        Task<InventorySummaryDto> GetSummaryAsync(int restaurantId, int lowStockTake = 4, CancellationToken ct = default);

        Task<IReadOnlyList<InventoryCategoryDto>> ListCategoriesAsync(int restaurantId, bool activeOnly = true, CancellationToken ct = default);
        Task<InventoryCategoryDto> CreateCategoryAsync(CreateInventoryCategoryRequest request, CancellationToken ct = default);
        Task<InventoryCategoryDto?> UpdateCategoryAsync(int restaurantId, int categoryId, UpdateInventoryCategoryRequest request, CancellationToken ct = default);
        Task<bool> DeactivateCategoryAsync(int restaurantId, int categoryId, CancellationToken ct = default);

        Task<IReadOnlyList<InventoryMovementDto>> ListMovementsAsync(
            int restaurantId,
            int? inventoryItemId = null,
            string? reason = null,
            DateTime? fromUtc = null,
            DateTime? toUtcExclusive = null,
            int skip = 0,
            int take = 50,
            CancellationToken ct = default);
    }
}
