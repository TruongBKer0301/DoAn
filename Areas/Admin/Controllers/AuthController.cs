using LapTopBD.Data;
using LapTopBD.Models.ViewModels.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LapTopBD.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] AdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
            }

            // Kiểm tra admin có tồn tại không
            var admin = await _context.admin
                .Where(a => a.Username == model.Username)
                .FirstOrDefaultAsync();

            if (admin == null || admin.Password != GetMD5Hash((model.Password ?? "")))
            {
                return Json(new { success = false, message = "Sai tài khoản hoặc mật khẩu." });
            }

            // Đường dẫn ảnh avatar
            string avatarPath = string.IsNullOrEmpty(admin.Avatar) ? "/avatar/default-avatar.png" : admin.Avatar;

            // Tạo danh sách Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new Claim(ClaimTypes.Name, admin.FullName ?? string.Empty),
                new Claim(ClaimTypes.Role, admin.Roles ?? string.Empty),
                new Claim("Avatar", avatarPath),
                new Claim("AdminId", admin.Id.ToString()),
            };

            var claimsIdentity = new ClaimsIdentity(claims, "AdminAuth");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            };

            // Đăng nhập vào hệ thống
            await HttpContext.SignInAsync("AdminAuth",
                new ClaimsPrincipal(claimsIdentity), authProperties);

            return Json(new { success = true, message = "Đăng nhập thành công!" });
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminAuth");
            HttpContext.Session.Clear();
            Response.Cookies.Delete("AdminAuthCookie"); // Xóa cookie của AdminAuth
            return RedirectToAction("Login", "Auth");
        }

        // Hàm Hash mật khẩu MD5
        private static string GetMD5Hash(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }
    }
}