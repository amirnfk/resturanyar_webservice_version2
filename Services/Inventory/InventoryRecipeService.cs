using Microsoft.EntityFrameworkCore;
using resturanyar.Models.Inventory;
using Resturanyar.Data;

namespace resturanyar.Services.Inventory
{
    public class InventoryRecipeService : IInventoryRecipeService
    {
        private readonly AppDbContext _db;
        private readonly IInventoryService _inventory;
        private readonly IUnitConversionService _units;

        public InventoryRecipeService(
            AppDbContext db,
            IInventoryService inventory,
            IUnitConversionService units)
        {
            _db = db;
            _inventory = inventory;
            _units = units;
        }

        public async Task<InventoryRecipeDto> GetRecipeAsync(int restaurantId, int foodItemId, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);
            await EnsureFoodBelongsAsync(restaurantId, foodItemId, ct);

            var recipe = await _db.InventoryRecipes.AsNoTracking()
                .Include(r => r.Lines)
                .ThenInclude(l => l.InventoryItem!)
                .ThenInclude(i => i.BaseUnit)
                .Include(r => r.Lines)
                .ThenInclude(l => l.Unit)
                .FirstOrDefaultAsync(r =>
                    r.RestaurantId == restaurantId
                    && r.FoodItemId == foodItemId
                    && r.IsActive, ct);

            var foodName = await _db.FoodItems.AsNoTracking()
                .Where(f => f.FoodItemId == foodItemId)
                .Select(f => f.Name)
                .FirstOrDefaultAsync(ct);

            if (recipe == null)
            {
                return new InventoryRecipeDto
                {
                    RestaurantId = restaurantId,
                    FoodItemId = foodItemId,
                    FoodName = foodName,
                    IsActive = false,
                    Lines = new List<InventoryRecipeLineDto>()
                };
            }

            return MapRecipe(recipe, foodName);
        }

        public async Task<InventoryRecipeDto> SaveRecipeAsync(
            int restaurantId,
            int foodItemId,
            SaveInventoryRecipeRequest request,
            CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);
            await EnsureFoodBelongsAsync(restaurantId, foodItemId, ct);

            var lines = (request.Lines ?? new List<SaveInventoryRecipeLineRequest>())
                .Where(l => l.InventoryItemId > 0 && l.Quantity > 0 && l.UnitId > 0)
                .GroupBy(l => new { l.InventoryItemId, l.UnitId })
                .Select(g => new SaveInventoryRecipeLineRequest
                {
                    InventoryItemId = g.Key.InventoryItemId,
                    UnitId = g.Key.UnitId,
                    Quantity = g.Sum(x => x.Quantity)
                })
                .ToList();

            if (lines.Count == 0)
                throw new InvalidOperationException("حداقل یک ماده اولیه با مقدار و واحد معتبر لازم است.");

            // Merge same item with different entry units into one line in base? Keep separate entry units
            // but plan groups by inventory item — convert to same unit if duplicates of same item.
            // Better: group by inventory item after converting to a chosen unit — keep first unitId and convert others.
            lines = await MergeLinesByItemAsync(restaurantId, lines, ct);

            var itemIds = lines.Select(l => l.InventoryItemId).ToList();
            var items = await _db.InventoryItems.AsNoTracking()
                .Include(i => i.BaseUnit)
                .Where(i => i.RestaurantId == restaurantId && i.IsActive && itemIds.Contains(i.InventoryItemId))
                .ToListAsync(ct);

            if (items.Count != itemIds.Count)
                throw new InvalidOperationException("یکی از مواد اولیه انتخاب‌شده معتبر نیست.");

            foreach (var line in lines)
            {
                var item = items.First(i => i.InventoryItemId == line.InventoryItemId);
                if (item.BaseUnit == null)
                    throw new InvalidOperationException($"واحد پایه «{item.Name}» تنظیم نشده است.");

                var entryUnit = await _units.GetRequiredUnitAsync(line.UnitId, ct);
                if (!_units.CanConvert(entryUnit, item.BaseUnit))
                    throw new InvalidOperationException(
                        $"واحد «{entryUnit.Code}» با واحد پایه «{item.BaseUnit.Code}» برای «{item.Name}» سازگار نیست.");
            }

