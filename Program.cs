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

// Cấu hình xác thực với các scheme 
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
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
}
else
{
    builder.Services.AddControllersWithViews();
}

// Thêm antiforgery
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// Thêm chính sách ủy quyền
builder.Services.AddAuthorization();
builder.Services.AddScoped<IOnlineVisitorTracker, OnlineVisitorTracker>();
builder.Services.Configure<VnPayOptions>(builder.Configuration.GetSection("VnPay"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IPendingCheckoutStore, PendingCheckoutStore>();
builder.Services.AddSingleton<IPolicyContentStore, JsonPolicyContentStore>();
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

    db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ContactRequests', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ContactRequests] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [FullName] NVARCHAR(100) NOT NULL,
        [Email] NVARCHAR(150) NOT NULL,
        [PhoneNumber] NVARCHAR(20) NOT NULL,
        [Message] NVARCHAR(2000) NOT NULL,
        [IsRead] BIT NOT NULL CONSTRAINT [DF_ContactRequests_IsRead] DEFAULT(0),
        [ReadAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL
    );
END

IF COL_LENGTH('dbo.ContactRequests', 'IsRead') IS NULL
BEGIN
    ALTER TABLE [dbo].[ContactRequests]
    ADD [IsRead] BIT NOT NULL CONSTRAINT [DF_ContactRequests_IsRead] DEFAULT(0);
END

IF COL_LENGTH('dbo.ContactRequests', 'ReadAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[ContactRequests]
    ADD [ReadAt] DATETIME2 NULL;
END
");

    db.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('dbo.Users', 'IsEmailVerified') IS NULL
BEGIN
    ALTER TABLE [dbo].[Users]
    ADD [IsEmailVerified] BIT NOT NULL CONSTRAINT [DF_Users_IsEmailVerified] DEFAULT(0);

    EXEC('UPDATE [dbo].[Users] SET [IsEmailVerified] = 1');
END

IF COL_LENGTH('dbo.Users', 'EmailVerificationOtp') IS NULL
BEGIN
    ALTER TABLE [dbo].[Users]
    ADD [EmailVerificationOtp] NVARCHAR(10) NULL;
END

IF COL_LENGTH('dbo.Users', 'EmailVerificationOtpExpiry') IS NULL
BEGIN
    ALTER TABLE [dbo].[Users]
    ADD [EmailVerificationOtpExpiry] DATETIME2 NULL;
END

IF COL_LENGTH('dbo.Users', 'PasswordResetOtp') IS NULL
BEGIN
    ALTER TABLE [dbo].[Users]
    ADD [PasswordResetOtp] NVARCHAR(10) NULL;
END

IF COL_LENGTH('dbo.Users', 'PasswordResetOtpExpiry') IS NULL
BEGIN
    ALTER TABLE [dbo].[Users]
    ADD [PasswordResetOtpExpiry] DATETIME2 NULL;
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