using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using resturanyar.Models;
using resturanyar.Models.Receipt;
using Resturanyar.Data;

namespace resturanyar.Services.Fulfillment
{
    public class OrderFulfillmentService : IOrderFulfillmentService
    {
        public const string TakeawayTableSentinel = "بیرون‌بر";
        public const string DeliveryTableSentinel = "پیک";

        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public OrderFulfillmentService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public bool IsGlobalEnabled()
        {
            return _configuration.GetValue("Features:FulfillmentOrders:Enabled", false);
        }

        public async Task<FulfillmentValidationResult> ValidateAndPrepareAsync(
            CreateOrderRequest request,
            bool restaurantEnableTakeaway,
            bool restaurantEnableDelivery,
            CancellationToken cancellationToken = default)
        {
            var orderType = request.OrderType.HasValue
                ? (OrderTypeKind)request.OrderType.Value
                : OrderTypeKind.DineIn;

            if (orderType == OrderTypeKind.DineIn)
            {
                // Preserve legacy Dine-In behavior: no new validation beyond existing client contracts.
                return FulfillmentValidationResult.Ok(OrderTypeKind.DineIn, request.TableNumber ?? string.Empty);
            }

            if (!IsGlobalEnabled())
                return FulfillmentValidationResult.Fail("ثبت سفارش بیرون‌بر/ارسال در حال حاضر غیرفعال است.", 403);

            if (orderType == OrderTypeKind.Takeaway && !restaurantEnableTakeaway)
                return FulfillmentValidationResult.Fail("سفارش بیرون‌بر برای این رستوران فعال نیست.", 403);

            if (orderType == OrderTypeKind.Delivery && !restaurantEnableDelivery)
                return FulfillmentValidationResult.Fail("سفارش ارسال برای این رستوران فعال نیست.", 403);

            if (orderType != OrderTypeKind.Takeaway && orderType != OrderTypeKind.Delivery)
                return FulfillmentValidationResult.Fail("نوع سفارش نامعتبر است.");

            string? customerName = null;
            string? customerPhone = null;

            if (request.CustomerId.HasValue)
            {
                var customer = await _context.Customers.AsNoTracking()
                    .FirstOrDefaultAsync(
                        c => c.CustomerId == request.CustomerId.Value
                             && c.RestaurantId == request.RestaurantId
                             && c.IsActive,
                        cancellationToken);

                if (customer == null)
                    return FulfillmentValidationResult.Fail("مشتری با این شناسه برای این رستوران یافت نشد.");

                customerName = customer.FullName;
                customerPhone = customer.Mobile;
            }
            else if (orderType == OrderTypeKind.Delivery)
            {
                return FulfillmentValidationResult.Fail("برای سفارش ارسال، انتخاب مشتری الزامی است.");
            }

            int? addressId = request.CustomerAddressId;
            string? addressSnapshot = null;

            if (orderType == OrderTypeKind.Delivery)
            {
                if (addressId.HasValue)
                {
                    var address = await _context.CustomerAddresses.AsNoTracking()
                        .FirstOrDefaultAsync(a => a.AddressId == addressId.Value, cancellationToken);

                    if (address == null)
                        return FulfillmentValidationResult.Fail("آدرس انتخاب‌شده یافت نشد.");

                    if (address.CustomerId != request.CustomerId!.Value)
                        return FulfillmentValidationResult.Fail("آدرس انتخاب‌شده متعلق به این مشتری نیست.");

                    addressSnapshot = BuildAddressSnapshot(address.AddressText, address.Unit, address.Floor, address.PlateNumber);
                }

                if (!string.IsNullOrWhiteSpace(request.AddressText))
                {
                    addressSnapshot = request.AddressText.Trim();
                }

                if (string.IsNullOrWhiteSpace(addressSnapshot))
                    return FulfillmentValidationResult.Fail("برای سفارش ارسال، آدرس الزامی است.");
            }
            else if (addressId.HasValue)
            {
                var address = await _context.CustomerAddresses.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.AddressId == addressId.Value, cancellationToken);

                if (address != null && request.CustomerId.HasValue && address.CustomerId == request.CustomerId.Value)
                {
                    addressSnapshot = BuildAddressSnapshot(address.AddressText, address.Unit, address.Floor, address.PlateNumber);
                }
                else
                {
                    addressId = null;
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.AddressText))
            {
                addressSnapshot = request.AddressText.Trim();
            }

