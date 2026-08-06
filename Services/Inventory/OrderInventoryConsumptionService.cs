using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using resturanyar.Models.Inventory;
using Resturanyar.Data;

namespace resturanyar.Services.Inventory
{
    public class OrderInventoryConsumptionService : IOrderInventoryConsumptionService
    {
        private readonly AppDbContext _db;
        private readonly IInventoryService _inventory;
        private readonly IUnitConversionService _units;
        private readonly ILogger<OrderInventoryConsumptionService> _logger;

        public OrderInventoryConsumptionService(
            AppDbContext db,
            IInventoryService inventory,
            IUnitConversionService units,
            ILogger<OrderInventoryConsumptionService> logger)
        {
            _db = db;
            _inventory = inventory;
            _units = units;
            _logger = logger;
        }

        public async Task HandleStatusChangeAsync(
            int orderId,
            int restaurantId,
            int previousStatusId,
            int newStatusId,
            CancellationToken ct = default)
        {
            try
            {
                var settings = await _inventory.GetSettingsIfExistsAsync(restaurantId, ct);
                if (settings == null || !settings.IsEnabled)
                    return;

                var deductStatus = settings.AutoDeductStatusId <= 0 ? 4 : settings.AutoDeductStatusId;

                if (newStatusId is 9 or 10)
                {
                    await TryReverseForOrderAsync(orderId, restaurantId, ct);
                    return;
                }

                // Threshold: deduct on first entry into deductStatus or any later kitchen status
                // (e.g. jump 3→5 still deducts when threshold is 4). Cancel 9/10 handled above.
                var previousAlreadyDeductedZone =
                    previousStatusId >= deductStatus && previousStatusId is not (9 or 10);

                if (newStatusId >= deductStatus && !previousAlreadyDeductedZone)
                    await TryDeductForOrderAsync(orderId, restaurantId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Inventory consumption handler failed for order {OrderId} restaurant {RestaurantId}",
                    orderId, restaurantId);
            }
        }

