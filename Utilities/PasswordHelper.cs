using BCrypt.Net;

namespace LapTopBD.Utilities
{
    public static class PasswordHelper
    {
        // Hàm này dùng khi Đăng ký hoặc Đổi mật khẩu
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        }

        public static bool VerifyPassword(string? password, string? baseHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(baseHash))
            {
                return false;
            }

            return BCrypt.Net.BCrypt.Verify(password, baseHash);
        }
    }
}
