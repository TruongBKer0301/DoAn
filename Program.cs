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
IF OBJECT_ID(N'dbo.BlogPosts', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BlogPosts] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AdminId] INT NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Slug] NVARCHAR(220) NOT NULL,
        [Summary] NVARCHAR(500) NULL,
        [ContentHtml] NVARCHAR(MAX) NOT NULL,
        [CoverImageUrl] NVARCHAR(300) NULL,
        [IsPublished] BIT NOT NULL CONSTRAINT [DF_BlogPosts_IsPublished] DEFAULT(1),
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [PublishedAt] DATETIME2 NULL,
        CONSTRAINT [FK_BlogPosts_admin_AdminId] FOREIGN KEY ([AdminId]) REFERENCES [dbo].[admin]([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_BlogPosts_Slug] ON [dbo].[BlogPosts] ([Slug]);
    CREATE INDEX [IX_BlogPosts_AdminId] ON [dbo].[BlogPosts] ([AdminId]);
    CREATE INDEX [IX_BlogPosts_CreatedAt] ON [dbo].[BlogPosts] ([CreatedAt]);
END

IF COL_LENGTH('dbo.BlogPosts', 'Summary') IS NULL
BEGIN
    ALTER TABLE [dbo].[BlogPosts]
    ADD [Summary] NVARCHAR(500) NULL;
END

IF COL_LENGTH('dbo.BlogPosts', 'CoverImageUrl') IS NULL
BEGIN
    ALTER TABLE [dbo].[BlogPosts]
    ADD [CoverImageUrl] NVARCHAR(300) NULL;
END

IF COL_LENGTH('dbo.BlogPosts', 'IsPublished') IS NULL
BEGIN
    ALTER TABLE [dbo].[BlogPosts]
    ADD [IsPublished] BIT NOT NULL CONSTRAINT [DF_BlogPosts_IsPublished] DEFAULT(1);
END

IF COL_LENGTH('dbo.BlogPosts', 'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[BlogPosts]
    ADD [UpdatedAt] DATETIME2 NULL;
END

IF COL_LENGTH('dbo.BlogPosts', 'PublishedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[BlogPosts]
    ADD [PublishedAt] DATETIME2 NULL;
END

IF COL_LENGTH('dbo.BlogPosts', 'AdminId') IS NULL
BEGIN
    DECLARE @DefaultAdminId INT;
    SELECT TOP 1 @DefaultAdminId = [Id] FROM [dbo].[admin] ORDER BY [Id];

    ALTER TABLE [dbo].[BlogPosts]
    ADD [AdminId] INT NULL;

    IF @DefaultAdminId IS NOT NULL
    BEGIN
        EXEC sp_executesql
            N'UPDATE [dbo].[BlogPosts] SET [AdminId] = @AdminId WHERE [AdminId] IS NULL;',
            N'@AdminId INT',
            @AdminId = @DefaultAdminId;

        EXEC(N'ALTER TABLE [dbo].[BlogPosts] ALTER COLUMN [AdminId] INT NOT NULL;');
    END
END

IF COL_LENGTH('dbo.BlogPosts', 'AdminId') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_BlogPosts_admin_AdminId'
      AND parent_object_id = OBJECT_ID(N'dbo.BlogPosts')
)
BEGIN
    DECLARE @NullAdminCount INT = 0;
    EXEC sp_executesql
        N'SELECT @c = COUNT(1) FROM [dbo].[BlogPosts] WHERE [AdminId] IS NULL;',
        N'@c INT OUTPUT',
        @c = @NullAdminCount OUTPUT;

    IF @NullAdminCount = 0
    BEGIN
        EXEC(N'ALTER TABLE [dbo].[BlogPosts] WITH CHECK ADD CONSTRAINT [FK_BlogPosts_admin_AdminId] FOREIGN KEY ([AdminId]) REFERENCES [dbo].[admin]([Id]) ON DELETE CASCADE;');
    END
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_BlogPosts_Slug'
      AND object_id = OBJECT_ID(N'dbo.BlogPosts')
)
AND COL_LENGTH('dbo.BlogPosts', 'Slug') IS NOT NULL
BEGIN
    CREATE UNIQUE INDEX [IX_BlogPosts_Slug] ON [dbo].[BlogPosts] ([Slug]);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_BlogPosts_AdminId'
      AND object_id = OBJECT_ID(N'dbo.BlogPosts')
)
AND COL_LENGTH('dbo.BlogPosts', 'AdminId') IS NOT NULL
BEGIN
    EXEC(N'CREATE INDEX [IX_BlogPosts_AdminId] ON [dbo].[BlogPosts] ([AdminId]);');
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_BlogPosts_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.BlogPosts')
)
AND COL_LENGTH('dbo.BlogPosts', 'CreatedAt') IS NOT NULL
BEGIN
    CREATE INDEX [IX_BlogPosts_CreatedAt] ON [dbo].[BlogPosts] ([CreatedAt]);
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