        public async Task<bool> TryDeductForOrderAsync(int orderId, int restaurantId, CancellationToken ct = default)
        {
            if (!await _inventory.IsEnabledAsync(restaurantId, ct))
                return false;

            var existing = await _db.InventoryOrderConsumptions
                .FirstOrDefaultAsync(c => c.OrderId == orderId, ct);

            if (existing != null && !existing.IsReversed)
                return false; // already deducted

            // If previously reversed, allow a fresh deduct cycle by inserting a new logical consume:
            // Plan uniqueness is on OrderId — so if reversed, we re-deduct by clearing reverse flag
            // and posting new movements. Simpler: if reversed, treat as eligible and update same row.

            var orderItems = await _db.OrderItems.AsNoTracking()
                .Where(oi => oi.OrderId == orderId)
                .Select(oi => new { oi.FoodItemId, oi.Quantity })
                .ToListAsync(ct);

            if (orderItems.Count == 0)
                return false;

            var foodIds = orderItems.Select(oi => oi.FoodItemId).Distinct().ToList();
            var recipes = await _db.InventoryRecipes
                .AsNoTracking()
                .Include(r => r.Lines)
                .ThenInclude(l => l.Unit)
                .Where(r => r.RestaurantId == restaurantId && r.IsActive && foodIds.Contains(r.FoodItemId))
                .ToListAsync(ct);

            if (recipes.Count == 0)
            {
                return false;
            }

            var recipeByFood = recipes.ToDictionary(r => r.FoodItemId);
            var itemIdsNeeded = recipes.SelectMany(r => r.Lines).Select(l => l.InventoryItemId).Distinct().ToList();
            var itemsLookup = await _db.InventoryItems.AsNoTracking()
                .Include(i => i.BaseUnit)
                .Where(i => i.RestaurantId == restaurantId && itemIdsNeeded.Contains(i.InventoryItemId))
                .ToDictionaryAsync(i => i.InventoryItemId, ct);

            var totals = new Dictionary<int, decimal>();

            foreach (var oi in orderItems)
            {
                if (!recipeByFood.TryGetValue(oi.FoodItemId, out var recipe))
                    continue;

                foreach (var line in recipe.Lines)
                {
                    if (!itemsLookup.TryGetValue(line.InventoryItemId, out var catalogItem) || catalogItem.BaseUnit == null)
                        continue;

                    var entryUnit = line.Unit;
                    if (entryUnit == null)
                    {
                        // Fallback: treat quantity as already in base
                        var fallback = line.Quantity * oi.Quantity;
                        if (fallback <= 0) continue;
                        totals.TryGetValue(line.InventoryItemId, out var cur0);
                        totals[line.InventoryItemId] = cur0 + fallback;
                        continue;
                    }

                    var inBase = _units.ToBase(line.Quantity, entryUnit, catalogItem.BaseUnit) * oi.Quantity;
                    if (inBase <= 0) continue;
                    totals.TryGetValue(line.InventoryItemId, out var cur);
                    totals[line.InventoryItemId] = cur + inBase;
                }
            }

            if (totals.Count == 0)
                return false;

            var itemIds = totals.Keys.ToList();
            var items = await _db.InventoryItems
                .Where(i => i.RestaurantId == restaurantId && itemIds.Contains(i.InventoryItemId))
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            foreach (var kv in totals)
            {
                var item = items.FirstOrDefault(i => i.InventoryItemId == kv.Key);
                if (item == null)
                    continue;

                var delta = -kv.Value;
                item.CurrentQuantity += delta; // may go negative
                item.UpdatedAt = now;

                _db.InventoryMovements.Add(new InventoryMovement
                {
                    RestaurantId = restaurantId,
                    InventoryItemId = item.InventoryItemId,
                    DeltaQuantity = delta,
                    QuantityAfter = item.CurrentQuantity,
                    Reason = InventoryMovementReasons.SaleConsumption,
                    Note = $"Order:{orderId}",
                    CreatedAt = now
                });
            }

            if (existing == null)
            {
                _db.InventoryOrderConsumptions.Add(new InventoryOrderConsumption
                {
                    RestaurantId = restaurantId,
                    OrderId = orderId,
                    DeductedAt = now,
                    IsReversed = false
                });
            }
            else
            {
                existing.IsReversed = false;
                existing.ReversedAt = null;
                existing.DeductedAt = now;
            }

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> TryReverseForOrderAsync(int orderId, int restaurantId, CancellationToken ct = default)
        {
            if (!await _inventory.IsEnabledAsync(restaurantId, ct))
                return false;

            var consumption = await _db.InventoryOrderConsumptions
                .FirstOrDefaultAsync(c => c.OrderId == orderId && c.RestaurantId == restaurantId, ct);

            if (consumption == null || consumption.IsReversed)
                return false;

            // Restore only SaleConsumption movements from the current deduct cycle
            var deductedAt = consumption.DeductedAt;
            var movements = await _db.InventoryMovements
                .Where(m => m.RestaurantId == restaurantId
                            && m.Reason == InventoryMovementReasons.SaleConsumption
                            && m.Note == $"Order:{orderId}"
                            && m.CreatedAt >= deductedAt)
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            var itemIds = movements.Select(m => m.InventoryItemId).Distinct().ToList();
            var items = await _db.InventoryItems
                .Where(i => i.RestaurantId == restaurantId && itemIds.Contains(i.InventoryItemId))
                .ToListAsync(ct);

            foreach (var group in movements.GroupBy(m => m.InventoryItemId))
            {
                // Original deltas are negative; reverse = negate sum
                var restore = -group.Sum(m => m.DeltaQuantity);
                if (restore == 0) continue;

                var item = items.FirstOrDefault(i => i.InventoryItemId == group.Key);
                if (item == null) continue;

                item.CurrentQuantity += restore;
                item.UpdatedAt = now;

                _db.InventoryMovements.Add(new InventoryMovement
                {
                    RestaurantId = restaurantId,
                    InventoryItemId = item.InventoryItemId,
                    DeltaQuantity = restore,
                    QuantityAfter = item.CurrentQuantity,
                    Reason = InventoryMovementReasons.Correction,
                    Note = $"Cancel order {orderId}",
                    CreatedAt = now
                });
            }

            consumption.IsReversed = true;
            consumption.ReversedAt = now;
            await _db.SaveChangesAsync(ct);
            return true;
        }
    }
}