            var recipe = await _db.InventoryRecipes
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r =>
                    r.RestaurantId == restaurantId
                    && r.FoodItemId == foodItemId
                    && r.IsActive, ct);

            var now = DateTime.UtcNow;
            if (recipe == null)
            {
                recipe = new InventoryRecipe
                {
                    RestaurantId = restaurantId,
                    FoodItemId = foodItemId,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.InventoryRecipes.Add(recipe);
            }
            else
            {
                _db.InventoryRecipeLines.RemoveRange(recipe.Lines);
                recipe.Lines.Clear();
                recipe.UpdatedAt = now;
            }

            foreach (var line in lines)
            {
                recipe.Lines.Add(new InventoryRecipeLine
                {
                    InventoryItemId = line.InventoryItemId,
                    UnitId = line.UnitId,
                    Quantity = line.Quantity
                });
            }

            await _db.SaveChangesAsync(ct);
            return await GetRecipeAsync(restaurantId, foodItemId, ct);
        }

        public async Task DeleteRecipeAsync(int restaurantId, int foodItemId, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);

            var recipe = await _db.InventoryRecipes
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r =>
                    r.RestaurantId == restaurantId
                    && r.FoodItemId == foodItemId
                    && r.IsActive, ct);

            if (recipe == null)
                return;

            recipe.IsActive = false;
            recipe.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<int>> GetFoodIdsWithRecipesAsync(int restaurantId, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);

            return await _db.InventoryRecipes.AsNoTracking()
                .Where(r => r.RestaurantId == restaurantId && r.IsActive)
                .Select(r => r.FoodItemId)
                .Distinct()
                .ToListAsync(ct);
        }

        private async Task<List<SaveInventoryRecipeLineRequest>> MergeLinesByItemAsync(
            int restaurantId,
            List<SaveInventoryRecipeLineRequest> lines,
            CancellationToken ct)
        {
            var itemIds = lines.Select(l => l.InventoryItemId).Distinct().ToList();
            var items = await _db.InventoryItems.AsNoTracking()
                .Include(i => i.BaseUnit)
                .Where(i => i.RestaurantId == restaurantId && itemIds.Contains(i.InventoryItemId))
                .ToDictionaryAsync(i => i.InventoryItemId, ct);

            var merged = new List<SaveInventoryRecipeLineRequest>();
            foreach (var group in lines.GroupBy(l => l.InventoryItemId))
            {
                if (!items.TryGetValue(group.Key, out var item) || item.BaseUnit == null)
                {
                    merged.AddRange(group);
                    continue;
                }

                // Keep first unit as display unit; sum quantities converted into that unit
                var first = group.First();
                var targetUnit = await _units.GetRequiredUnitAsync(first.UnitId, ct);
                decimal totalInTarget = 0;
                foreach (var line in group)
                {
                    var fromUnit = await _units.GetRequiredUnitAsync(line.UnitId, ct);
                    var inBase = _units.ToBase(line.Quantity, fromUnit, item.BaseUnit);
                    totalInTarget += _units.Convert(inBase, item.BaseUnit, targetUnit);
                }

                merged.Add(new SaveInventoryRecipeLineRequest
                {
                    InventoryItemId = group.Key,
                    UnitId = first.UnitId,
                    Quantity = totalInTarget
                });
            }

            return merged;
        }

        private async Task EnsureEnabledAsync(int restaurantId, CancellationToken ct)
        {
            if (!await _inventory.IsEnabledAsync(restaurantId, ct))
                throw new InvalidOperationException("ماژول انبار برای این رستوران فعال نیست.");
        }

        private async Task EnsureFoodBelongsAsync(int restaurantId, int foodItemId, CancellationToken ct)
        {
            var ok = await _db.FoodItems.AsNoTracking()
                .AnyAsync(f => f.FoodItemId == foodItemId && f.RestaurantId == restaurantId && f.IsActive, ct);
            if (!ok)
                throw new InvalidOperationException("غذای انتخاب‌شده معتبر نیست.");
        }

        private InventoryRecipeDto MapRecipe(InventoryRecipe recipe, string? foodName) => new()
        {
            RecipeId = recipe.RecipeId,
            RestaurantId = recipe.RestaurantId,
            FoodItemId = recipe.FoodItemId,
            FoodName = foodName,
            IsActive = recipe.IsActive,
            Lines = recipe.Lines.Select(l =>
            {
                var entryUnit = l.Unit;
                var baseUnit = l.InventoryItem?.BaseUnit;
                decimal qtyInBase = l.Quantity;
                if (entryUnit != null && baseUnit != null)
                {
                    try { qtyInBase = _units.ToBase(l.Quantity, entryUnit, baseUnit); }
                    catch { qtyInBase = l.Quantity; }
                }

                return new InventoryRecipeLineDto
                {
                    InventoryItemId = l.InventoryItemId,
                    InventoryItemName = l.InventoryItem?.Name,
                    Quantity = l.Quantity,
                    UnitId = l.UnitId,
                    UnitCode = entryUnit?.Code ?? baseUnit?.Code,
                    UnitNameFa = entryUnit?.NameFa,
                    Unit = entryUnit?.Code ?? baseUnit?.Code,
                    QuantityInBase = qtyInBase,
                    BaseUnitCode = baseUnit?.Code
                };
            }).ToList()
        };
    }
}
