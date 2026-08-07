using Microsoft.EntityFrameworkCore;
using resturanyar.Models.Inventory;
using Resturanyar.Data;

namespace resturanyar.Services.Inventory
{
    public class InventoryService : IInventoryService
    {
        private readonly AppDbContext _db;
        private readonly IUnitConversionService _units;

        public InventoryService(AppDbContext db, IUnitConversionService units)
        {
            _db = db;
            _units = units;
        }

        public async Task<InventorySettingsDto> GetSettingsAsync(int restaurantId, CancellationToken ct = default)
        {
            try
            {
                var settings = await GetOrCreateSettingsAsync(restaurantId, ct);
                return ToSettingsDto(settings);
            }
            catch (Exception)
            {
                var enabled = await IsEnabledAsync(restaurantId, ct);
                return new InventorySettingsDto
                {
                    RestaurantId = restaurantId,
                    IsEnabled = enabled,
                    AutoDeductStatusId = 4
                };
            }
        }

        public async Task<InventorySettingsDto> SetEnabledAsync(int restaurantId, bool isEnabled, CancellationToken ct = default)
        {
            return await UpdateSettingsAsync(restaurantId, isEnabled, autoDeductStatusId: null, ct);
        }

        public async Task<InventorySettingsDto> UpdateSettingsAsync(
            int restaurantId,
            bool? isEnabled,
            int? autoDeductStatusId,
            CancellationToken ct = default)
        {
            var settings = await GetOrCreateSettingsAsync(restaurantId, ct);

            if (isEnabled.HasValue)
                settings.IsEnabled = isEnabled.Value;

            if (autoDeductStatusId.HasValue)
            {
                if (autoDeductStatusId.Value is not (3 or 4 or 5))
                    throw new InvalidOperationException("وضعیت کسر خودکار باید یکی از ۳، ۴ یا ۵ باشد.");
                settings.AutoDeductStatusId = autoDeductStatusId.Value;
            }

            settings.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return ToSettingsDto(settings);
        }

        public async Task<bool> IsEnabledAsync(int restaurantId, CancellationToken ct = default)
        {
            return await _db.InventorySettings.AsNoTracking()
                .Where(s => s.RestaurantId == restaurantId)
                .Select(s => s.IsEnabled)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<InventorySettingsDto?> GetSettingsIfExistsAsync(int restaurantId, CancellationToken ct = default)
        {
            try
            {
                var settings = await _db.InventorySettings.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId, ct);
                return settings == null ? null : ToSettingsDto(settings);
            }
            catch
            {
                var enabled = await _db.InventorySettings.AsNoTracking()
                    .Where(s => s.RestaurantId == restaurantId)
                    .Select(s => (bool?)s.IsEnabled)
                    .FirstOrDefaultAsync(ct);
                if (enabled == null) return null;
                return new InventorySettingsDto
                {
                    RestaurantId = restaurantId,
                    IsEnabled = enabled.Value,
                    AutoDeductStatusId = 4
                };
            }
        }

        public async Task<IReadOnlyList<InventoryItemDto>> ListItemsAsync(int restaurantId, bool activeOnly = true, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);

            var query = _db.InventoryItems.AsNoTracking()
                .Include(i => i.Category)
                .Include(i => i.BaseUnit)
                .Where(i => i.RestaurantId == restaurantId);

            if (activeOnly)
                query = query.Where(i => i.IsActive);

            var items = await query
                .OrderBy(i => i.Name)
                .ToListAsync(ct);

            return items.Select(ToItemDto).ToList();
        }

        public async Task<InventoryItemDto?> GetItemAsync(int restaurantId, int itemId, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);

            var item = await _db.InventoryItems.AsNoTracking()
                .Include(i => i.Category)
                .Include(i => i.BaseUnit)
                .FirstOrDefaultAsync(i => i.RestaurantId == restaurantId && i.InventoryItemId == itemId, ct);

