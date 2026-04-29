using LapTopBD.Data;
using LapTopBD.ViewModels;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LapTopBD.Models.ViewModels.Admin;
using LapTopBD.Models.ViewModels;
using LapTopBD.Utilities;


namespace LapTopBD.Areas.Admin.Controllers
{
    [Area("Admin")]
   [Authorize(AuthenticationSchemes = "AdminAuth")]
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOnlineVisitorTracker _onlineVisitorTracker;
        private readonly IHttpClientFactory _httpClientFactory;

        public AdminController(
            ApplicationDbContext context,
            IOnlineVisitorTracker onlineVisitorTracker,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _onlineVisitorTracker = onlineVisitorTracker;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost]
        [Route("fetch-image-from-url")]
        public async Task<IActionResult> FetchImageFromUrl([FromBody] FetchImageRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest(new { success = false, message = "URL ảnh không hợp lệ." });
            }

            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                uri.IsLoopback ||
                uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "Chỉ chấp nhận URL ảnh HTTP/HTTPS hợp lệ." });
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    return BadRequest(new { success = false, message = "Không thể tải ảnh từ URL này." });
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = "URL không phải là ảnh hợp lệ." });
                }

                const long maxBytes = 5 * 1024 * 1024;
                if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > maxBytes)
                {
                    return BadRequest(new { success = false, message = "Ảnh vượt quá giới hạn 5MB." });
                }

                await using var inputStream = await response.Content.ReadAsStreamAsync();
                await using var outputStream = new MemoryStream();

                var buffer = new byte[81920];
                int read;
                long totalRead = 0;

                while ((read = await inputStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    totalRead += read;
                    if (totalRead > maxBytes)
                    {
                        return BadRequest(new { success = false, message = "Ảnh vượt quá giới hạn 5MB." });
                    }

                    await outputStream.WriteAsync(buffer.AsMemory(0, read));
                }

                var extension = Path.GetExtension(uri.AbsolutePath);
                if (string.IsNullOrWhiteSpace(extension) || !Regex.IsMatch(extension, "^\\.(jpg|jpeg|png|gif|webp)$", RegexOptions.IgnoreCase))
                {
                    extension = contentType switch
                    {
                        "image/png" => ".png",
                        "image/gif" => ".gif",
                        "image/webp" => ".webp",
                        _ => ".jpg"
                    };
                }

                var fileName = $"dropped-image{extension}";
                return File(outputStream.ToArray(), contentType, fileName);
            }
            catch
            {
                return BadRequest(new { success = false, message = "Không thể xử lý ảnh từ URL." });
            }
        }

        [HttpGet]
        [Route("")]
        [Route("dashboard")]
        public async Task<IActionResult> Dashboard(int? month, int? year)
        {
            var viewModel = await BuildDashboardViewModel(month, year);
            return View(viewModel);
        }

        [HttpGet]
        [Route("dashboard-data")]
        public async Task<IActionResult> DashboardData(int? month, int? year)
        {
            var viewModel = await BuildDashboardViewModel(month, year);

            return Json(new
            {
                selectedMonth = viewModel.SelectedMonth,
                selectedYear = viewModel.SelectedYear,
                onlineVisitors = viewModel.OnlineVisitors,
                monthlyVisits = viewModel.MonthlyVisits,
                totalVisits = viewModel.TotalVisits,
                monthlyVisitLabels = viewModel.MonthlyVisitLabels,
                monthlyVisitSeries = viewModel.MonthlyVisitSeries,
                browserStats = viewModel.BrowserStats,
                deviceStats = viewModel.DeviceStats,
                topIps = viewModel.TopIps,
                totalUsers = viewModel.TotalUsers,
                totalOrders = viewModel.TotalOrders,
                weekRevenue = viewModel.WeekRevenue,
                totalProduct = viewModel.TotalProduct
            });
        }

        private async Task<AdminDashboardViewModel> BuildDashboardViewModel(int? month, int? year)
        {
            var now = DateTime.Now;
            var today = now.Date;
            var weekStart = today.AddDays(-6);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var selectedMonth = month.GetValueOrDefault(now.Month);
            var selectedYear = year.GetValueOrDefault(now.Year);

            if (selectedMonth < 1 || selectedMonth > 12)
            {
                selectedMonth = now.Month;
            }

            if (selectedYear < 2020 || selectedYear > now.Year + 1)
            {
                selectedYear = now.Year;
            }

            var allOrders = await _context.Order
                .AsNoTracking()
                .ToListAsync();

            var todayOrders = allOrders.Where(o => o.OrderDate.Date == today).ToList();
            var weekOrders = allOrders.Where(o => o.OrderDate.Date >= weekStart && o.OrderDate.Date <= today).ToList();
            var monthOrders = allOrders.Where(o => o.OrderDate.Date >= monthStart && o.OrderDate.Date <= today).ToList();

            var labels = new List<string>();
            var dailyOrderCounts = new List<int>();
            var dailyRevenue = new List<decimal>();

            for (var i = 6; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var dayOrders = allOrders.Where(o => o.OrderDate.Date == date).ToList();

                labels.Add(date.ToString("dd/MM"));
                dailyOrderCounts.Add(dayOrders.Count);
                dailyRevenue.Add(dayOrders.Sum(o => o.TotalPrice));
            }

            var monthlyVisits = await _onlineVisitorTracker.GetDailyVisitCountsAsync(selectedYear, selectedMonth);
            var browserStats = (await _onlineVisitorTracker.GetTopBrowsersAsync(7))
                .Select(x => new DashboardStatItem { Name = x.Key, Value = x.Value })
                .ToList();
            var deviceStats = (await _onlineVisitorTracker.GetDeviceBreakdownAsync())
                .Select(x => new DashboardStatItem { Name = x.Key, Value = x.Value })
                .ToList();
            var ipStats = (await _onlineVisitorTracker.GetTopIpsAsync(7))
                .Select(x => new DashboardStatItem { Name = x.Key, Value = x.Value })
                .ToList();

            var monthlyVisitLabels = Enumerable.Range(1, monthlyVisits.Count)
                .Select(day => $"D{day}")
                .ToList();

            var monthlyVisitTotal = monthlyVisits.Sum();
            var totalVisits = await _onlineVisitorTracker.GetTotalVisitCountAsync();

            var viewModel = new AdminDashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalOrders = allOrders.Count,
                TotalProduct = await _context.Product.CountAsync(),

                TodayOrders = todayOrders.Count,
                WeekOrders = weekOrders.Count,
                MonthOrders = monthOrders.Count,
                OnlineVisitors = await _onlineVisitorTracker.GetOnlineCountAsync(TimeSpan.FromMinutes(5)),

                TodayRevenue = todayOrders.Sum(o => o.TotalPrice),
                WeekRevenue = weekOrders.Sum(o => o.TotalPrice),
                MonthRevenue = monthOrders.Sum(o => o.TotalPrice),

                Last7DaysLabels = labels,
                Last7DaysOrderCounts = dailyOrderCounts,
                Last7DaysRevenue = dailyRevenue,

                SelectedMonth = selectedMonth,
                SelectedYear = selectedYear,
                MonthlyVisitLabels = monthlyVisitLabels,
                MonthlyVisitSeries = monthlyVisits.ToList(),
                MonthlyVisits = monthlyVisitTotal,
                TotalVisits = totalVisits,

                BrowserStats = browserStats,
                DeviceStats = deviceStats,
                TopIps = ipStats
            };

            return viewModel;
        }

        // Danh sách Admins
        [HttpGet]
        [Route("list-admins")] 
        public async Task<IActionResult> ListAdmins(int page = 1, int pageSize = 3)
        {
            var totalAdmins = await _context.admin.CountAsync();
            var totalPages = (int)Math.Ceiling(totalAdmins / (double)pageSize);

            var admins = await _context.admin
                .OrderBy(a => a.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AdminViewModel
                {
                    Id = a.Id,
                    Username = a.Username,
                    CreationDate = a.CreationDate,
                    FullName = a.FullName,
                    Avatar = a.Avatar,
                    Roles = a.Roles,
                    Status = a.Status
                })
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(admins);
        }


        // Form chỉnh sửa Admin
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("edit/{id}")]
        public IActionResult Edit(int id)
        {
            var admin = _context.admin.Find(id);
            if (admin == null)
            {
                return NotFound();
            }

            //  AdminViewModel
            var model = new AdminViewModel
            {
                Id = admin.Id,
                Username = admin.Username,
                FullName = admin.FullName,
                Avatar = admin.Avatar,
                Roles = admin.Roles,
                Status = admin.Status,
                CreationDate = admin.CreationDate
            };

            return View("EditAdmin", model);
        }


        [HttpPost]
        [Route("edit/{id}")]
        public async Task<IActionResult> Edit(int id, [FromForm] AdminViewModel model, IFormFile? AvatarFile)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                              .Select(e => e.ErrorMessage);
                return Json(new { success = false, message = "Lỗi dữ liệu: " + string.Join(", ", errors) });
            }

            var admin = await _context.admin.FindAsync(id);
            if (admin == null)
            {
                return Json(new { success = false, message = "Không tìm thấy Admin!" });
            }

            // Kiểm tra nếu có đổi mật khẩu
            if (!string.IsNullOrEmpty(model.OldPassword) && !string.IsNullOrEmpty(model.NewPassword))
            {
                // Kiểm tra mật khẩu cũ mà không hash
                if (!PasswordHelper.VerifyPassword(model.OldPassword, admin.Password))
                {
                    return Json(new { success = false, message = "Mật khẩu cũ không chính xác!" });
                }


                // Chỉ hash mật khẩu mới
                admin.Password = PasswordHelper.HashPassword(model.NewPassword);
            }

            // Xử lý ảnh đại diện nếu có upload ảnh mới
            if (AvatarFile?.Length > 0)
            {
                string fileExtension = Path.GetExtension(AvatarFile.FileName).ToLower();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Json(new { success = false, message = "Chỉ hỗ trợ file JPG, JPEG, PNG." });
                }

                string uploadsFolder = Path.Combine("wwwroot", "avatar");
                Directory.CreateDirectory(uploadsFolder); // Tạo thư mục nếu chưa có

                string uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await AvatarFile.CopyToAsync(stream);

                // Xóa ảnh cũ nếu không phải mặc định
                if (!string.IsNullOrEmpty(admin.Avatar) && admin.Avatar != "/avatar/default-avatar.png")
                {
                    string oldFilePath = Path.Combine("wwwroot", admin.Avatar.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath)) System.IO.File.Delete(oldFilePath);
                }

                admin.Avatar = $"/avatar/{uniqueFileName}";
            }


            // Cập nhật thông tin Admin
            admin.FullName = model.FullName;
            admin.Username = model.Username ?? string.Empty;
            admin.Status = model.Status ?? string.Empty;
            admin.Roles = model.Roles;
            admin.UpdationDate = DateTime.Now;

            _context.Update(admin);
            await _context.SaveChangesAsync();

            // Kiểm tra nếu user đang đăng nhập chính là user đang được cập nhật
            var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (currentUserIdClaim != null && int.Parse(currentUserIdClaim.Value) == admin.Id)
            {
                // Đăng xuất
                await HttpContext.SignOutAsync("AdminAuth");

                // Tạo claims mới
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                    new Claim(ClaimTypes.Name, admin.Username),
                    new Claim("FullName", admin.FullName ?? string.Empty),
                    new Claim(ClaimTypes.Role, admin.Roles ?? string.Empty),
                    new Claim("Avatar", admin.Avatar ?? "/avatar/default-avatar.png")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                // Thiết lập cookie authentication properties
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24),
                    AllowRefresh = true
                };

                // Đăng nhập lại với claims mới
                await HttpContext.SignInAsync("AdminAuth", claimsPrincipal, authProperties);

                return Json(new
                {
                    success = true,
                    message = "Cập nhật thành công!",
                    needRefresh = true
                });
            }

            return Json(new { success = true, message = "Cập nhật thành công!" });
        }


        //Thêm Admin
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("add-admin")]
        public async Task<IActionResult> AddAdmin([FromForm] AdminViewModel model, IFormFile AvatarFile)
        {
            if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin!" });
            }

            var existingAdmin = await _context.admin.FirstOrDefaultAsync(a => a.Username == model.Username);
            if (existingAdmin != null)
            {
                return Json(new { success = false, message = "Tên đăng nhập đã tồn tại!" });
            }

            string avatarPath = "/avatar/default-avatar.png"; // Ảnh mặc định

            if (AvatarFile != null && AvatarFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/avatar");
                string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(AvatarFile.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await AvatarFile.CopyToAsync(stream);
                }

                avatarPath = "/avatar/" + uniqueFileName;
            }

            try
            {
                var newAdmin = new Models.Admin
                {
                    Username = model.Username,
                    FullName = model.FullName ?? "Chưa cập nhật",
                    Roles = model.Roles ?? "User",
                    Password = PasswordHelper.HashPassword(model.Password),
                    Status = model.Status ?? "Hoạt động",
                    CreationDate = DateTime.Now,
                    Avatar = avatarPath
                };

                _context.admin.Add(newAdmin);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Thêm Admin thành công!",
                    avatarUrl = avatarPath,
                    adminId = newAdmin.Id // Thêm adminId vào phản hồi
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // Xóa Tk
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var admin = await _context.admin.FindAsync(id);
            if (admin == null)
            {
                return Json(new { success = false, message = "Không tìm thấy Admin!" });
            }

            // Lấy ID của người dùng hiện tại đang đăng nhập
            var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (currentUserIdClaim == null)
            {
                return Json(new { success = false, message = "Không thể xóa vì tài khoản đang ở trạng thái đăng nhập!" });
            }

            int currentUserId = int.Parse(currentUserIdClaim.Value);

            if (currentUserId == admin.Id)
            {
                return Json(new { success = false, message = "Bạn không thể tự xóa tài khoản của mình khi đang đăng nhập!" });
            }

            // Xóa avatar nếu có và không phải avatar mặc định
            if (!string.IsNullOrEmpty(admin.Avatar) && admin.Avatar != "/avatar/default-avatar.png")
            {
                string avatarPath = Path.Combine("wwwroot", admin.Avatar.TrimStart('/'));
                if (System.IO.File.Exists(avatarPath))
                {
                    System.IO.File.Delete(avatarPath);
                }
            }

            _context.admin.Remove(admin);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Xóa Admin thành công!" });
        }


        //Danh sách tài khoản người dùng
        // Danh sách Users
        [HttpGet]
        [Authorize(Roles = "Admin, Seller")]

        [Route("list-users")]
        public async Task<IActionResult> ListUsers(int page = 1, int pageSize = 5)
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);

            var users = await _context.Users
                .OrderBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserViewModel
                {
                    Id = u.Id,
                    Name = u.Name ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    ContactNo = u.ContactNo ?? string.Empty,
                    RegDate = u.RegDate
                })
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(users);
        }

        // Hàm Hash mật khẩu MD5
        //private static string GetMD5Hash(string input)
        //{
        //    using (var md5 = MD5.Create())
        //    {
        //        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        //        byte[] hashBytes = md5.ComputeHash(inputBytes);
        //        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        //    }
        //}

        public class FetchImageRequest
        {
            public string Url { get; set; } = string.Empty;
        }


    }
}
