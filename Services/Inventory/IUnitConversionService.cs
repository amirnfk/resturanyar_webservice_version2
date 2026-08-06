using resturanyar.Models.Inventory;

namespace resturanyar.Services.Inventory
{
    public interface IUnitConversionService
    {
        Task<IReadOnlyList<InventoryUnitDto>> GetAllActiveUnitsAsync(CancellationToken ct = default);
        Task<IReadOnlyList<InventoryUnitDto>> GetCompatibleUnitsAsync(int baseUnitId, CancellationToken ct = default);
        Task<InventoryUnit> GetRequiredUnitAsync(int unitId, CancellationToken ct = default);
        bool CanConvert(InventoryUnit from, InventoryUnit to);
        decimal Convert(decimal quantity, InventoryUnit from, InventoryUnit to);
        decimal ToBase(decimal quantity, InventoryUnit from, InventoryUnit itemBase);
    }
}
