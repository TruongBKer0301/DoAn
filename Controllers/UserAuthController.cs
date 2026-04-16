using LapTopBD.Data;
using LapTopBD.Models;
using LapTopBD.Models.ViewModels.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LapTopBD.Utilities;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Net.Mail;

namespace LapTopBD.Controllers
{
    public class UserAuthController : Controller
    {
        private const int OtpLifetimeMinutes = 1;
        private const int OtpResendCooldownSeconds = 60;
        private static readonly Dictionary<string, string> CommonEmailDomainTypos = new(StringComparer.OrdinalIgnoreCase)
        {
            ["gmal.com"] = "gmail.com",
            ["gmial.com"] = "gmail.com",
            ["gmail.con"] = "gmail.com",
            ["gmail.co"] = "gmail.com",
            ["hotmail.co"] = "hotmail.com",
            ["hotnail.com"] = "hotmail.com",
            ["yaho.com"] = "yahoo.com",
            ["yahooo.com"] = "yahoo.com",
            ["outlok.com"] = "outlook.com",
            ["outllok.com"] = "outlook.com"
        };

        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserAuthController> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _environment;

        public UserAuthController(
            ApplicationDbContext context,
            ILogger<UserAuthController> logger,
            IEmailSender emailSender,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _emailSender = emailSender;
            _environment = environment;
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

            var loginEmail = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!TryValidateEmail(loginEmail, out var loginEmailError))
            {
                return Json(new { success = false, message = loginEmailError });
            }

            var user = await _context.Users
                .Where(u => u.Email == loginEmail)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return Json(new { success = false, message = "Tài khoản không tồn tại." });
            }

            if (user.Password != GetMD5Hash(model.Password ?? string.Empty))
            {
                return Json(new { success = false, message = "Mật khẩu không đúng." });
            }

            if (!user.IsEmailVerified)
            {
                return Json(new
                {
                    success = false,
                    requiresVerification = true,
                    email = user.Email,
                    message = "Email của bạn chưa được xác thực. Vui lòng nhập OTP để hoàn tất đăng ký."
                });
            }

            await SignInUserAsync(user, model.RememberMe);

            _logger.LogInformation("User {Email} logged in successfully", user.Email);

