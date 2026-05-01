using LapTopBD.Data;
using LapTopBD.Utilities;
using Microsoft.EntityFrameworkCore;

try
{
var builder = WebApplication.CreateBuilder(args);

// =====================
// AZURE SQL (Entra ID / AD)
// =====================
// KHÔNG cần tự gắn token, Azure sẽ xử lý qua connection string
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Missing 'DefaultConnection' connection string.");
    }

    options.UseSqlServer(connectionString);
});

// =====================
// CONTROLLERS + JSON
// =====================
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// =====================
// SESSION
// =====================
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// =====================
// AUTH
// =====================
builder.Services.AddAuthentication()
    .AddCookie("AdminAuth", options =>
    {
        options.Cookie.Name = "AdminAuthCookie";
        options.LoginPath = "/Admin/Auth/Login";
        options.AccessDeniedPath = "/Admin/Auth/AccessDenied";
    })
    .AddCookie("UserAuth", options =>
    {
        options.Cookie.Name = "UserAuthCookie";
        options.LoginPath = "/UserAuth/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

// =====================
// SERVICES
// =====================
builder.Services.AddScoped<IOnlineVisitorTracker, OnlineVisitorTracker>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IPendingCheckoutStore, PendingCheckoutStore>();
builder.Services.AddSingleton<IPolicyContentStore, JsonPolicyContentStore>();

builder.Services.Configure<VnPayOptions>(
    builder.Configuration.GetSection("VnPay"));

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddHttpClient();

// =====================
// BUILD APP
// =====================
var app = builder.Build();

// =====================
// PIPELINE
// =====================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<UserAuthHydrationMiddleware>();
app.UseAuthorization();

app.UseSession();

app.UseMiddleware<VisitorTrackingMiddleware>();

app.MapControllers();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Admin}/{action=Dashboard}/{id?}");

app.MapControllerRoute(
    name: "Detail",
    pattern: "chi-tiet/{slug}",
    defaults: new { controller = "Home", action = "Detail" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine("Application startup failed:");
    Console.Error.WriteLine(ex.ToString());
    throw;
}