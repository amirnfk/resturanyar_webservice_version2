using resturanyar.Models.Inventory;

namespace resturanyar.Services.Inventory
{
    public interface IInventoryRecipeService
    {
        Task<InventoryRecipeDto> GetRecipeAsync(int restaurantId, int foodItemId, CancellationToken ct = default);
        Task<InventoryRecipeDto> SaveRecipeAsync(int restaurantId, int foodItemId, SaveInventoryRecipeRequest request, CancellationToken ct = default);
        Task<IReadOnlyList<int>> GetFoodIdsWithRecipesAsync(int restaurantId, CancellationToken ct = default);
        Task DeleteRecipeAsync(int restaurantId, int foodItemId, CancellationToken ct = default);
    }
}