            returnUrl = string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl) ? "/" : returnUrl;
            return Json(new { success = true, message = "Đăng nhập thành công!", userName = user.Name ?? "User", redirectUrl = returnUrl });
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody] UserRegisterViewModel model)
        {
            if (model == null)
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            var email = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            var username = model.Username?.Trim() ?? string.Empty;
            var phone = model.Phone?.Trim() ?? string.Empty;
            var password = model.Password?.Trim() ?? string.Empty;
            var confirmPassword = model.Password2?.Trim() ?? string.Empty;

            if (!TryValidateEmail(email, out var registerEmailError))
            {
                return Json(new { success = false, message = registerEmailError });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return Json(new { success = false, message = "Vui lòng nhập họ tên." });
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                return Json(new { success = false, message = "Vui lòng nhập số điện thoại." });
            }

            if (!IsStrongPassword(password))
            {
                return Json(new { success = false, message = "Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường và số." });
            }

            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
            {
                return Json(new { success = false, message = "Mật khẩu xác nhận không khớp." });
            }

            var otp = GenerateOtp();
            var otpExpiry = DateTimeHelper.Now.AddMinutes(OtpLifetimeMinutes);
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (existingUser != null)
            {
                return Json(new { success = false, message = "Email đã được sử dụng." });
            }

            var user = new User
            {
                Email = email,
                Name = username,
                ContactNo = phone,
                Password = GetMD5Hash(password),
                RegDate = DateTimeHelper.Now,
                IsEmailVerified = false,
                EmailVerificationOtp = otp,
                EmailVerificationOtpExpiry = otpExpiry,
                City = string.Empty,
                District = string.Empty,
                Ward = string.Empty,
                Address = string.Empty
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            var mailResult = await _emailSender.SendAsync(
                email,
                "Ma OTP xac thuc tai khoan LapTopBD",
                BuildOtpMailBody(username, otp, "xac thuc tai khoan"));

            if (!mailResult.Success)
            {
                return Json(new
                {
                    success = false,
                    message = $"Không thể gửi OTP: {mailResult.ErrorMessage ?? "lỗi SMTP không xác định"}"
                });
            }

            return Json(new
            {
                success = true,
                requiresVerification = true,
                email,
                cooldownSeconds = OtpResendCooldownSeconds,
                message = "Đăng ký thành công. Mã OTP xác thực đã được gửi về email của bạn."
            });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyEmailOtp([FromBody] VerifyEmailOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            var email = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            var otp = model.Otp?.Trim() ?? string.Empty;

            if (!TryValidateEmail(email, out var verifyEmailError))
            {
                return Json(new { success = false, message = verifyEmailError });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return Json(new { success = false, message = "Tài khoản không tồn tại." });
            }

            if (user.IsEmailVerified)
            {
                await SignInUserAsync(user, true);
                return Json(new { success = true, message = "Email đã được xác thực trước đó.", redirectUrl = "/" });
            }

            if (string.IsNullOrWhiteSpace(user.EmailVerificationOtp)
                || user.EmailVerificationOtp != otp
                || !user.EmailVerificationOtpExpiry.HasValue
                || user.EmailVerificationOtpExpiry.Value < DateTimeHelper.Now)
            {
                return Json(new { success = false, message = "Mã OTP không đúng hoặc đã hết hạn. Vui lòng kiểm tra lại." });
            }

            user.IsEmailVerified = true;
            user.EmailVerificationOtp = null;
            user.EmailVerificationOtpExpiry = null;
            user.UpdationDate = DateTimeHelper.Now;

            await _context.SaveChangesAsync();
            await SignInUserAsync(user, true);

            return Json(new { success = true, message = "Xác thực email thành công!", redirectUrl = "/" });
        }

        [HttpPost]
        public async Task<IActionResult> ResendVerificationOtp([FromBody] ForgotPasswordRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Vui lòng nhập email hợp lệ." });
            }

            var email = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!TryValidateEmail(email, out var resendEmailError))
            {
                return Json(new { success = false, message = resendEmailError });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return Json(new { success = false, message = "Email chưa được đăng ký." });
            }

            if (user.IsEmailVerified)
            {
                return Json(new { success = false, message = "Email này đã xác thực rồi." });
            }

            var now = DateTimeHelper.Now;
            var verifyCooldownRemaining = GetRemainingCooldownSeconds(user.EmailVerificationOtpExpiry, now);
            if (verifyCooldownRemaining > 0)
            {
                return Json(new
                {
                    success = false,
                    cooldownSeconds = verifyCooldownRemaining,
                    message = $"Bạn vừa yêu cầu OTP. Vui lòng thử lại sau {verifyCooldownRemaining} giây."
                });
            }

            var otp = GenerateOtp();
            user.EmailVerificationOtp = otp;
            user.EmailVerificationOtpExpiry = now.AddMinutes(OtpLifetimeMinutes);
            user.UpdationDate = now;
            await _context.SaveChangesAsync();

            var mailResult = await _emailSender.SendAsync(
                email,
                "Mã OTP xác thực tài khoản LapTopBD",
                BuildOtpMailBody(user.Name ?? "ban", otp, "xác thực tài khoản"));

            if (!mailResult.Success)
            {
                return Json(new { success = false, message = $"Không thể gửi lại OTP: {mailResult.ErrorMessage ?? "lỗi SMTP không xác định"}" });
            }

            return Json(new
            {
                success = true,
                cooldownSeconds = OtpResendCooldownSeconds,
                message = "Đã gửi lại mã OTP xác thực vào email của bạn."
            });
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPasswordRequest([FromBody] ForgotPasswordRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Vui lòng nhập email hợp lệ." });
            }

            var email = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!TryValidateEmail(email, out var forgotEmailError))
            {
                return Json(new { success = false, message = forgotEmailError });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Email chưa được đăng ký."
                });
            }

            string? otpPreview = null;

            if (user != null)
            {
                var now = DateTimeHelper.Now;
                var resetCooldownRemaining = GetRemainingCooldownSeconds(user.PasswordResetOtpExpiry, now);
                if (resetCooldownRemaining > 0)
                {
                    return Json(new
                    {
                        success = false,
                        cooldownSeconds = resetCooldownRemaining,
                        message = $"Bạn vừa yêu cầu OTP đặt lại mật khẩu. Vui lòng thử lại sau {resetCooldownRemaining} giây."
                    });
                }

                var otp = GenerateOtp();
                user.PasswordResetOtp = otp;
                user.PasswordResetOtpExpiry = now.AddMinutes(OtpLifetimeMinutes);
                user.UpdationDate = now;
                await _context.SaveChangesAsync();
                otpPreview = _environment.IsDevelopment() ? otp : null;

                var mailResult = await _emailSender.SendAsync(
                    email,
                    "Mã OTP đặt lại mật khẩu LapTopBD",
                    BuildOtpMailBody(user.Name ?? "ban", otp, "đặt lại mật khẩu"));

                if (!mailResult.Success)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Không thể gửi OTP quên mật khẩu: {mailResult.ErrorMessage ?? "lỗi SMTP không xác định"}"
                    });
                }
            }

            return Json(new
            {
                success = true,
                cooldownSeconds = OtpResendCooldownSeconds,
                message = "OTP đặt lại mật khẩu đã được gửi về email của bạn.",
                email,
                otpPreview
            });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyForgotPasswordOtp([FromBody] VerifyEmailOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            var email = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            var otp = model.Otp?.Trim() ?? string.Empty;

            if (!TryValidateEmail(email, out var verifyEmailError))
            {
                return Json(new { success = false, message = verifyEmailError });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return Json(new { success = false, message = "Tài khoản không tồn tại." });
            }

            if (string.IsNullOrWhiteSpace(user.PasswordResetOtp)
                || user.PasswordResetOtp != otp
                || !user.PasswordResetOtpExpiry.HasValue
                || user.PasswordResetOtpExpiry.Value < DateTimeHelper.Now)
            {
                return Json(new { success = false, message = "Mã OTP không đúng hoặc đã hết hạn." });
            }

            return Json(new
            {
                success = true,
                message = "Xác thực OTP thành công. Đang chuyển đến trang tạo mật khẩu mới.",
                redirectUrl = Url.Action("ResetPassword", "UserAuth", new { email, otp })
                    ?? $"/UserAuth/ResetPassword?email={Uri.EscapeDataString(email)}&otp={Uri.EscapeDataString(otp)}"
            });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPasswordWithOtp([FromBody] ResetPasswordWithOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            var email = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            var otp = model.Otp?.Trim() ?? string.Empty;

            if (!TryValidateEmail(email, out var resetEmailError))
            {
                return Json(new { success = false, message = resetEmailError });
            }

            if (!IsStrongPassword(model.NewPassword ?? string.Empty))
            {
                return Json(new { success = false, message = "Mật khẩu mới phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường và số." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return Json(new { success = false, message = "Tài khoản không tồn tại." });
            }

            if (string.IsNullOrWhiteSpace(user.PasswordResetOtp)
                || user.PasswordResetOtp != otp
                || !user.PasswordResetOtpExpiry.HasValue
                || user.PasswordResetOtpExpiry.Value < DateTimeHelper.Now)
            {
                return Json(new { success = false, message = "Mã OTP không đúng hoặc đã hết hạn." });
            }

            user.Password = GetMD5Hash(model.NewPassword ?? string.Empty);
            user.PasswordResetOtp = null;
            user.PasswordResetOtpExpiry = null;
            user.UpdationDate = DateTimeHelper.Now;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại." });
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("UserAuth");
            HttpContext.Session.Clear();
            Response.Cookies.Delete("UserAuth");
            return RedirectToAction("Login", "UserAuth");
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View("Login");
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string email = "", string otp = "")
        {
            ViewBag.ShowBanner = false;

            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (!TryValidateEmail(normalizedEmail, out _))
            {
                return RedirectToAction("Login");
            }

            var normalizedOtp = otp?.Trim() ?? string.Empty;
            if (normalizedOtp.Length != 6)
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
            if (user == null
                || string.IsNullOrWhiteSpace(user.PasswordResetOtp)
                || user.PasswordResetOtp != normalizedOtp
                || !user.PasswordResetOtpExpiry.HasValue
                || user.PasswordResetOtpExpiry.Value < DateTimeHelper.Now)
            {
                return RedirectToAction("Login");
            }

            ViewBag.ResetEmail = normalizedEmail;
            ViewBag.ResetOtp = normalizedOtp;
            return View();
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

        private async Task SignInUserAsync(User user, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name ?? "User"),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim("UserId", user.Id.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, "UserAuth");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync("UserAuth", new ClaimsPrincipal(claimsIdentity), authProperties);
        }

        private static string GenerateOtp()
        {
            return RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
        }

        private static string BuildOtpMailBody(string userName, string otp, string purpose)
        {
            return $@"
                <div style='font-family:Arial,sans-serif;line-height:1.6'>
                    <h2>LapTopBD - Mã OTP</h2>
                    <p>Xin chào {userName},</p>
                    <p>Bạn vừa yêu cầu {purpose}.</p>
                    <p>Mã OTP của bạn là:</p>
                    <p style='font-size:28px;font-weight:700;letter-spacing:4px'>{otp}</p>
                    <p>Mã có hiệu lực trong {OtpLifetimeMinutes} phút.</p>
                    <p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.</p>
                </div>";
        }

        private static int GetRemainingCooldownSeconds(DateTime? otpExpiry, DateTime now)
        {
            if (!otpExpiry.HasValue)
            {
                return 0;
            }

            var sentAt = otpExpiry.Value.AddMinutes(-OtpLifetimeMinutes);
            var nextAllowedAt = sentAt.AddSeconds(OtpResendCooldownSeconds);
            var remaining = (int)Math.Ceiling((nextAllowedAt - now).TotalSeconds);
            return remaining > 0 ? remaining : 0;
        }

        private static bool IsStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                return false;
            }

            var hasUpper = password.Any(char.IsUpper);
            var hasLower = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);
            return hasUpper && hasLower && hasDigit;
        }

        private static bool TryValidateEmail(string email, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(email))
            {
                errorMessage = "Vui lòng nhập email.";
                return false;
            }

            try
            {
                var addr = new MailAddress(email);
                if (!string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "Email không hợp lệ.";
                    return false;
                }
            }
            catch
            {
                errorMessage = "Email không hợp lệ.";
                return false;
            }

            var atIndex = email.LastIndexOf('@');
            if (atIndex <= 0 || atIndex >= email.Length - 1)
            {
                errorMessage = "Email không hợp lệ.";
                return false;
            }

            var localPart = email[..atIndex];
            var domain = email[(atIndex + 1)..];
            if (CommonEmailDomainTypos.TryGetValue(domain, out var correctedDomain))
            {
                errorMessage = $"Bạn có nhập sai email không? Gợi ý: {localPart}@{correctedDomain}";
                return false;
            }

            return true;
        }
    }
}