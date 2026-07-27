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

    public interface IReceiptService
    {
        Task<ReceiptServiceResult> GetStatusAsync(int orderId, int restaurantId);
        Task<ReceiptServiceResult> PreviewAsync(int orderId, int restaurantId, ReceiptPreviewRequest request);
        Task<ReceiptServiceResult> IssueAsync(int orderId, int restaurantId, ReceiptPreviewRequest request, int? userId, string channel);
        Task<ReceiptServiceResult> GetReceiptDataAsync(int orderId, int restaurantId, string channel, int? userId);
        Task<List<ChargeDefinitionDto>> GetChargeDefinitionsAsync(int restaurantId);
        Task<List<ChargeDefinitionDto>> EnsureChargeDefinitionsAsync(int restaurantId);
        Task<bool> SaveChargeDefinitionsAsync(int restaurantId, List<ChargeDefinitionDto> definitions);
        string RenderHtml(ReceiptDto receipt);
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

        public async Task<ReceiptServiceResult> PreviewAsync(int orderId, int restaurantId, ReceiptPreviewRequest request)
        {
            var load = await LoadOrderContext(orderId, restaurantId);
            if (!load.Success)
                return ToFailResult(load);

            if (!load.Restaurant!.ReceiptChargesEnabled)
                return Success(BuildLegacyReceipt(load.Order!, load.Restaurant, load.StatusName!));

            var snapshot = await _context.OrderReceiptSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrderId == orderId);

            if (snapshot != null)
            {
                var issued = DeserializeReceipt(snapshot.ReceiptPayloadJson);
                if (issued != null)
                    return Success(issued);
            }

            var receipt = await BuildCalculatedReceiptAsync(load.Order!, load.Restaurant, load.StatusName!, request);
            return Success(receipt);
        }

        public async Task<ReceiptServiceResult> IssueAsync(
            int orderId,
            int restaurantId,
            ReceiptPreviewRequest request,
            int? userId,
            string channel)
        {
            var load = await LoadOrderContext(orderId, restaurantId);
            if (!load.Success)
                return ToFailResult(load);

            if (!load.Restaurant!.ReceiptChargesEnabled)
                return Fail("قابلیت فاکتور با کارمزد برای این رستوران فعال نیست.", 400);

            var existing = await _context.OrderReceiptSnapshots
                .FirstOrDefaultAsync(s => s.OrderId == orderId);

            if (existing != null)
                return Fail("فاکتور این سفارش قبلاً صادر شده است. برای چاپ مجدد از همان فاکتور استفاده کنید.", 409);

            var receipt = await BuildCalculatedReceiptAsync(load.Order!, load.Restaurant, load.StatusName!, request);
            receipt.IsIssued = true;
            receipt.IssuedAt = DateTime.UtcNow;

            var snapshot = new OrderReceiptSnapshot
            {
                OrderId = orderId,
                RestaurantId = restaurantId,
                OrderType = receipt.OrderType,
                ItemsSubtotal = receipt.ItemsSubtotal,
                GrandTotal = receipt.GrandTotal,
                ChargeLinesJson = JsonSerializer.Serialize(receipt.ChargeLines, JsonOptions),
                ReceiptPayloadJson = JsonSerializer.Serialize(receipt, JsonOptions),
                OrderItemsVersion = load.Order!.UpdatedAt,
                IssuedAt = DateTime.UtcNow,
                IssuedByUserId = userId
            };

            _context.OrderReceiptSnapshots.Add(snapshot);
            load.Order!.OrderType = receipt.OrderType;
            await _context.SaveChangesAsync();

            await AddPrintHistory(orderId, snapshot.Id, userId, channel);
            return Success(receipt);
        }

        public async Task<ReceiptServiceResult> GetReceiptDataAsync(int orderId, int restaurantId, string channel, int? userId)
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

                await AddPrintHistory(orderId, snapshot.Id, userId, channel);
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
                entity.Value = dto.Value;
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

        private async Task<ReceiptDto> BuildCalculatedReceiptAsync(
            Order order,
            Restaurant restaurant,
            string statusName,
            ReceiptPreviewRequest request)
        {
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
                var value = selection?.Value ?? def.Value;

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

        private async Task AddPrintHistory(int orderId, int snapshotId, int? userId, string channel)
        {
            _context.ReceiptPrintHistories.Add(new ReceiptPrintHistory
            {
                OrderId = orderId,
                OrderReceiptSnapshotId = snapshotId,
                PrintedAt = DateTime.UtcNow,
                PrintedByUserId = userId,
                Channel = channel
            });
            await _context.SaveChangesAsync();
        }

        private static ReceiptItemDto MapItem(OrderItem item)
        {
            var unit = FoodItemPricing.GetEffectiveSellingPrice(item.UnitPrice, item.UnitPriceWithDiscount);
            return new ReceiptItemDto
            {
                Name = item.FoodName ?? "-",
                Quantity = item.Quantity,
                UnitPrice = unit,
                LineTotal = unit * item.Quantity
            };
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
    }
}
