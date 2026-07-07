using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting; // ✳️ اضافه شده برای Rate Limiting
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Models;
using resturanyar.Controllers.Api;
using resturanyar.Controllers.Api;
using resturanyar.Models.Settings;
using resturanyar.Utility;
using resturanyar.Utility;
using Resturanyar.Data;
using Resturanyar.Data;
using Resturanyar.Hubs;
using Resturanyar.Hubs;
using Serilog;
using Serilog;
using System.Text.Json;
using System.Threading.RateLimiting;
using System.Threading.RateLimiting;      // ✳️ اضافه شده برای Rate Limiting

var builder = WebApplication.CreateBuilder(args);

// ✳️ افزودن DbContext به DI
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(
            "https://delavita.ir",
            "https://www.delavita.ir",
            "https://resturanyar.ir",
            "https://localhost:7171"
        )
        .AllowAnyHeader()   // ← این اجازه می‌دهد همه هدرها از جمله Authorization
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// بعد در app:


// ✳️ افزودن کنترلرهای API و MVC
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
// ✳️ افزودن Swagger با Security Definition
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Resturanyar API", Version = "v1" });

    // تعریف امنیتی توکن
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "برای دسترسی به API، لطفاً توکن را به شکل زیر وارد کنید:\n\nBearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});


builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
// using Microsoft.AspNetCore.Authentication.Cookies;
builder.Services.AddHostedService<WarmupService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/ManagerLogin"; // مسیر لاگین پیش‌فرض
        options.Cookie.Name = "ResturanyarAuth";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.Configure<PayamakSettings>(
    builder.Configuration.GetSection("Payamak"));


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // ---- Global limiter with SignalR exclusion ----
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        // If the request is for SignalR, do not limit it
        if (httpContext.Request.Path.StartsWithSegments("/orderHub"))
        {
            return RateLimitPartition.GetNoLimiter<string>("signalr_exempt");
        }

        // Otherwise, use IP‑based limiting
        var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? httpContext.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown_ip";

        return RateLimitPartition.GetFixedWindowLimiter(ip,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 60,                // 60 requests per minute
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    // ---- OTP policy (to be used on specific endpoints) ----
    options.AddPolicy("OtpPolicy", httpContext =>
    {
        var phone = httpContext.Items["OtpPhoneNumber"]?.ToString();
        if (string.IsNullOrEmpty(phone))
        {
            phone = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: phone,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 3,                 // max 3 attempts
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(5) // within 5 minutes
            });
    });
});

// ✳️ راه‌اندازی لاگر Serilog
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog(); // جایگزین ILogger پیش‌فرض


var app = builder.Build();

app.UseCors("AllowAll");
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Resturanyar API v1");
        c.RoutePrefix = "swagger";
    });

    // ✅ Add this to see detailed errors in the browser
    app.UseDeveloperExceptionPage();
}

// ✅ Middleware برای هندل‌کردن تمام exceptionها (اصلاح شده برای امنیت بیشتر)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        // تغییر استاتوس کد به 500 برای مدیریت صحیح خطا در فرانت و کلاینت‌ها
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var isDev = app.Environment.IsDevelopment();

        var errorResponse = new
        {
            success = false,
            message = "خطای غیرمنتظره در سرور",
            // نمایش جزییات دقیق خطا فقط در محیط توسعه (لوکال) جهت جلوگیری از افشای اطلاعات
            detail = isDev ? ex.Message : "توضیحات خطا در محیط پروداکشن در دسترس نیست."
        };

        var json = JsonSerializer.Serialize(errorResponse);
        await context.Response.WriteAsync(json);
    }
});

//// ✳️ فعال‌سازی Swagger


// ✳️ کانفیگ‌های دیگر


app.UseHttpsRedirection();
app.UseStaticFiles();

//Console.WriteLine("ENV: " + builder.Environment.EnvironmentName);

// ✳️ Routing و Session
app.UseRouting();
// after app.UseRouting() and before app.UseRateLimiter()
app.UseMiddleware<OtpPhoneNumberExtractorMiddleware>();


// 🔒 فعال‌سازی میدل‌ور Rate Limiting (دقیقاً بعد از Routing و قبل از مپ شدن کنترلرها و هاب‌ها)
app.UseRateLimiter();

app.MapHub<OrderHub>("/orderHub");

app.UseSession();            // اگه هنوز سشن رو می‌خوای نگه داری (تا مرحله‌ی انتقال)
app.UseAuthentication();     // <-- مهم: اول auth
app.UseAuthorization();

// ✳️ Middleware توکن فقط برای مسیر /api
// ✳️ Middleware توکن فقط برای مسیر /api (به جز verifyotpweb)
// ✳️ Middleware توکن فقط برای مسیر /api (به جز verifyotpweb)
app.UseWhen(context =>
{
    var path = context.Request.Path.ToString();

    return path.StartsWith("/api") && !path.Contains("verifyotpweb") && !path.Contains("sendPriceList") && !path.Contains("addrestaurant") && !path.Contains("registerandlogin");
}, appBuilder =>
{
    appBuilder.UseMiddleware<StaticTokenMiddleware>();
    appBuilder.UseAuthorization();
});








/////////////////////////////////////////////////////////////////////////////////////برای فعال شدن وب این قسمت کامنت فعال و قسمت پایین کامنتت بشه////////////////////////////////////


//// ✳️ مسیردهی به APIها
app.MapControllers();

// ✳️ مسیردهی به MVC کنترلرها
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

//app.MapWhen(context => context.Request.Path.StartsWithSegments("/api"), appBuilder =>
//{
//    appBuilder.UseRouting();
//    appBuilder.UseAuthentication();
//    appBuilder.UseAuthorization();
//    appBuilder.UseEndpoints(endpoints =>
//    {
//        endpoints.MapControllers();
//    });
//});


//app.Use(async (context, next) =>
//{
//    if (!context.Request.Path.StartsWithSegments("/api"))
//    {
//        context.Response.StatusCode = 403;
//        await context.Response.WriteAsync("دسترسی به صفحات وب موقتاً غیرفعال است.");
//    }
//    else
//    {
//        await next();
//    }
//});


///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



app.Run();

