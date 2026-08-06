using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using resturanyar.Models.Inventory;
using Resturanyar.Data;

namespace resturanyar.Services.Inventory
{
    public class UnitConversionService : IUnitConversionService
    {
        private const string CacheKey = "inventory:units:all";
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public UnitConversionService(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<IReadOnlyList<InventoryUnitDto>> GetAllActiveUnitsAsync(CancellationToken ct = default)
        {
            var units = await GetActiveUnitsAsync(ct);
            return units.Select(ToDto).ToList();
        }

        public async Task<IReadOnlyList<InventoryUnitDto>> GetCompatibleUnitsAsync(int baseUnitId, CancellationToken ct = default)
        {
            var baseUnit = await GetRequiredUnitAsync(baseUnitId, ct);
            var units = await GetActiveUnitsAsync(ct);
            return units
                .Where(u => CanConvert(u, baseUnit))
                .OrderBy(u => u.SortOrder)
                .ThenBy(u => u.Code)
                .Select(ToDto)
                .ToList();
        }

        public async Task<InventoryUnit> GetRequiredUnitAsync(int unitId, CancellationToken ct = default)
        {
            var units = await GetActiveUnitsAsync(ct);
            var unit = units.FirstOrDefault(u => u.UnitId == unitId);
            if (unit == null)
                throw new InvalidOperationException("واحد اندازه‌گیری معتبر نیست.");
            return unit;
        }

        public bool CanConvert(InventoryUnit from, InventoryUnit to)
        {
            if (from == null || to == null) return false;
            if (from.UnitId == to.UnitId) return true;
            if (!string.Equals(from.Dimension, to.Dimension, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!from.AllowsCrossUnitConversion || !to.AllowsCrossUnitConversion)
                return false;
            if (from.ToDimensionBaseFactor <= 0 || to.ToDimensionBaseFactor <= 0)
                return false;
            return true;
        }

        public decimal Convert(decimal quantity, InventoryUnit from, InventoryUnit to)
        {
            if (from.UnitId == to.UnitId)
                return quantity;

            if (!CanConvert(from, to))
                throw new InvalidOperationException(
                    $"تبدیل واحد از «{from.Code}» به «{to.Code}» مجاز نیست.");

            return quantity * (from.ToDimensionBaseFactor / to.ToDimensionBaseFactor);
        }

        public decimal ToBase(decimal quantity, InventoryUnit from, InventoryUnit itemBase)
            => Convert(quantity, from, itemBase);

        private async Task<List<InventoryUnit>> GetActiveUnitsAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(CacheKey, out List<InventoryUnit>? cached) && cached != null)
                return cached;

            var units = await _db.InventoryUnits.AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.SortOrder)
                .ThenBy(u => u.Code)
                .ToListAsync(ct);

            _cache.Set(CacheKey, units, TimeSpan.FromMinutes(30));
            return units;
        }

        private static InventoryUnitDto ToDto(InventoryUnit u) => new()
        {
            UnitId = u.UnitId,
            Code = u.Code,
            NameFa = u.NameFa,
            Dimension = u.Dimension,
            ToDimensionBaseFactor = u.ToDimensionBaseFactor,
            AllowsCrossUnitConversion = u.AllowsCrossUnitConversion,
            SortOrder = u.SortOrder
        };
    }
}
