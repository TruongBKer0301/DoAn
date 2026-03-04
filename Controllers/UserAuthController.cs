using LapTopBD.Data;
using LapTopBD.Models;
using LapTopBD.Models.ViewModels.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LapTopBD.Controllers
{
    public class UserAuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserAuthController> _logger;

        public UserAuthController(ApplicationDbContext context, ILogger<UserAuthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] UserLoginViewModel model, string returnUrl = "")
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            var user = await _context.Users
                .Where(u => u.Email == model.Email)
                .FirstOrDefaultAsync();

            if (user == null || user.Password != GetMD5Hash(model.Password ?? string.Empty))
            {
                return Json(new { success = false, message = "Sai email hoặc mật khẩu." });
            }

            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Name ?? "User"),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim("UserId", user.Id.ToString())  // Kiểm tra lại claim này
                };


            var claimsIdentity = new ClaimsIdentity(claims, "UserAuth");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync("UserAuth", new ClaimsPrincipal(claimsIdentity), authProperties);

            _logger.LogInformation($"User {user.Email} logged in successfully");

            returnUrl = string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl) ? "/" : returnUrl;
            return Json(new { success = true, message = "Đăng nhập thành công!", userName = user.Name ?? "User", redirectUrl = returnUrl });
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("UserAuth");
            HttpContext.Session.Clear();
            Response.Cookies.Delete("UserAuth");
            return RedirectToAction("Login", "UserAuth");
        }


        // Add this to UserAuthController.cs
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View("Login"); // Using the same view as Login since they toggle
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody] UserRegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            if (existingUser != null)
            {
                return Json(new { success = false, message = "Email đã được sử dụng." });
            }

            var user = new User
            {
                Email = model.Email,
                Name = model.Username,
                ContactNo = model.Phone, // Thêm số điện thoại
                Password = GetMD5Hash(model.Password ?? string.Empty),
                RegDate = DateTime.UtcNow,
                // Các trường khác để mặc định hoặc null vì không bắt buộc
                City = "N/A",
                District = "N/A",
                Ward = "N/A",
                Address = "N/A"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Authentication code...
            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Name ?? "User"),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim("UserId", user.Id.ToString())
                };

            var claimsIdentity = new ClaimsIdentity(claims, "UserAuth");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync("UserAuth", new ClaimsPrincipal(claimsIdentity), authProperties);

            _logger.LogInformation($"User {user.Email} registered and logged in successfully");

            return Json(new
            {
                success = true,
                message = "Đăng ký thành công!",
                userName = user.Name ?? "User",
                redirectUrl = "/"
            });
        }

        private static string GetMD5Hash(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException(nameof(input));
            }

            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }
    }
}