            return item == null ? null : ToItemDto(item);
        }

        public async Task<InventoryItemDto> CreateItemAsync(CreateInventoryItemRequest request, int? ownerId, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(request.RestaurantId, ct);
            var baseUnit = await _units.GetRequiredUnitAsync(request.BaseUnitId, ct);
            await ValidateCategoryAsync(request.RestaurantId, request.CategoryId, ct);

            if (request.CurrentQuantity < 0)
                throw new InvalidOperationException("موجودی نمی‌تواند منفی باشد.");
            if (request.MinimumQuantity < 0)
                throw new InvalidOperationException("حداقل موجودی نمی‌تواند منفی باشد.");

            var now = DateTime.UtcNow;
            var item = new InventoryItem
            {
                RestaurantId = request.RestaurantId,
                Name = request.Name.Trim(),
                BaseUnitId = baseUnit.UnitId,
                Unit = baseUnit.Code,
                CategoryId = request.CategoryId,
                CurrentQuantity = 0,
                MinimumQuantity = request.MinimumQuantity,
                LastPurchasePrice = request.LastPurchasePrice,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.InventoryItems.Add(item);
            await _db.SaveChangesAsync(ct);

            if (request.CurrentQuantity > 0)
            {
                item.CurrentQuantity = request.CurrentQuantity;
                item.UpdatedAt = DateTime.UtcNow;
                _db.InventoryMovements.Add(new InventoryMovement
                {
                    RestaurantId = request.RestaurantId,
                    InventoryItemId = item.InventoryItemId,
                    DeltaQuantity = request.CurrentQuantity,
                    QuantityAfter = request.CurrentQuantity,
                    Reason = InventoryMovementReasons.Opening,
                    UnitPrice = request.LastPurchasePrice,
                    Note = "موجودی اولیه",
                    CreatedAt = DateTime.UtcNow,
                    CreatedByOwnerId = ownerId
                });
                await _db.SaveChangesAsync(ct);
            }

            await _db.Entry(item).Reference(i => i.Category).LoadAsync(ct);
            await _db.Entry(item).Reference(i => i.BaseUnit).LoadAsync(ct);
            return ToItemDto(item);
        }

        public async Task<InventoryItemDto?> UpdateItemAsync(int restaurantId, int itemId, UpdateInventoryItemRequest request, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);
            var baseUnit = await _units.GetRequiredUnitAsync(request.BaseUnitId, ct);
            await ValidateCategoryAsync(restaurantId, request.CategoryId, ct);

            if (request.MinimumQuantity < 0)
                throw new InvalidOperationException("حداقل موجودی نمی‌تواند منفی باشد.");

            var item = await _db.InventoryItems
                .Include(i => i.Category)
                .Include(i => i.BaseUnit)
                .FirstOrDefaultAsync(i => i.RestaurantId == restaurantId && i.InventoryItemId == itemId && i.IsActive, ct);

            if (item == null)
                return null;

            if (item.BaseUnitId != baseUnit.UnitId)
            {
                if (item.CurrentQuantity != 0)
                    throw new InvalidOperationException("تغییر واحد پایه فقط وقتی موجودی صفر است مجاز است.");
            }

            item.Name = request.Name.Trim();
            item.BaseUnitId = baseUnit.UnitId;
            item.Unit = baseUnit.Code;
            item.CategoryId = request.CategoryId;
            item.MinimumQuantity = request.MinimumQuantity;
            item.LastPurchasePrice = request.LastPurchasePrice;
            item.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            item.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            item.BaseUnit = baseUnit;
            return ToItemDto(item);
        }

        public async Task<bool> DeactivateItemAsync(int restaurantId, int itemId, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);

            var item = await _db.InventoryItems
                .FirstOrDefaultAsync(i => i.RestaurantId == restaurantId && i.InventoryItemId == itemId && i.IsActive, ct);

            if (item == null)
                return false;

            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<InventoryItemDto?> AddStockAsync(int restaurantId, int itemId, AddStockRequest request, int? ownerId, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);

            if (request.Quantity <= 0)
                throw new InvalidOperationException("مقدار افزایش موجودی باید بیشتر از صفر باشد.");

            var item = await _db.InventoryItems
                .Include(i => i.Category)
                .Include(i => i.BaseUnit)
                .FirstOrDefaultAsync(i => i.RestaurantId == restaurantId && i.InventoryItemId == itemId && i.IsActive, ct);

            if (item == null)
                return null;

            if (item.BaseUnit == null)
                throw new InvalidOperationException("واحد پایه کالا تنظیم نشده است.");

            var entryUnitId = request.UnitId ?? item.BaseUnitId;
            var entryUnit = await _units.GetRequiredUnitAsync(entryUnitId, ct);
            var qtyInBase = _units.ToBase(request.Quantity, entryUnit, item.BaseUnit);

            item.CurrentQuantity += qtyInBase;
            if (request.UnitPrice.HasValue)
                item.LastPurchasePrice = request.UnitPrice;
            item.UpdatedAt = DateTime.UtcNow;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                RestaurantId = restaurantId,
                InventoryItemId = item.InventoryItemId,
                DeltaQuantity = qtyInBase,
                QuantityAfter = item.CurrentQuantity,
                Reason = InventoryMovementReasons.Purchase,
                UnitPrice = request.UnitPrice,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedByOwnerId = ownerId
            });

            await _db.SaveChangesAsync(ct);
            return ToItemDto(item);
        }

        public async Task<InventoryItemDto?> AdjustStockAsync(int restaurantId, int itemId, AdjustStockRequest request, int? ownerId, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);

            if (!InventoryMovementReasons.AdjustAllowed.Contains(request.Reason))
                throw new InvalidOperationException("دلیل تعدیل موجودی معتبر نیست.");

            if (!request.NewQuantity.HasValue && !request.DeltaQuantity.HasValue)
                throw new InvalidOperationException("مقدار جدید یا میزان تغییر را مشخص کنید.");

            var item = await _db.InventoryItems
                .Include(i => i.Category)
                .Include(i => i.BaseUnit)
                .FirstOrDefaultAsync(i => i.RestaurantId == restaurantId && i.InventoryItemId == itemId && i.IsActive, ct);

            if (item == null)
                return null;

            decimal newQty;
            if (request.NewQuantity.HasValue)
                newQty = request.NewQuantity.Value;
            else
                newQty = item.CurrentQuantity + request.DeltaQuantity!.Value;

            if (newQty < 0)
                throw new InvalidOperationException("موجودی نمی‌تواند منفی شود.");

            var delta = newQty - item.CurrentQuantity;
            if (delta == 0)
                return ToItemDto(item);

            item.CurrentQuantity = newQty;
            item.UpdatedAt = DateTime.UtcNow;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                RestaurantId = restaurantId,
                InventoryItemId = item.InventoryItemId,
                DeltaQuantity = delta,
                QuantityAfter = newQty,
                Reason = request.Reason,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedByOwnerId = ownerId
            });

            await _db.SaveChangesAsync(ct);
            return ToItemDto(item);
        }

        public async Task<IReadOnlyList<InventoryItemDto>> GetLowStockItemsAsync(int restaurantId, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);

            var items = await _db.InventoryItems.AsNoTracking()
                .Include(i => i.Category)
                .Include(i => i.BaseUnit)
                .Where(i => i.RestaurantId == restaurantId
                            && i.IsActive
                            && i.CurrentQuantity <= i.MinimumQuantity)
                .OrderBy(i => i.CurrentQuantity)
                .ThenBy(i => i.Name)
                .ToListAsync(ct);

            return items.Select(ToItemDto).ToList();
        }

        public async Task<InventorySummaryDto> GetSummaryAsync(int restaurantId, int lowStockTake = 4, CancellationToken ct = default)
        {
            if (lowStockTake < 0) lowStockTake = 0;

            InventorySettingsDto? settings;
            try
            {
                settings = await GetSettingsIfExistsAsync(restaurantId, ct);
            }
            catch (Exception)
            {
                return new InventorySummaryDto();
            }

            if (settings?.IsEnabled != true)
            {
                return new InventorySummaryDto
                {
                    IsEnabled = false,
                    ItemCount = 0,
                    LowStockCount = 0,
                    LowStockItems = Array.Empty<InventoryLowStockHintDto>()
                };
            }

            try
            {
                var activeItems = await _db.InventoryItems.AsNoTracking()
                    .Include(i => i.BaseUnit)
                    .Where(i => i.RestaurantId == restaurantId && i.IsActive)
                    .ToListAsync(ct);

                var low = activeItems
                    .Where(i => i.CurrentQuantity <= i.MinimumQuantity)
                    .OrderBy(i => i.CurrentQuantity)
                    .ThenBy(i => i.Name)
                    .ToList();

                var hints = low.Take(lowStockTake).Select(i =>
                {
                    var shortage = i.MinimumQuantity - i.CurrentQuantity;
                    if (shortage < 0) shortage = 0;
                    return new InventoryLowStockHintDto
                    {
                        InventoryItemId = i.InventoryItemId,
                        Name = i.Name,
                        Unit = i.BaseUnit?.Code ?? i.Unit ?? string.Empty,
                        UnitNameFa = i.BaseUnit?.NameFa,
                        CurrentQuantity = i.CurrentQuantity,
                        MinimumQuantity = i.MinimumQuantity,
                        Shortage = shortage
                    };
                }).ToList();

                return new InventorySummaryDto
                {
                    IsEnabled = true,
                    ItemCount = activeItems.Count,
                    LowStockCount = low.Count,
                    LowStockItems = hints
                };
            }
            catch (Exception)
            {
                return new InventorySummaryDto
                {
                    IsEnabled = true,
                    ItemCount = 0,
                    LowStockCount = 0,
                    LowStockItems = Array.Empty<InventoryLowStockHintDto>()
                };
            }
        }

        public async Task<IReadOnlyList<InventoryCategoryDto>> ListCategoriesAsync(int restaurantId, bool activeOnly = true, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);

            var query = _db.InventoryCategories.AsNoTracking()
                .Where(c => c.RestaurantId == restaurantId);

            if (activeOnly)
                query = query.Where(c => c.IsActive);

            var list = await query.OrderBy(c => c.Name).ToListAsync(ct);
            return list.Select(c => new InventoryCategoryDto
            {
                InventoryCategoryId = c.InventoryCategoryId,
                RestaurantId = c.RestaurantId,
                Name = c.Name,
                IsActive = c.IsActive
            }).ToList();
        }

        public async Task<InventoryCategoryDto> CreateCategoryAsync(CreateInventoryCategoryRequest request, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(request.RestaurantId, ct);

            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("نام دسته‌بندی الزامی است.");

            var exists = await _db.InventoryCategories.AnyAsync(
                c => c.RestaurantId == request.RestaurantId && c.IsActive && c.Name == name, ct);
            if (exists)
                throw new InvalidOperationException("این دسته‌بندی از قبل وجود دارد.");

            var category = new InventoryCategory
            {
                RestaurantId = request.RestaurantId,
                Name = name,
                IsActive = true
            };
            _db.InventoryCategories.Add(category);
            await _db.SaveChangesAsync(ct);

            return new InventoryCategoryDto
            {
                InventoryCategoryId = category.InventoryCategoryId,
                RestaurantId = category.RestaurantId,
                Name = category.Name,
                IsActive = category.IsActive
            };
        }

        public async Task<InventoryCategoryDto?> UpdateCategoryAsync(
            int restaurantId,
            int categoryId,
            UpdateInventoryCategoryRequest request,
            CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);

            var category = await _db.InventoryCategories
                .FirstOrDefaultAsync(c => c.InventoryCategoryId == categoryId && c.RestaurantId == restaurantId, ct);
            if (category == null || !category.IsActive)
                return null;

            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("نام دسته‌بندی الزامی است.");

            var exists = await _db.InventoryCategories.AnyAsync(
                c => c.RestaurantId == restaurantId
                     && c.IsActive
                     && c.Name == name
                     && c.InventoryCategoryId != categoryId, ct);
            if (exists)
                throw new InvalidOperationException("این دسته‌بندی از قبل وجود دارد.");

            category.Name = name;
            await _db.SaveChangesAsync(ct);

            return new InventoryCategoryDto
            {
                InventoryCategoryId = category.InventoryCategoryId,
                RestaurantId = category.RestaurantId,
                Name = category.Name,
                IsActive = category.IsActive
            };
        }

        public async Task<bool> DeactivateCategoryAsync(int restaurantId, int categoryId, CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);

            var category = await _db.InventoryCategories
                .FirstOrDefaultAsync(c => c.InventoryCategoryId == categoryId && c.RestaurantId == restaurantId, ct);
            if (category == null || !category.IsActive)
                return false;

            category.IsActive = false;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<IReadOnlyList<InventoryMovementDto>> ListMovementsAsync(
            int restaurantId,
            int? inventoryItemId = null,
            string? reason = null,
            DateTime? fromUtc = null,
            DateTime? toUtcExclusive = null,
            int skip = 0,
            int take = 50,
            CancellationToken ct = default)
        {
            await EnsureEnabledAsync(restaurantId, ct);

            if (take <= 0) take = 50;
            if (take > 100) take = 100;
            if (skip < 0) skip = 0;

            var query = _db.InventoryMovements.AsNoTracking()
                .Where(m => m.RestaurantId == restaurantId);

            if (inventoryItemId.HasValue && inventoryItemId.Value > 0)
                query = query.Where(m => m.InventoryItemId == inventoryItemId.Value);

            if (!string.IsNullOrWhiteSpace(reason))
            {
                var reasonNorm = InventoryMovementReasons.All
                    .FirstOrDefault(r => string.Equals(r, reason.Trim(), StringComparison.OrdinalIgnoreCase));
                if (reasonNorm == null)
                    throw new InvalidOperationException("دلیل حرکت معتبر نیست.");
                query = query.Where(m => m.Reason == reasonNorm);
            }

            if (fromUtc.HasValue)
                query = query.Where(m => m.CreatedAt >= fromUtc.Value);

            if (toUtcExclusive.HasValue)
                query = query.Where(m => m.CreatedAt < toUtcExclusive.Value);

            var rows = await query
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.MovementId)
                .Skip(skip)
                .Take(take)
                .Select(m => new
                {
                    m.MovementId,
                    m.InventoryItemId,
                    ItemName = m.Item != null ? m.Item.Name : "",
                    Unit = m.Item != null ? m.Item.Unit : null,
                    UnitNameFa = m.Item != null && m.Item.BaseUnit != null ? m.Item.BaseUnit.NameFa : null,
                    m.DeltaQuantity,
                    m.QuantityAfter,
                    m.Reason,
                    m.Note,
                    m.UnitPrice,
                    m.CreatedAt,
                    m.CreatedByOwnerId
                })
                .ToListAsync(ct);

            return rows.Select(m => new InventoryMovementDto
            {
                MovementId = m.MovementId,
                InventoryItemId = m.InventoryItemId,
                ItemName = m.ItemName ?? "",
                Unit = m.Unit,
                UnitNameFa = m.UnitNameFa,
                DeltaQuantity = m.DeltaQuantity,
                QuantityAfter = m.QuantityAfter,
                Reason = m.Reason,
                Note = m.Note,
                OrderId = TryParseRelatedOrderId(m.Note),
                UnitPrice = m.UnitPrice,
                CreatedAt = m.CreatedAt,
                CreatedByOwnerId = m.CreatedByOwnerId
            }).ToList();
        }

        /// <summary>
        /// Sale deduct notes are "Order:{id}"; cancel restore notes are "Cancel order {id}".
        /// </summary>
        private static int? TryParseRelatedOrderId(string? note)
        {
            if (string.IsNullOrWhiteSpace(note))
                return null;

            note = note.Trim();
            if (note.StartsWith("Order:", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(note.AsSpan("Order:".Length), out var id) && id > 0)
                    return id;
            }

            const string cancelPrefix = "Cancel order ";
            if (note.StartsWith(cancelPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(note.AsSpan(cancelPrefix.Length), out var id) && id > 0)
                    return id;
            }

            return null;
        }

        private async Task EnsureEnabledAsync(int restaurantId, CancellationToken ct)
        {
            if (!await IsEnabledAsync(restaurantId, ct))
                throw new InvalidOperationException("ماژول انبار برای این رستوران فعال نیست.");
        }

        private async Task<InventorySettings> GetOrCreateSettingsAsync(int restaurantId, CancellationToken ct)
        {
            var settings = await _db.InventorySettings
                .FirstOrDefaultAsync(s => s.RestaurantId == restaurantId, ct);

            if (settings != null)
                return settings;

            settings = new InventorySettings
            {
                RestaurantId = restaurantId,
                IsEnabled = false,
                AutoDeductStatusId = 4,
                UpdatedAt = DateTime.UtcNow
            };
            _db.InventorySettings.Add(settings);
            await _db.SaveChangesAsync(ct);
            return settings;
        }

        private async Task ValidateCategoryAsync(int restaurantId, int? categoryId, CancellationToken ct)
        {
            if (!categoryId.HasValue)
                return;

            var ok = await _db.InventoryCategories.AnyAsync(
                c => c.InventoryCategoryId == categoryId.Value
                     && c.RestaurantId == restaurantId
                     && c.IsActive, ct);

            if (!ok)
                throw new InvalidOperationException("دسته‌بندی انتخاب‌شده معتبر نیست.");
        }

        private static InventorySettingsDto ToSettingsDto(InventorySettings s) => new()
        {
            RestaurantId = s.RestaurantId,
            IsEnabled = s.IsEnabled,
            AutoDeductStatusId = s.AutoDeductStatusId <= 0 ? 4 : s.AutoDeductStatusId
        };

        private static InventoryItemDto ToItemDto(InventoryItem item) => new()
        {
            InventoryItemId = item.InventoryItemId,
            RestaurantId = item.RestaurantId,
            CategoryId = item.CategoryId,
            CategoryName = item.Category?.Name,
            Name = item.Name,
            BaseUnitId = item.BaseUnitId,
            Unit = item.BaseUnit?.Code ?? item.Unit ?? string.Empty,
            UnitNameFa = item.BaseUnit?.NameFa,
            CurrentQuantity = item.CurrentQuantity,
            MinimumQuantity = item.MinimumQuantity,
            LastPurchasePrice = item.LastPurchasePrice,
            Notes = item.Notes,
            IsActive = item.IsActive,
            IsLowStock = item.CurrentQuantity <= item.MinimumQuantity
        };
    }
}
