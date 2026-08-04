using Microsoft.EntityFrameworkCore;
using resturanyar.Helpers;
using resturanyar.Models;
using resturanyar.Models.Receipt;
using Resturanyar.Data;
using System.Text.Json;

namespace resturanyar.Services.Receipt
{
    public class ReceiptServiceResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public ReceiptDto? Receipt { get; set; }
        public int StatusCode { get; set; } = 200;
    }

    public class ReceiptPreviewDefaultsResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public ReceiptDto? Receipt { get; set; }
        public List<ReceiptChargeSelectionDto> AppliedCharges { get; set; } = new();
        public int StatusCode { get; set; } = 200;
    }

    public interface IReceiptService
    {
        Task<ReceiptServiceResult> GetStatusAsync(int orderId, int restaurantId);
        Task<ReceiptPreviewDefaultsResult> GetPreviewDefaultsAsync(int orderId, int restaurantId, int? userId);
        Task<ReceiptServiceResult> PreviewAsync(int orderId, int restaurantId, ReceiptPreviewRequest request);
        Task<ReceiptServiceResult> IssueAsync(int orderId, int restaurantId, ReceiptPreviewRequest request, int? userId, string channel, bool recordPrintHistory = true);
        Task<ReceiptServiceResult> ReissueAsync(int orderId, int restaurantId, ReceiptPreviewRequest request, int? userId, string channel, bool recordPrintHistory = false);
        Task<ReceiptServiceResult> TryAutoIssueOnSettlementAsync(int orderId, int restaurantId, int? userId, int previousStatusId, int newStatusId);
        Task<ReceiptServiceResult> GetReceiptDataAsync(int orderId, int restaurantId, string channel, int? userId, bool recordPrintHistory = true);
        Task AttachReceiptTotalsForOrderListAsync(IList<OrderDto> orders, int restaurantId, int? userId);
        Task<List<ChargeDefinitionDto>> GetChargeDefinitionsAsync(int restaurantId);
        Task<List<ChargeDefinitionDto>> EnsureChargeDefinitionsAsync(int restaurantId);
        Task<bool> SaveChargeDefinitionsAsync(int restaurantId, List<ChargeDefinitionDto> definitions);
        string RenderHtml(ReceiptDto receipt);
        bool IsSettlementStatus(int statusId);
        bool IsOrderEligibleForChargeDefaults(Restaurant restaurant, DateTime orderCreatedAt);
    }

    public class ReceiptService : IReceiptService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly AppDbContext _context;
        private readonly IReceiptCalculationEngine _engine;
        private readonly IReceiptRenderer _renderer;

        public ReceiptService(
            AppDbContext context,
            IReceiptCalculationEngine engine,
            IReceiptRenderer renderer)
        {
            _context = context;
            _engine = engine;
            _renderer = renderer;
        }

        public async Task<ReceiptServiceResult> GetStatusAsync(int orderId, int restaurantId)
        {
            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId);

            if (restaurant == null)
                return Fail("رستوران یافت نشد.", 404);

            var orderExists = await _context.Orders
                .AsNoTracking()
                .AnyAsync(o => o.OrderId == orderId && o.RestaurantId == restaurantId);

            if (!orderExists)
                return Fail("سفارش یافت نشد.", 404);

            var snapshot = await _context.OrderReceiptSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrderId == orderId);

            return new ReceiptServiceResult
            {
                Success = true,
                Receipt = new ReceiptDto
                {
                    OrderId = orderId,
                    IsIssued = snapshot != null,
                    IssuedAt = snapshot?.IssuedAt,
                    UsesCharges = restaurant.ReceiptChargesEnabled
                }
            };
        }

        public async Task<ReceiptPreviewDefaultsResult> GetPreviewDefaultsAsync(int orderId, int restaurantId, int? userId)
        {
            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.RestaurantId == restaurantId);
            if (order == null)
            {
                return new ReceiptPreviewDefaultsResult
                {
                    Success = false,
                    Message = "سفارش یافت نشد.",
                    StatusCode = 404
                };
            }

            var restaurant = await _context.Restaurants.AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId);
            if (restaurant == null)
            {
                return new ReceiptPreviewDefaultsResult
                {
                    Success = false,
                    Message = "رستوران یافت نشد.",
                    StatusCode = 404
                };
            }

            if (!restaurant.ReceiptChargesEnabled)
            {
                var legacy = await PreviewAsync(orderId, restaurantId, new ReceiptPreviewRequest { OrderType = order.OrderType });
                return new ReceiptPreviewDefaultsResult
                {
                    Success = legacy.Success,
                    Message = legacy.Message,
                    Receipt = legacy.Receipt,
                    AppliedCharges = new List<ReceiptChargeSelectionDto>(),
                    StatusCode = legacy.StatusCode
                };
            }

            var snapshot = await _context.OrderReceiptSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrderId == orderId);

            var receiptRes = snapshot != null
                ? await GetReceiptDataAsync(orderId, restaurantId, "Api", userId, recordPrintHistory: false)
                : await PreviewAsync(orderId, restaurantId, new ReceiptPreviewRequest { OrderType = order.OrderType });

            if (!receiptRes.Success || receiptRes.Receipt == null)
            {
                return new ReceiptPreviewDefaultsResult
                {
                    Success = receiptRes.Success,
                    Message = receiptRes.Message,
                    StatusCode = receiptRes.StatusCode
                };
            }

            var definitions = await _context.RestaurantChargeDefinitions.AsNoTracking()
                .Where(d => d.RestaurantId == restaurantId)
                .OrderBy(d => d.DisplayOrder)
                .ToListAsync();

            var flag = OrderTypeToFlag(order.OrderType);
            var applicableDefs = definitions
                .Where(d => (d.AppliesToOrderTypes & flag) != 0)
                .ToList();

            var eligibleForDefaults = IsOrderEligibleForChargeDefaults(restaurant, order.CreatedAt);
            var appliedCharges = new List<ReceiptChargeSelectionDto>();

            foreach (var def in applicableDefs)
            {
                bool isEnabled;
                decimal value;

                var line = receiptRes.Receipt.ChargeLines.FirstOrDefault(cl =>
                    (cl.DefinitionId.HasValue && cl.DefinitionId.Value == def.Id) ||
                    (!string.IsNullOrWhiteSpace(cl.Code) && cl.Code.Equals(def.Code, StringComparison.OrdinalIgnoreCase)));

                if (snapshot != null && receiptRes.Receipt.UsesCharges)
                {
                    isEnabled = line != null;
                    value = isEnabled
                        ? (line?.Value ?? NormalizeChargeValue(def.CalculationType, def.Value))
                        : NormalizeChargeValue(def.CalculationType, def.Value);
                }
                else
                {
                    isEnabled = eligibleForDefaults && def.IsEnabled;
                    value = NormalizeChargeValue(def.CalculationType, def.Value);
                }

                appliedCharges.Add(new ReceiptChargeSelectionDto
                {
                    DefinitionId = def.Id,
                    Code = def.Code,
                    IsEnabled = isEnabled,
                    Value = value
                });
            }

            return new ReceiptPreviewDefaultsResult
            {
                Success = true,
                Receipt = receiptRes.Receipt,
                AppliedCharges = appliedCharges,
                StatusCode = receiptRes.StatusCode
            };
        }

        public async Task<ReceiptServiceResult> PreviewAsync(int orderId, int restaurantId, ReceiptPreviewRequest request)
        {
            var load = await LoadOrderContext(orderId, restaurantId);
            if (!load.Success)
                return ToFailResult(load);

            if (!load.Restaurant!.ReceiptChargesEnabled)
                return Success(BuildLegacyReceipt(load.Order!, load.Restaurant, load.StatusName!));

            // Always calculate live so staff can preview/edit charges even after a snapshot exists.
            var receipt = await BuildCalculatedReceiptAsync(load.Order!, load.Restaurant, load.StatusName!, request);
            return Success(receipt);
        }

        public async Task<ReceiptServiceResult> IssueAsync(
            int orderId,
            int restaurantId,
            ReceiptPreviewRequest request,
            int? userId,
            string channel,
            bool recordPrintHistory = true)
        {
            return await PersistIssuedReceiptAsync(
                orderId,
                restaurantId,
                request,
                userId,
                channel,
                replaceExisting: false,
                recordPrintHistory);
        }

        public async Task<ReceiptServiceResult> ReissueAsync(
            int orderId,
            int restaurantId,
            ReceiptPreviewRequest request,
            int? userId,
            string channel,
            bool recordPrintHistory = false)
        {
            return await PersistIssuedReceiptAsync(
                orderId,
                restaurantId,
                request,
                userId,
                channel,
                replaceExisting: true,
                recordPrintHistory);
        }

        public bool IsSettlementStatus(int statusId) => statusId is 8 or 11;

        public bool IsOrderEligibleForChargeDefaults(Restaurant restaurant, DateTime orderCreatedAt)
        {
            if (!restaurant.ReceiptChargesEnabled)
                return false;

            if (!restaurant.ReceiptChargesEnabledAt.HasValue)
                return false;

            return orderCreatedAt >= restaurant.ReceiptChargesEnabledAt.Value;
        }

        public async Task<ReceiptServiceResult> TryAutoIssueOnSettlementAsync(
            int orderId,
            int restaurantId,
            int? userId,
            int previousStatusId,
            int newStatusId)
        {
            if (!IsSettlementStatus(newStatusId))
                return Success(new ReceiptDto { OrderId = orderId, IsIssued = false });

            if (previousStatusId == newStatusId)
                return Success(new ReceiptDto { OrderId = orderId, IsIssued = false });

            var status = await GetStatusAsync(orderId, restaurantId);
            if (!status.Success)
                return status;

            if (status.Receipt?.UsesCharges != true)
                return Success(status.Receipt!);

            if (status.Receipt.IsIssued)
            {
                var snapshot = await _context.OrderReceiptSnapshots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.OrderId == orderId);

                status.Receipt.GrandTotal = snapshot?.GrandTotal ?? 0;
                status.Receipt.IssuedAt = snapshot?.IssuedAt ?? status.Receipt.IssuedAt;
                status.Receipt.IsIssued = true;
                return Success(status.Receipt);
            }

            var load = await LoadOrderContext(orderId, restaurantId);
            if (!load.Success)
                return ToFailResult(load);

            var request = IsOrderEligibleForChargeDefaults(load.Restaurant!, load.Order!.CreatedAt)
                ? await BuildDefaultPreviewRequestAsync(load.Order.OrderType, restaurantId)
                : await BuildDisabledChargesPreviewRequestAsync(load.Order.OrderType, restaurantId);
            return await IssueAsync(orderId, restaurantId, request, userId, "AutoSettle", recordPrintHistory: false);
        }

        private async Task<ReceiptPreviewRequest> BuildDefaultPreviewRequestAsync(OrderTypeKind orderType, int restaurantId)
            => await BuildPreviewRequestFromDefinitionsAsync(orderType, restaurantId, useSavedEnabledState: true);

        private async Task<ReceiptPreviewRequest> BuildDisabledChargesPreviewRequestAsync(OrderTypeKind orderType, int restaurantId)
            => await BuildPreviewRequestFromDefinitionsAsync(orderType, restaurantId, useSavedEnabledState: false);

        private async Task<ReceiptPreviewRequest> BuildPreviewRequestFromDefinitionsAsync(
            OrderTypeKind orderType,
            int restaurantId,
            bool useSavedEnabledState)
        {
            var definitions = await _context.RestaurantChargeDefinitions
                .AsNoTracking()
                .Where(d => d.RestaurantId == restaurantId)
                .OrderBy(d => d.DisplayOrder)
                .ToListAsync();

            var flag = OrderTypeToFlag(orderType);
            var charges = definitions
                .Where(d => (d.AppliesToOrderTypes & flag) != 0)
                .Select(d => new ReceiptChargeSelectionDto
                {
                    DefinitionId = d.Id,
                    Code = d.Code,
                    IsEnabled = useSavedEnabledState && d.IsEnabled,
                    Value = NormalizeChargeValue(d.CalculationType, d.Value)
                })
                .ToList();

            return new ReceiptPreviewRequest
            {
                OrderType = orderType,
                Charges = charges
            };
        }

        private async Task<ReceiptServiceResult> PersistIssuedReceiptAsync(
            int orderId,
            int restaurantId,
            ReceiptPreviewRequest request,
            int? userId,
            string channel,
            bool replaceExisting,
            bool recordPrintHistory)
        {
            var load = await LoadOrderContext(orderId, restaurantId);
            if (!load.Success)
                return ToFailResult(load);

            if (!load.Restaurant!.ReceiptChargesEnabled)
                return Fail("قابلیت فاکتور با کارمزد برای این رستوران فعال نیست.", 400);

            var existing = await _context.OrderReceiptSnapshots
                .FirstOrDefaultAsync(s => s.OrderId == orderId);

            if (existing != null && !replaceExisting)
                return Fail("فاکتور این سفارش قبلاً صادر شده است. برای چاپ مجدد از همان فاکتور استفاده کنید.", 409);

            if (existing == null && replaceExisting)
                return Fail("فاکتور این سفارش هنوز صادر نشده است.", 404);

            request ??= new ReceiptPreviewRequest();
            var receipt = await BuildCalculatedReceiptAsync(load.Order!, load.Restaurant, load.StatusName!, request);
            receipt.IsIssued = true;
            receipt.IssuedAt = DateTime.UtcNow;

            if (existing == null)
            {
                existing = new OrderReceiptSnapshot
                {
                    OrderId = orderId,
                    RestaurantId = restaurantId
                };
                _context.OrderReceiptSnapshots.Add(existing);
            }

            existing.OrderType = receipt.OrderType;
            existing.ItemsSubtotal = receipt.ItemsSubtotal;
            existing.GrandTotal = receipt.GrandTotal;
            existing.ChargeLinesJson = JsonSerializer.Serialize(receipt.ChargeLines, JsonOptions);
            existing.ReceiptPayloadJson = JsonSerializer.Serialize(receipt, JsonOptions);
            existing.OrderItemsVersion = load.Order!.UpdatedAt;
            existing.IssuedAt = DateTime.UtcNow;
            existing.IssuedByUserId = userId;

            load.Order!.OrderType = receipt.OrderType;
            await _context.SaveChangesAsync();

            if (recordPrintHistory)
                await AddPrintHistory(orderId, existing.Id, userId, channel, receipt);

            return Success(receipt);
        }

        public async Task<ReceiptServiceResult> GetReceiptDataAsync(
            int orderId,
            int restaurantId,
            string channel,
            int? userId,
            bool recordPrintHistory = true)
        {
            var load = await LoadOrderContext(orderId, restaurantId);
            if (!load.Success)
                return ToFailResult(load);

            ReceiptDto receipt;

            if (!load.Restaurant!.ReceiptChargesEnabled)
            {
                receipt = BuildLegacyReceipt(load.Order!, load.Restaurant, load.StatusName!);
            }
            else
            {
                var snapshot = await _context.OrderReceiptSnapshots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.OrderId == orderId);

                if (snapshot == null)
                    return Fail("فاکتور این سفارش هنوز صادر نشده است.", 404);

                receipt = DeserializeReceipt(snapshot.ReceiptPayloadJson)
                    ?? BuildLegacyReceipt(load.Order!, load.Restaurant, load.StatusName!);

                EnrichOriginalUnitPricesFromOrder(receipt, load.Order!);

                if (recordPrintHistory)
                    await AddPrintHistory(orderId, snapshot.Id, userId, channel, receipt);
            }

            return Success(receipt);
        }

        public async Task<List<ChargeDefinitionDto>> GetChargeDefinitionsAsync(int restaurantId)
        {
            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId);

            if (restaurant == null || !restaurant.ReceiptChargesEnabled)
                return new List<ChargeDefinitionDto>();

            var defs = await _context.RestaurantChargeDefinitions
                .AsNoTracking()
                .Where(d => d.RestaurantId == restaurantId)
                .OrderBy(d => d.DisplayOrder)
                .ToListAsync();

            return defs.Select(MapDefinition).ToList();
        }

        public async Task<List<ChargeDefinitionDto>> EnsureChargeDefinitionsAsync(int restaurantId)
        {
            var defs = await GetChargeDefinitionsAsync(restaurantId);
            if (defs.Count > 0)
                return defs;

            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId);

            if (restaurant == null || !restaurant.ReceiptChargesEnabled)
                return defs;

            await SaveChargeDefinitionsAsync(restaurantId, CreateDefaultChargeTemplates());
            return await GetChargeDefinitionsAsync(restaurantId);
        }

        private static List<ChargeDefinitionDto> CreateDefaultChargeTemplates() => new()
        {
            new ChargeDefinitionDto
            {
                Code = "service",
                Title = "حق سرویس",
                ChargeCategory = ChargeCategory.Fee,
                CalculationType = ChargeCalculationType.Percentage,
                Value = 10,
                IsEnabled = false,
                IsTaxable = true,
                PercentageBase = PercentageBaseKind.ItemsNet,
                DisplayOrder = 10,
                AppliesToOrderTypes = OrderTypeFlags.All
            },
            new ChargeDefinitionDto
            {
                Code = "vat",
                Title = "مالیات بر ارزش افزوده",
                ChargeCategory = ChargeCategory.Tax,
                CalculationType = ChargeCalculationType.Percentage,
                Value = 9,
                IsEnabled = false,
                IsTaxable = false,
                PercentageBase = PercentageBaseKind.ItemsNet,
                DisplayOrder = 20,
                AppliesToOrderTypes = OrderTypeFlags.All
            },
            new ChargeDefinitionDto
            {
                Code = "packaging",
                Title = "هزینه بسته‌بندی",
                ChargeCategory = ChargeCategory.Fee,
                CalculationType = ChargeCalculationType.Fixed,
                Value = 20000,
                IsEnabled = false,
                IsTaxable = false,
                PercentageBase = PercentageBaseKind.ItemsNet,
                DisplayOrder = 30,
                AppliesToOrderTypes = OrderTypeFlags.Takeaway | OrderTypeFlags.Delivery
            },
            new ChargeDefinitionDto
            {
                Code = "delivery",
                Title = "هزینه ارسال",
                ChargeCategory = ChargeCategory.Fee,
                CalculationType = ChargeCalculationType.Fixed,
                Value = 30000,
                IsEnabled = false,
                IsTaxable = false,
                PercentageBase = PercentageBaseKind.ItemsNet,
                DisplayOrder = 40,
                AppliesToOrderTypes = OrderTypeFlags.Delivery
            }
        };

        public async Task<bool> SaveChargeDefinitionsAsync(int restaurantId, List<ChargeDefinitionDto> definitions)
        {
            var restaurant = await _context.Restaurants
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId);

            if (restaurant == null || !restaurant.ReceiptChargesEnabled)
                return false;

            if (definitions == null || definitions.Count == 0)
                return false;

            var existing = await _context.RestaurantChargeDefinitions
                .Where(d => d.RestaurantId == restaurantId)
                .ToListAsync();

            foreach (var dto in definitions)
            {
                if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Title))
                    continue;

                var entity = dto.Id > 0
                    ? existing.FirstOrDefault(e => e.Id == dto.Id)
                    : existing.FirstOrDefault(e => e.Code == dto.Code.Trim());

                if (entity == null)
                {
                    entity = new RestaurantChargeDefinition
                    {
                        RestaurantId = restaurantId,
                        Code = dto.Code.Trim(),
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.RestaurantChargeDefinitions.Add(entity);
                }

                entity.Title = dto.Title.Trim();
                entity.ChargeCategory = dto.ChargeCategory;
                entity.CalculationType = dto.CalculationType;
                entity.Value = NormalizeChargeValue(dto.CalculationType, dto.Value);
                entity.IsEnabled = dto.IsEnabled;
                entity.IsTaxable = dto.IsTaxable;
                entity.PercentageBase = dto.PercentageBase;
                entity.DisplayOrder = dto.DisplayOrder;
                entity.AppliesToOrderTypes = dto.AppliesToOrderTypes;
                entity.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public string RenderHtml(ReceiptDto receipt) => _renderer.RenderHtml(receipt);

        private async Task<OrderLoadResult> LoadOrderContext(int orderId, int restaurantId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.RestaurantId == restaurantId);

            if (order == null)
                return OrderLoadResult.Fail("سفارش یافت نشد.", 404);

            var restaurant = await _context.Restaurants
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.restaurant_id == restaurantId);

            if (restaurant == null)
                return OrderLoadResult.Fail("رستوران یافت نشد.", 404);

            var statusName = await _context.OrderStatus
                .AsNoTracking()
                .Where(s => s.OrderStatusId == order.StatusId)
                .Select(s => s.StatusName)
                .FirstOrDefaultAsync() ?? order.StatusId.ToString();

            return OrderLoadResult.Ok(order, restaurant, statusName);
        }

        private async Task<ReceiptPreviewRequest> NormalizePreviewRequestAsync(
            Order order,
            Restaurant restaurant,
            ReceiptPreviewRequest? request)
        {
            request ??= new ReceiptPreviewRequest();

            // Passive estimate/issue path: apply saved defaults only for orders created after charges were enabled.
            if (request.Charges == null || request.Charges.Count == 0)
            {
                if (!IsOrderEligibleForChargeDefaults(restaurant, order.CreatedAt))
                    return await BuildDisabledChargesPreviewRequestAsync(order.OrderType, order.RestaurantId);

                return await BuildDefaultPreviewRequestAsync(order.OrderType, order.RestaurantId);
            }

            // Explicit selections (receipt modal): honor caller order type when provided.
            if (request.OrderType == default)
                request.OrderType = order.OrderType;

            return request;
        }

        private async Task<ReceiptDto> BuildCalculatedReceiptAsync(
            Order order,
            Restaurant restaurant,
            string statusName,
            ReceiptPreviewRequest request)
        {
            request = await NormalizePreviewRequestAsync(order, restaurant, request);
            var orderType = request.OrderType;
            var items = order.OrderItems?.Select(MapItem).ToList() ?? new List<ReceiptItemDto>();
            var subtotal = items.Sum(i => i.LineTotal);

            var definitions = await _context.RestaurantChargeDefinitions
                .AsNoTracking()
                .Where(d => d.RestaurantId == order.RestaurantId)
                .OrderBy(d => d.DisplayOrder)
                .ToListAsync();

            var chargeInputs = BuildChargeInputs(definitions, orderType, request.Charges);
            var calculation = _engine.Calculate(subtotal, chargeInputs);

            return new ReceiptDto
            {
                OrderId = order.OrderId,
                RestaurantId = order.RestaurantId,
                RestaurantName = restaurant.name,
                OrderNumber = order.OrderId.ToString(),
                TableNumber = order.TableNumber,
                OrderStatus = statusName,
                OrderType = orderType,
                OrderTypeLabel = GetOrderTypeLabel(orderType),
                CreatedAt = order.CreatedAtShamsi ?? Utility.DateHelper.ToShamsi(order.CreatedAt),
                UpdatedAt = order.UpdatedAtShamsi ?? Utility.DateHelper.ToShamsi(order.UpdatedAt),
                Description = order.Description,
                CustomerName = order.Customer?.FullName,
                CustomerMobile = order.Customer?.Mobile,
                Items = items,
                ChargeLines = calculation.ChargeLines,
                ItemsSubtotal = calculation.ItemsSubtotal,
                DiscountTotal = calculation.DiscountTotal,
                FeesTotal = calculation.FeesTotal,
                TaxTotal = calculation.TaxTotal,
                GrandTotal = calculation.GrandTotal,
                UsesCharges = true,
                IsIssued = false
            };
        }

        private ReceiptDto BuildLegacyReceipt(Order order, Restaurant restaurant, string statusName)
        {
            var items = order.OrderItems?.Select(MapItem).ToList() ?? new List<ReceiptItemDto>();
            var subtotal = items.Sum(i => i.LineTotal);

            return new ReceiptDto
            {
                OrderId = order.OrderId,
                RestaurantId = order.RestaurantId,
                RestaurantName = restaurant.name,
                OrderNumber = order.OrderId.ToString(),
                TableNumber = order.TableNumber,
                OrderStatus = statusName,
                OrderType = order.OrderType,
                OrderTypeLabel = GetOrderTypeLabel(order.OrderType),
                CreatedAt = order.CreatedAtShamsi ?? Utility.DateHelper.ToShamsi(order.CreatedAt),
                UpdatedAt = order.UpdatedAtShamsi ?? Utility.DateHelper.ToShamsi(order.UpdatedAt),
                Description = order.Description,
                CustomerName = order.Customer?.FullName,
                CustomerMobile = order.Customer?.Mobile,
                Items = items,
                ItemsSubtotal = subtotal,
                GrandTotal = subtotal,
                UsesCharges = false,
                IsIssued = false
            };
        }

        private static List<ChargeCalculationInput> BuildChargeInputs(
            List<RestaurantChargeDefinition> definitions,
            OrderTypeKind orderType,
            List<ReceiptChargeSelectionDto> selections)
        {
            var flag = OrderTypeToFlag(orderType);
            var applicable = definitions
                .Where(d => (d.AppliesToOrderTypes & flag) != 0)
                .ToList();

            var result = new List<ChargeCalculationInput>();

            foreach (var def in applicable)
            {
                var selection = selections?.FirstOrDefault(s =>
                    s.DefinitionId == def.Id ||
                    (s.Code != null && s.Code.Equals(def.Code, StringComparison.OrdinalIgnoreCase)));

                var isEnabled = selection != null ? selection.IsEnabled : def.IsEnabled;
                var value = NormalizeChargeValue(def.CalculationType, selection?.Value ?? def.Value);

                result.Add(new ChargeCalculationInput
                {
                    DefinitionId = def.Id,
                    Code = def.Code,
                    Title = def.Title,
                    Category = def.ChargeCategory,
                    CalculationType = def.CalculationType,
                    Value = value,
                    IsEnabled = isEnabled,
                    IsTaxable = def.IsTaxable,
                    PercentageBase = def.PercentageBase,
                    DisplayOrder = def.DisplayOrder
                });
            }

            return result;
        }

        private async Task AddPrintHistory(int orderId, int snapshotId, int? userId, string channel, ReceiptDto? receipt = null)
        {
            string? payloadJson = null;
            decimal? itemsSubtotal = null;
            decimal? grandTotal = null;

            if (receipt != null)
            {
                itemsSubtotal = receipt.ItemsSubtotal;
                grandTotal = receipt.GrandTotal;
                try
                {
                    payloadJson = JsonSerializer.Serialize(receipt, JsonOptions);
                }
                catch
                {
                    payloadJson = null;
                }
            }

            _context.ReceiptPrintHistories.Add(new ReceiptPrintHistory
            {
                OrderId = orderId,
                OrderReceiptSnapshotId = snapshotId,
                PrintedAt = DateTime.UtcNow,
                PrintedByUserId = userId,
                Channel = channel,
                ItemsSubtotal = itemsSubtotal,
                GrandTotal = grandTotal,
                ReceiptPayloadJson = payloadJson
            });
            await _context.SaveChangesAsync();
        }

        private static ReceiptItemDto MapItem(OrderItem item)
        {
            var original = item.UnitPrice;
            var unit = FoodItemPricing.GetEffectiveSellingPrice(item.UnitPrice, item.UnitPriceWithDiscount);
            return new ReceiptItemDto
            {
                Name = item.FoodName ?? "-",
                Quantity = item.Quantity,
                OriginalUnitPrice = original,
                UnitPrice = unit,
                LineTotal = unit * item.Quantity
            };
        }

        /// <summary>
        /// Older issued snapshots only store effective unit price. Backfill list price from live order items
        /// so HTML/Android print can show original + discounted unit price.
        /// </summary>
        private static void EnrichOriginalUnitPricesFromOrder(ReceiptDto receipt, Order order)
        {
            if (receipt?.Items == null || order?.OrderItems == null || order.OrderItems.Count == 0)
                return;

            var orderItems = order.OrderItems.ToList();
            for (var i = 0; i < receipt.Items.Count; i++)
            {
                var receiptItem = receipt.Items[i];
                if (receiptItem.OriginalUnitPrice > 0 && receiptItem.OriginalUnitPrice > receiptItem.UnitPrice)
                    continue;

                OrderItem? match = null;
                if (i < orderItems.Count
                    && string.Equals(receiptItem.Name?.Trim(), orderItems[i].FoodName?.Trim(), StringComparison.OrdinalIgnoreCase)
                    && receiptItem.Quantity == orderItems[i].Quantity)
                {
                    match = orderItems[i];
                }
                else
                {
                    match = orderItems.FirstOrDefault(oi =>
                        string.Equals(receiptItem.Name?.Trim(), oi.FoodName?.Trim(), StringComparison.OrdinalIgnoreCase)
                        && oi.Quantity == receiptItem.Quantity);
                }

                match ??= i < orderItems.Count ? orderItems[i] : null;
                if (match == null)
                    continue;

                if (match.UnitPrice > 0)
                    receiptItem.OriginalUnitPrice = match.UnitPrice;
            }
        }

        private static ChargeDefinitionDto MapDefinition(RestaurantChargeDefinition d) => new()
        {
            Id = d.Id,
            Code = d.Code,
            Title = d.Title,
            ChargeCategory = d.ChargeCategory,
            CalculationType = d.CalculationType,
            Value = d.Value,
            IsEnabled = d.IsEnabled,
            IsTaxable = d.IsTaxable,
            PercentageBase = d.PercentageBase,
            DisplayOrder = d.DisplayOrder,
            AppliesToOrderTypes = d.AppliesToOrderTypes
        };

        private static ReceiptDto? DeserializeReceipt(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<ReceiptDto>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static decimal NormalizeChargeValue(ChargeCalculationType calculationType, decimal? value)
        {
            var amount = value ?? 0m;
            if (amount < 0) amount = 0;
            if (calculationType == ChargeCalculationType.Percentage && amount > 100m)
                amount = 100m;
            return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        }

        private static OrderTypeFlags OrderTypeToFlag(OrderTypeKind orderType)
        {
            return orderType switch
            {
                OrderTypeKind.Takeaway => OrderTypeFlags.Takeaway,
                OrderTypeKind.Delivery => OrderTypeFlags.Delivery,
                _ => OrderTypeFlags.DineIn
            };
        }

        private static string GetOrderTypeLabel(OrderTypeKind orderType)
        {
            return orderType switch
            {
                OrderTypeKind.Takeaway => "بیرون‌بر",
                OrderTypeKind.Delivery => "ارسال",
                _ => "سالن"
            };
        }

        private static ReceiptServiceResult Success(ReceiptDto receipt) => new()
        {
            Success = true,
            Receipt = receipt
        };

        private static ReceiptServiceResult Fail(string message, int statusCode) => new()
        {
            Success = false,
            Message = message,
            StatusCode = statusCode
        };

        private static ReceiptServiceResult ToFailResult(OrderLoadResult load) => new()
        {
            Success = false,
            Message = load.Message,
            StatusCode = load.StatusCode
        };

        private sealed class OrderLoadResult
        {
            public bool Success { get; init; }
            public string? Message { get; init; }
            public int StatusCode { get; init; }
            public Order? Order { get; init; }
            public Restaurant? Restaurant { get; init; }
            public string? StatusName { get; init; }

            public static OrderLoadResult Ok(Order order, Restaurant restaurant, string statusName) => new()
            {
                Success = true,
                Order = order,
                Restaurant = restaurant,
                StatusName = statusName
            };

            public static OrderLoadResult Fail(string message, int statusCode) => new()
            {
                Success = false,
                Message = message,
                StatusCode = statusCode
            };
        }

        public async Task AttachReceiptTotalsForOrderListAsync(IList<OrderDto> orders, int restaurantId, int? userId)
        {
            if (orders == null || orders.Count == 0)
                return;

            var orderIds = orders.Select(o => o.OrderId).ToList();
            var snapshots = await _context.OrderReceiptSnapshots
                .AsNoTracking()
                .Where(s => orderIds.Contains(s.OrderId))
                .ToDictionaryAsync(s => s.OrderId);

            foreach (var order in orders)
            {
                if (snapshots.TryGetValue(order.OrderId, out var snapshot))
                {
                    // Issued snapshot: show stored receipt totals (no re-calc).
                    var receiptRes = await GetReceiptDataAsync(
                        order.OrderId,
                        restaurantId,
                        channel: "Api",
                        userId: userId,
                        recordPrintHistory: false);

                    if (receiptRes.Success && receiptRes.Receipt != null)
                    {
                        order.ReceiptGrandTotal = receiptRes.Receipt.GrandTotal;
                        order.ReceiptIssuedAt = receiptRes.Receipt.IssuedAt;
                        order.ReceiptTotals = new ReceiptTotalsDto
                        {
                            ItemsSubtotal = receiptRes.Receipt.ItemsSubtotal,
                            DiscountTotal = receiptRes.Receipt.DiscountTotal,
                            FeesTotal = receiptRes.Receipt.FeesTotal,
                            TaxTotal = receiptRes.Receipt.TaxTotal,
                            GrandTotal = receiptRes.Receipt.GrandTotal,
                            IsIssued = receiptRes.Receipt.IsIssued,
                            UsesCharges = receiptRes.Receipt.UsesCharges,
                            ChargeLines = receiptRes.Receipt.ChargeLines
                        };
                    }
                    else
                    {
                        // Safe fallback (amount-less breakdown might be missing).
                        order.ReceiptGrandTotal = snapshot.GrandTotal;
                        order.ReceiptIssuedAt = snapshot.IssuedAt;
                    }
                }
                else
                {
                    // Pre-receipt preview: compute server-side using restaurant defaults for the order type.
                    var previewRes = await PreviewAsync(
                        order.OrderId,
                        restaurantId,
                        new ReceiptPreviewRequest
                        {
                            OrderType = (OrderTypeKind)order.OrderType
                        });

                    if (previewRes.Success && previewRes.Receipt != null)
                    {
                        order.EstimatedReceiptGrandTotal = previewRes.Receipt.GrandTotal;
                        order.ReceiptTotals = new ReceiptTotalsDto
                        {
                            ItemsSubtotal = previewRes.Receipt.ItemsSubtotal,
                            DiscountTotal = previewRes.Receipt.DiscountTotal,
                            FeesTotal = previewRes.Receipt.FeesTotal,
                            TaxTotal = previewRes.Receipt.TaxTotal,
                            GrandTotal = previewRes.Receipt.GrandTotal,
                            IsIssued = false,
                            UsesCharges = previewRes.Receipt.UsesCharges,
                            ChargeLines = previewRes.Receipt.ChargeLines
                        };
                    }
                }
            }
        }
    }
}
