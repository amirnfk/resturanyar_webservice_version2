using Microsoft.AspNetCore.Mvc;
using Resturanyar.Data;
using resturanyar.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace resturanyar.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SubscriptionController(AppDbContext context)
        {
            _context = context;
        }

        
        [HttpPost("createsubscription")]
        public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == request.RestaurantId && r.owner_id == request.OwnerId);

                if (restaurant == null)
                {
                    return Ok(new SubscriptionResponse
                    {
                        Success = false,
                        Message = "رستوران یا مالک یافت نشد"
                    });
                }

                
                var owner = await _context.Owners
                    .FirstOrDefaultAsync(o => o.Id == request.OwnerId);

                if (owner == null)
                {
                    return Ok(new SubscriptionResponse
                    {
                        Success = false,
                        Message = "مالک یافت نشد"
                    });
                }

                // بررسی پلن اشتراک
                var subscriptionPlan = await _context.SubscriptionPlans
                    .FirstOrDefaultAsync(sp => sp.Id == request.SubscriptionPlanId && sp.IsActive);

                if (subscriptionPlan == null)
                {
                    return Ok(new SubscriptionResponse
                    {
                        Success = false,
                        Message = "پلن اشتراک یافت نشد یا غیرفعال است"
                    });
                }

                // غیرفعال کردن اشتراک‌های قبلی رستوران
                var activeSubscriptions = await _context.Subscriptions
                    .Where(s => s.RestaurantId == request.RestaurantId && s.Status == "Active")
                    .ToListAsync();

                foreach (var sub in activeSubscriptions)
                {
                    sub.Status = "Expired";
                    sub.UpdatedAt = DateTime.Now;
                }

                // ایجاد اشتراک جدید
                var subscription = new Subscription
                {
                    RestaurantId = request.RestaurantId,
                    OwnerId = request.OwnerId,
                    SubscriptionPlanId = request.SubscriptionPlanId,
                    SubscriptionPeriod = request.SubscriptionPeriod,
                    Status = "Active",
                    StartDate = DateTime.Now,
                    EndDate = CalculateEndDate(DateTime.Now, request.SubscriptionPeriod),
                    PurchaseDate = DateTime.Now,
                    PricePaid = request.PricePaid,
                    DiscountApplied = request.DiscountApplied,
                    PaymentMethod = request.PaymentMethod,
                    TransactionId = request.TransactionId,
                    IsPaid = true,
                    CafeBazarPurchaseToken = request.CafeBazarPurchaseToken,
                    CafeBazarOrderId = request.CafeBazarOrderId,
                    AutoRenew = request.AutoRenew,
                    NextRenewalDate = request.AutoRenew ? CalculateEndDate(DateTime.Now, request.SubscriptionPeriod) : null,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Subscriptions.Add(subscription);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new SubscriptionResponse
                {
                    Success = true,
                    Message = "اشتراک با موفقیت ایجاد شد",
                    Data = new SubscriptionData
                    {
                        Id = subscription.Id,
                        RestaurantName = restaurant.name,
                        PlanName = subscriptionPlan.Name,
                        Status = subscription.Status,
                        StartDate = subscription.StartDate,
                        EndDate = subscription.EndDate,
                        PricePaid = subscription.PricePaid,
                        AutoRenew = subscription.AutoRenew
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Ok(new SubscriptionResponse
                {
                    Success = false,
                    Message = "خطا در ایجاد اشتراک: " + ex.Message
                });
            }
        }
        private static string EncodePassword(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword)) return null;
            byte[] bytes = Encoding.UTF8.GetBytes(plainPassword);
            return Convert.ToBase64String(bytes);
        }

        private static string DecodePassword(string encodedPassword)
        {
            if (string.IsNullOrEmpty(encodedPassword)) return null;
            byte[] bytes = Convert.FromBase64String(encodedPassword);
            return Encoding.UTF8.GetString(bytes);
        }

        [HttpPost("getOwnerInfoAndSubscriptions")]
        public async Task<IActionResult> GetOwnerInfo(OwnerLoginWithRestaurantRequest request)
        {
            try
            {
                var owner = await _context.Owners.FirstOrDefaultAsync(o => o.Phone == request.Phone);

                if (owner == null)
                {
                    return Ok(new { success = false, message = "شماره تلفن یافت نشد" });
                }

                if (DecodePassword(owner.Password) != request.Password)
                {
                    return Ok(new { success = false, message = "رمز عبور برای این شماره تلفن نادرست است" });
                }

                // Check if restaurant belongs to owner
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == request.RestaurantId && r.owner_id == owner.Id);

                if (restaurant == null)
                {
                    return Ok(new { success = false, message = "رستوران متعلق به این کاربر نمی‌باشد" });
                }

                // Get active subscription for this restaurant
                var activeSubscription = await _context.Subscriptions
                    .Include(s => s.SubscriptionPlan)
                    .Where(s => s.RestaurantId == request.RestaurantId &&
                               s.Status == "Active" &&
                               s.EndDate > DateTime.Now)
                    .Select(s => new
                    {
                        plan_name = s.SubscriptionPlan.Name,
                        end_date = s.EndDate,
                        days_remaining = (s.EndDate - DateTime.Now).Days,
                        features = new
                        {
                            employee_limit = s.SubscriptionPlan.EmployeeLimit,
                            food_limit = s.SubscriptionPlan.FoodLimit,
                            can_use_web = s.SubscriptionPlan.CanUseWeb,
                            can_use_printer = s.SubscriptionPlan.CanUsePrinter
                        }
                    })
                    .FirstOrDefaultAsync();

                return Ok(new
                {
                    success = true,
                    message = "ورود با موفقیت انجام شد",
                    owner_name = owner.Name,
                    owner_phone = owner.Phone,
                    restaurant = new
                    {
                        restaurant_id = restaurant.restaurant_id,
                        name = restaurant.name,
                        restaurant_code = restaurant.restaurant_code
                    },
                    subscription = activeSubscription,
                    has_active_subscription = activeSubscription != null
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "خطا در سرور: " + ex.Message
                });
            }
        }

        [HttpPost("getUserInfoAndSubscriptions")]
        public async Task<IActionResult> LoginUser(LoginUserRequest request)
        {
            try
            {
                // پیدا کردن رستوران بر اساس کد
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_code == request.restaurant_code);

                if (restaurant == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "کد رستوران معتبر نیست"
                    });
                }

                // پیدا کردن کاربر در آن رستوران
                var user = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.name == request.name &&
                        u.restaurant_id == restaurant.restaurant_id);

                if (user == null || DecodePassword(user.password) != request.password)
                {
                    return Ok(new { success = false, message = "کاربری با این مشخصات یافت نشد" });
                }

                // پیدا کردن سابسکریپشن فعال مخصوص همین رستوران
                var activeSubscription = await _context.Subscriptions
                    .Include(s => s.SubscriptionPlan)
                    .Where(s => s.RestaurantId == restaurant.restaurant_id &&
                                s.Status == "Active" &&
                                s.EndDate > DateTime.Now)
                    .Select(s => new
                    {
                        plan_name = s.SubscriptionPlan.Name,
                        end_date = s.EndDate,
                        days_remaining = (s.EndDate - DateTime.Now).Days,
                        features = new
                        {
                            employee_limit = s.SubscriptionPlan.EmployeeLimit,
                            food_limit = s.SubscriptionPlan.FoodLimit,
                            can_use_web = s.SubscriptionPlan.CanUseWeb,
                            can_use_printer = s.SubscriptionPlan.CanUsePrinter
                        }
                    })
                    .FirstOrDefaultAsync();

                // بازگرداندن اطلاعات کاربر همراه با رستوران و سابسکریپشن
                return Ok(new
                {
                    success = true,
                    message = "ورود موفقیت‌آمیز بود",
                    user = new
                    {
                        user_id = user.user_id,
                        name = user.name,
                        role = user.role_id,
                        restaurant_id = user.restaurant_id,
                        restaurant_code = restaurant.restaurant_code,
                        restaurant_name = restaurant.name,
                        // پرمیشن‌ها
                        order_management_permission = user.order_management_permission,
                        kitchen_management_permission = user.kitchen_management_permission,
                        payment_management_permission = user.payment_management_permission
                    },
                    subscription = activeSubscription,
                    has_active_subscription = activeSubscription != null
                });
            }
            catch (Exception ex)
            {
                string fullErrorMessage = ex.Message;
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    fullErrorMessage += " --> " + inner.Message;
                    inner = inner.InnerException;
                }

                return Ok(new
                {
                    success = false,
                    message = "خطا در سرور: " + fullErrorMessage
                });
            }
        }


        [HttpPost("getUserPermissions")]
        public async Task<IActionResult> GetUserPermissions(OwnerLoginWithRestaurantRequest request)
        {
            try
            {
                var owner = await _context.Owners.FirstOrDefaultAsync(o => o.Phone == request.Phone);
                if (owner == null)
                    return Ok(new { success = false, message = "شماره تلفن یافت نشد" });

                if (DecodePassword(owner.Password) != request.Password)
                    return Ok(new { success = false, message = "رمز عبور نادرست است" });

                // بررسی مالکیت رستوران
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.restaurant_id == request.RestaurantId && r.owner_id == owner.Id);

                if (restaurant == null)
                    return Ok(new { success = false, message = "رستوران متعلق به این کاربر نمی‌باشد" });

                // اشتراک فعال
                var activeSubscription = await _context.Subscriptions
                    .Include(s => s.SubscriptionPlan)
                    .Where(s => s.RestaurantId == request.RestaurantId &&
                                s.Status == "Active" &&
                                s.EndDate > DateTime.Now)
                    .Select(s => new
                    {
                        plan_id = s.SubscriptionPlan.Id,
                        plan_name = s.SubscriptionPlan.Name,
                        plan_code = s.SubscriptionPlan.Code,
                        end_date = s.EndDate,
                        days_remaining = (s.EndDate - DateTime.Now).Days
                    })
                    .FirstOrDefaultAsync();

                // اگر اشتراک فعالی وجود نداشت، از پلن رایگان استفاده کن
                var planCodeToUse = activeSubscription?.plan_code ?? "FREE";

                var subscriptionPlan = await _context.SubscriptionPlans
                    .Where(sp => sp.Code == planCodeToUse)
                    .Select(sp => new
                    {
                        limits = new
                        {
                            max_employees = sp.EmployeeLimit,
                            max_foods = sp.FoodLimit,
                            max_categories = sp.CategoryLimit,
                            max_tables = sp.TableLimit
                        },
                        modules = new
                        {
                            web_access = sp.CanUseWeb,
                            printer_access = sp.CanUsePrinter,
                            menu_sharing = sp.CanShareMenu,
                            goftino_integration = sp.CanUseGoftino,
                            social_chat = sp.CanUseSocialChat,
                            realtime_updates = sp.CanUseRealtime,
                            user_management = sp.CanManageUsers,
                            table_management = sp.CanManageTables,
                            category_management = sp.CanManageCategories,
                            image_upload = sp.CanAddImages,
                            multi_restaurant = sp.CanManageMultipleRestaurants,
                            reports_access = sp.CanAccessReports
                        },
                        plan_info = new
                        {
                            name = sp.Name,
                            code = sp.Code,
                            description = sp.Description,
                            is_active = sp.IsActive
                        }
                    })
                    .FirstOrDefaultAsync();

                // حالا پاسخ نهایی
                return Ok(new
                {
                    success = true,
                    has_active_subscription = activeSubscription != null,
                    message = "دسترسی‌ها با موفقیت دریافت شد",

                    user_info = new
                    {
                        user_id = owner.Id,
                        user_name = owner.Name,
                        user_phone = owner.Phone,
                        user_role = "owner"
                    },

                    restaurant_info = new
                    {
                        restaurant_id = restaurant.restaurant_id,
                        restaurant_name = restaurant.name,
                        restaurant_code = restaurant.restaurant_code
                    },

                    subscription_info = new
                    {
                        plan_name = subscriptionPlan.plan_info.name,
                        plan_code = subscriptionPlan.plan_info.code,
                        end_date = activeSubscription?.end_date,
                        days_remaining = activeSubscription?.days_remaining ?? 0,
                        is_active = activeSubscription != null
                    },

                    permissions = new
                    {
                        can_access_web = subscriptionPlan.modules.web_access,
                        can_use_printer = subscriptionPlan.modules.printer_access,
                        can_share_menu = subscriptionPlan.modules.menu_sharing,
                        can_use_goftino = subscriptionPlan.modules.goftino_integration,
                        can_use_social_chat = subscriptionPlan.modules.social_chat,
                        can_use_realtime = subscriptionPlan.modules.realtime_updates,
                        can_manage_users = subscriptionPlan.modules.user_management,
                        can_manage_tables = subscriptionPlan.modules.table_management,
                        can_manage_category = subscriptionPlan.modules.category_management,
                        can_upload_images = subscriptionPlan.modules.image_upload,
                        can_manage_multiple_restaurants = subscriptionPlan.modules.multi_restaurant,
                        can_access_reports = subscriptionPlan.modules.reports_access,

                        max_employees_allowed = subscriptionPlan.limits.max_employees,
                        max_foods_allowed = subscriptionPlan.limits.max_foods,
                        max_categories_allowed = subscriptionPlan.limits.max_categories,
                        max_tables_allowed = subscriptionPlan.limits.max_tables,

                        has_premium_access = planCodeToUse != "FREE"
                    },

                    ui_settings = new
                    {
                        show_premium_features = subscriptionPlan.modules.reports_access ||
                                                subscriptionPlan.modules.multi_restaurant,
                        show_advanced_settings = subscriptionPlan.modules.user_management ||
                                                 subscriptionPlan.modules.realtime_updates,
                        allow_menu_customization = subscriptionPlan.modules.menu_sharing &&
                                                   subscriptionPlan.modules.image_upload
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "خطا در دریافت دسترسی‌ها: " + ex.Message
                });
            }
        }



        // دریافت اشتراک‌های یک رستوران
        [HttpGet("getrestaurantsubscriptions/{restaurantId}")]
        public async Task<IActionResult> GetRestaurantSubscriptions(int restaurantId)
        {
            try
            {
                var subscriptions = await _context.Subscriptions
                    .Where(s => s.RestaurantId == restaurantId)
                    .Include(s => s.SubscriptionPlan)
                    .Include(s => s.Restaurant)
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new
                    {
                        s.Id,
                        s.Status,
                        PlanName = s.SubscriptionPlan.Name,
                        s.SubscriptionPeriod,
                        s.StartDate,
                        s.EndDate,
                        s.PricePaid,
                        s.AutoRenew,
                        s.IsPaid,
                        DaysRemaining = (s.EndDate - DateTime.Now).Days
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Success = true,
                    Message = "اشتراک‌ها با موفقیت دریافت شد",
                    Data = subscriptions
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Success = false,
                    Message = "خطا در دریافت اشتراک‌ها: " + ex.Message
                });
            }
        }

        // دریافت اشتراک فعال رستوران
        [HttpGet("getactivesubscription/{restaurantId}")]
        public async Task<IActionResult> GetActiveSubscription(int restaurantId)
        {
            try
            {
                var activeSubscription = await _context.Subscriptions
                    .Where(s => s.RestaurantId == restaurantId && s.Status == "Active" && s.EndDate > DateTime.Now)
                    .Include(s => s.SubscriptionPlan)
                    .Select(s => new SubscriptionData
                    {
                        Id = s.Id,
                        RestaurantName = s.Restaurant.name,
                        PlanName = s.SubscriptionPlan.Name,
                        Status = s.Status,
                        StartDate = s.StartDate,
                        EndDate = s.EndDate,
                        PricePaid = s.PricePaid,
                        AutoRenew = s.AutoRenew
                    })
                    .FirstOrDefaultAsync();

                if (activeSubscription == null)
                {
                    return Ok(new SubscriptionResponse
                    {
                        Success = false,
                        Message = "اشتراک فعالی یافت نشد"
                    });
                }

                return Ok(new SubscriptionResponse
                {
                    Success = true,
                    Message = "اشتراک فعال با موفقیت دریافت شد",
                    Data = activeSubscription
                });
            }
            catch (Exception ex)
            {
                return Ok(new SubscriptionResponse
                {
                    Success = false,
                    Message = "خطا در دریافت اشتراک: " + ex.Message
                });
            }
        }

        // تمدید اشتراک
        [HttpPost("renewsubscription/{subscriptionId}")]
        public async Task<IActionResult> RenewSubscription(int subscriptionId)
        {
            try
            {
                var subscription = await _context.Subscriptions
                    .Include(s => s.SubscriptionPlan)
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId);

                if (subscription == null)
                {
                    return Ok(new SubscriptionResponse
                    {
                        Success = false,
                        Message = "اشتراک یافت نشد"
                    });
                }

                subscription.StartDate = DateTime.Now;
                subscription.EndDate = CalculateEndDate(DateTime.Now, subscription.SubscriptionPeriod);
                subscription.Status = "Active";
                subscription.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new SubscriptionResponse
                {
                    Success = true,
                    Message = "اشتراک با موفقیت تمدید شد",
                    Data = new SubscriptionData
                    {
                        Id = subscription.Id,
                        PlanName = subscription.SubscriptionPlan.Name,
                        Status = subscription.Status,
                        StartDate = subscription.StartDate,
                        EndDate = subscription.EndDate,
                        PricePaid = subscription.PricePaid,
                        AutoRenew = subscription.AutoRenew
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new SubscriptionResponse
                {
                    Success = false,
                    Message = "خطا در تمدید اشتراک: " + ex.Message
                });
            }
        }

       
        [HttpPost("cancelsubscription/{subscriptionId}")]
        public async Task<IActionResult> CancelSubscription(int subscriptionId)
        {
            try
            {
                var subscription = await _context.Subscriptions.FindAsync(subscriptionId);
                if (subscription == null)
                {
                    return Ok(new SubscriptionResponse
                    {
                        Success = false,
                        Message = "اشتراک یافت نشد"
                    });
                }

                subscription.Status = "Canceled";
                subscription.AutoRenew = false;
                subscription.CanceledAt = DateTime.Now;
                subscription.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new SubscriptionResponse
                {
                    Success = true,
                    Message = "اشتراک با موفقیت لغو شد"
                });
            }
            catch (Exception ex)
            {
                return Ok(new SubscriptionResponse
                {
                    Success = false,
                    Message = "خطا در لغو اشتراک: " + ex.Message
                });
            }
        }


 


        // متد کمکی برای محاسبه تاریخ پایان
        private DateTime CalculateEndDate(DateTime startDate, string period)
        {
            return period switch
            {
                "Monthly" => startDate.AddMonths(1),
                "3Monthly" => startDate.AddMonths(3),
                "6Monthly" => startDate.AddMonths(6),
                "12Monthly" => startDate.AddMonths(12),
                _ => startDate.AddMonths(1)
            };
        }





        
    }
}
