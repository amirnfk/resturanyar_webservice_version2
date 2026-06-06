using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using resturanyar.Models.CustomerModels;
using Resturanyar.Data;
using Resturanyar.Hubs;

namespace resturanyar.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomersController : Controller
    {
    
        private readonly IHubContext<OrderHub> _hubContext;

        private readonly AppDbContext _context;


        public CustomersController(AppDbContext context, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;

        }

        [HttpPost("addcustomer")]
        public IActionResult AddCustomer([FromBody] AddCustomerRequest request)
        {
            try
            {
                // بررسی وجود رستوران
                var restaurant = _context.Restaurants.Find(request.RestaurantId);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });

                // جستجوی مشتری با این شماره موبایل در همان رستوران (حتی غیرفعال‌ها)
                var existingCustomer = _context.Customers
                    .FirstOrDefault(c => c.RestaurantId == request.RestaurantId && c.Mobile == request.Mobile);

                if (existingCustomer != null)
                {
                    // اگر مشتری وجود دارد ولی غیرفعال است
                    if (!existingCustomer.IsActive)
                    {
                        // فعال کردن مجدد و به‌روزرسانی اطلاعات
                        existingCustomer.IsActive = true;
                        existingCustomer.FullName = request.FullName;
                        existingCustomer.Description = request.Description;
                        existingCustomer.UpdatedAt = DateTime.Now;

                        _context.SaveChanges();

                        return Ok(new
                        {
                            success = true,
                            message = "مشتری غیرفعال قبلی با موفقیت فعال و ویرایش شد",
                            customerId = existingCustomer.CustomerId,
                            wasReactivated = true
                        });
                    }
                    else
                    {
                        // مشتری فعال وجود دارد
                        return Ok(new { success = false, message = "این شماره موبایل قبلاً برای این رستوران ثبت شده است" });
                    }
                }

                // اگر مشتری وجود نداشت، مشتری جدید بساز
                var customer = new Customer
                {
                    RestaurantId = request.RestaurantId,
                    Mobile = request.Mobile,
                    FullName = request.FullName,
                    Description = request.Description,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Customers.Add(customer);
                _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "مشتری با موفقیت اضافه شد",
                    customerId = customer.CustomerId,
                    wasReactivated = false
                });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { success = false, message = innerMessage });
            }
        }


        [HttpGet("getcustomersstats/{restaurantId}")]
        public IActionResult GetCustomersStats(int restaurantId)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(restaurantId);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });

                var totalCount = _context.Customers
                    .Count(c => c.RestaurantId == restaurantId && c.IsActive);

                var activeCount = _context.Customers
                    .Count(c => c.RestaurantId == restaurantId && c.IsActive);

                return Ok(new
                {
                    success = true,
                    totalCount = totalCount,
                    activeCount = activeCount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }
        [HttpPost("editcustomer")]
        public IActionResult EditCustomer([FromBody] EditCustomerRequest request)
        {
            try
            {
                var customer = _context.Customers
                    .FirstOrDefault(c => c.CustomerId == request.CustomerId && c.RestaurantId == request.RestaurantId);
                if (customer == null)
                    return NotFound(new { success = false, message = "مشتری یافت نشد" });

                // بررسی یکتایی شماره موبایل (به جز خود این مشتری)
                bool mobileExists = _context.Customers
                    .Any(c => c.RestaurantId == request.RestaurantId && c.Mobile == request.Mobile && c.CustomerId != request.CustomerId);
                if (mobileExists)
                    return Ok(new { success = false, message = "این شماره موبایل قبلاً برای مشتری دیگری در این رستوران ثبت شده است" });

                customer.Mobile = request.Mobile;
                customer.FullName = request.FullName;
                customer.Description = request.Description;
                customer.UpdatedAt = DateTime.Now;

                _context.SaveChanges();

                return Ok(new { success = true, message = "مشتری با موفقیت ویرایش شد" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpPost("deletecustomer")] // soft delete
        public IActionResult DeleteCustomer([FromBody] DeleteCustomerRequest request)
        {
            try
            {
                var customer = _context.Customers
                    .FirstOrDefault(c => c.CustomerId == request.CustomerId && c.RestaurantId == request.RestaurantId);
                if (customer == null)
                    return NotFound(new { success = false, message = "مشتری یافت نشد" });

                customer.IsActive = false;
                customer.UpdatedAt = DateTime.Now;
                _context.SaveChanges();

                return Ok(new { success = true, message = "مشتری با موفقیت حذف شد (غیرفعال)" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpGet("getcustomers/{restaurantId}")]
        public IActionResult GetCustomers(int restaurantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string search = null)
        {
            try
            {
                var restaurant = _context.Restaurants.Find(restaurantId);
                if (restaurant == null)
                    return NotFound(new { success = false, message = "رستوران یافت نشد" });

                var query = _context.Customers
                    .Where(c => c.RestaurantId == restaurantId && c.IsActive);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(c => c.Mobile.Contains(search) ||
                                             (c.FullName != null && c.FullName.Contains(search)));
                }

                var totalCount = query.Count();
                var customers = query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        c.CustomerId,
                        c.Mobile,
                        c.FullName,
                        c.Description,
                        c.CreatedAt,
                        c.UpdatedAt
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = customers,
                    totalCount = totalCount,
                    currentPage = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }


        [HttpPost("addaddress")]
        public IActionResult AddAddress([FromBody] AddAddressRequest request)
        {
            try
            {
                var customer = _context.Customers.Find(request.CustomerId);
                if (customer == null)
                    return NotFound(new { success = false, message = "مشتری یافت نشد" });

                // اگر آدرس جدید به عنوان پیش‌فرض انتخاب شده، سایر آدرس‌های آن مشتری را غیرپیش‌فرض کن
                if (request.IsDefault)
                {
                    var existingDefaults = _context.CustomerAddresses
                        .Where(a => a.CustomerId == request.CustomerId && a.IsDefault);
                    foreach (var addr in existingDefaults)
                        addr.IsDefault = false;
                }

                var address = new CustomerAddress
                {
                    CustomerId = request.CustomerId,
                    Title = request.Title,
                    AddressText = request.AddressText,
                    Unit = request.Unit,
                    Floor = request.Floor,
                    PlateNumber = request.PlateNumber,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    IsDefault = request.IsDefault,
                    Description = request.Description,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.CustomerAddresses.Add(address);
                _context.SaveChanges();

                return Ok(new { success = true, message = "آدرس با موفقیت اضافه شد", addressId = address.AddressId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpPost("editaddress")]
        public IActionResult EditAddress([FromBody] EditAddressRequest request)
        {
            try
            {
                var address = _context.CustomerAddresses
                    .FirstOrDefault(a => a.AddressId == request.AddressId && a.CustomerId == request.CustomerId);
                if (address == null)
                    return NotFound(new { success = false, message = "آدرس یافت نشد" });

                // اگر این آدرس را پیش‌فرض می‌کنیم، سایر آدرس‌های مشتری را غیرپیش‌فرض کن
                if (request.IsDefault && !address.IsDefault)
                {
                    var otherAddresses = _context.CustomerAddresses
                        .Where(a => a.CustomerId == request.CustomerId && a.AddressId != request.AddressId);
                    foreach (var a in otherAddresses)
                        a.IsDefault = false;
                }

                address.Title = request.Title;
                address.AddressText = request.AddressText;
                address.Unit = request.Unit;
                address.Floor = request.Floor;
                address.PlateNumber = request.PlateNumber;
                address.Latitude = request.Latitude;
                address.Longitude = request.Longitude;
                address.IsDefault = request.IsDefault;
                address.Description = request.Description;
                address.UpdatedAt = DateTime.Now;

                _context.SaveChanges();

                return Ok(new { success = true, message = "آدرس با موفقیت ویرایش شد" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpPost("deleteaddress")]
        public IActionResult DeleteAddress([FromBody] DeleteAddressRequest request)
        {
            try
            {
                var address = _context.CustomerAddresses
                    .FirstOrDefault(a => a.AddressId == request.AddressId && a.CustomerId == request.CustomerId);
                if (address == null)
                    return NotFound(new { success = false, message = "آدرس یافت نشد" });

                _context.CustomerAddresses.Remove(address);
                _context.SaveChanges();

                return Ok(new { success = true, message = "آدرس با موفقیت حذف شد" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

        [HttpGet("getaddresses/{customerId}")]
        public IActionResult GetAddresses(int customerId)
        {
            try
            {
                var customer = _context.Customers.Find(customerId);
                if (customer == null)
                    return NotFound(new { success = false, message = "مشتری یافت نشد" });

                var addresses = _context.CustomerAddresses
                    .Where(a => a.CustomerId == customerId)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenByDescending(a => a.CreatedAt)
                    .Select(a => new
                    {
                        a.AddressId,
                        a.Title,
                        a.AddressText,
                        a.Unit,
                        a.Floor,
                        a.PlateNumber,
                        a.Latitude,
                        a.Longitude,
                        a.IsDefault,
                        a.Description,
                        a.CreatedAt,
                        a.UpdatedAt
                    })
                    .ToList();

                return Ok(new { success = true, data = addresses });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "خطا در سرور: " + ex.Message });
            }
        }

    }
}
