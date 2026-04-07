using LapTopBD.Data;
using LapTopBD.Utilities;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Lấy chuỗi kết nối từ appsettings.json
string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Cấu hình DbContext với SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Thêm bộ nhớ cache phân tán (dùng cho session)
builder.Services.AddDistributedMemoryCache();

// Thêm dịch vụ Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Chỉ dùng với HTTPS
});

// Cấu hình xác thực với các scheme (không đặt default scheme ở đây)
builder.Services.AddAuthentication()
    .AddCookie("AdminAuth", options =>
    {
        options.Cookie.Name = "AdminAuthCookie";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/Admin/Auth/Login";
        options.LogoutPath = "/Admin/Auth/Logout";
        options.AccessDeniedPath = "/Admin/Auth/AccessDenied";
    })
    .AddCookie("UserAuth", options =>
    {
        options.Cookie.Name = "UserAuthCookie";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/UserAuth/Login";
        options.LogoutPath = "/UserAuth/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

// Thêm dịch vụ MVC
builder.Services.AddControllersWithViews();

// Thêm antiforgery
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// Thêm chính sách ủy quyền
builder.Services.AddAuthorization();
builder.Services.AddScoped<IOnlineVisitorTracker, OnlineVisitorTracker>();
builder.Services.Configure<VnPayOptions>(builder.Configuration.GetSection("VnPay"));
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IPendingCheckoutStore, PendingCheckoutStore>();
builder.Services.AddHttpClient();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.VisitLogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[VisitLogs] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [VisitorId] NVARCHAR(64) NOT NULL,
        [VisitedAtUtc] DATETIME2 NOT NULL,
        [Path] NVARCHAR(200) NOT NULL,
        [Browser] NVARCHAR(64) NOT NULL,
        [Device] NVARCHAR(32) NOT NULL,
        [IpAddress] NVARCHAR(64) NOT NULL,
        [UserAgent] NVARCHAR(512) NOT NULL
    );
    CREATE INDEX [IX_VisitLogs_VisitedAtUtc] ON [dbo].[VisitLogs] ([VisitedAtUtc]);
    CREATE INDEX [IX_VisitLogs_VisitorId] ON [dbo].[VisitLogs] ([VisitorId]);
END
");
}

// Cấu hình pipeline HTTP
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
app.UseMiddleware<VisitorTrackingMiddleware>();

app.UseRouting();
app.UseAuthentication(); // Đọc cookie và điền thông tin vào HttpContext.User
app.UseAuthorization();
app.UseSession();

// Định tuyến
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