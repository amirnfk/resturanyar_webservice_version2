using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using resturanyar.Controllers.Api;
using Resturanyar.Data;
using Serilog;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Resturanyar.Hubs;


var builder = WebApplication.CreateBuilder(args);

// ✳️ افزودن DbContext به DI
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost",
        policy =>
        {
            policy.WithOrigins("https://localhost:7171", "http://delavita.ir/")  
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                   .AllowCredentials(); ;
        });
});

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

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/ManagerLogin"; // مسیر لاگین پیش‌فرض
        options.Cookie.Name = "ResturanyarAuth";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// ✳️ راه‌اندازی لاگر Serilog
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog(); // جایگزین ILogger پیش‌فرض


var app = builder.Build();

app.UseCors("AllowLocalhost");

// ✅ Middleware برای هندل‌کردن تمام exceptionها به صورت JSON با status 200
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            success = false,
            message = "خطای غیرمنتظره در سرور",
            detail = ex.Message
        };

        var json = JsonSerializer.Serialize(errorResponse);
        await context.Response.WriteAsync(json);
    }
});

//// ✳️ فعال‌سازی Swagger
//app.UseSwagger();
//app.UseSwaggerUI(c =>
//{
//    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Resturanyar API v1");
//    c.RoutePrefix = "swagger";
//});

// ✳️ کانفیگ‌های دیگر
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

//Console.WriteLine("ENV: " + builder.Environment.EnvironmentName);

// ✳️ Routing و Session
app.UseRouting();
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
   
    return path.StartsWith("/api") && !path.Contains("verifyotpweb" ) && !path.Contains("addrestaurant")&& !path.Contains("registerandlogin"); 
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