            var tableNumber = string.IsNullOrWhiteSpace(request.TableNumber)
                ? (orderType == OrderTypeKind.Takeaway ? TakeawayTableSentinel : DeliveryTableSentinel)
                : request.TableNumber.Trim();

            var fulfillment = new OrderFulfillment
            {
                CustomerAddressId = addressId,
                CustomerNameSnapshot = customerName,
                PhoneSnapshot = customerPhone,
                AddressSnapshot = addressSnapshot,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return FulfillmentValidationResult.Ok(orderType, tableNumber, fulfillment);
        }

        public async Task AttachFulfillmentAsync(Order order, OrderFulfillment fulfillment, CancellationToken cancellationToken = default)
        {
            fulfillment.OrderId = order.OrderId;
            fulfillment.UpdatedAt = DateTime.UtcNow;
            _context.OrderFulfillments.Add(fulfillment);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> TryUpdateFulfillmentSnapshotsAsync(
            int orderId,
            int restaurantId,
            int? customerId,
            int? customerAddressId,
            string? addressText,
            CancellationToken cancellationToken = default)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.RestaurantId == restaurantId, cancellationToken);

            if (order == null)
                return false;

            if (order.OrderType != OrderTypeKind.Takeaway && order.OrderType != OrderTypeKind.Delivery)
                return false;

            var fulfillment = await _context.OrderFulfillments
                .FirstOrDefaultAsync(f => f.OrderId == orderId, cancellationToken);

            if (fulfillment == null)
            {
                fulfillment = new OrderFulfillment
                {
                    OrderId = orderId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.OrderFulfillments.Add(fulfillment);
            }

            if (customerId.HasValue)
            {
                var customer = await _context.Customers.AsNoTracking()
                    .FirstOrDefaultAsync(
                        c => c.CustomerId == customerId.Value
                             && c.RestaurantId == restaurantId
                             && c.IsActive,
                        cancellationToken);

                if (customer == null)
                    return false;

                fulfillment.CustomerNameSnapshot = customer.FullName;
                fulfillment.PhoneSnapshot = customer.Mobile;
            }

            if (customerAddressId.HasValue)
            {
                var address = await _context.CustomerAddresses.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.AddressId == customerAddressId.Value, cancellationToken);

                if (address == null)
                    return false;

                var ownerCustomerId = customerId ?? order.CustomerId;
                if (!ownerCustomerId.HasValue || address.CustomerId != ownerCustomerId.Value)
                    return false;

                fulfillment.CustomerAddressId = address.AddressId;
                fulfillment.AddressSnapshot = BuildAddressSnapshot(address.AddressText, address.Unit, address.Floor, address.PlateNumber);
            }

            if (!string.IsNullOrWhiteSpace(addressText))
            {
                fulfillment.AddressSnapshot = addressText.Trim();
            }

            fulfillment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private static string BuildAddressSnapshot(string addressText, string? unit, string? floor, string? plate)
        {
            var parts = new List<string> { addressText.Trim() };
            if (!string.IsNullOrWhiteSpace(unit))
                parts.Add($"واحد {unit.Trim()}");
            if (!string.IsNullOrWhiteSpace(floor))
                parts.Add($"طبقه {floor.Trim()}");
            if (!string.IsNullOrWhiteSpace(plate))
                parts.Add($"پلاک {plate.Trim()}");
            return string.Join("، ", parts);
        }
    }
}
