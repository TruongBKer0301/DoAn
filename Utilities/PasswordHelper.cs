using BCrypt.Net;
using System.Security.Cryptography;
using System.Text;

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

            // Support both BCrypt and legacy MD5/plaintext passwords.
            if (baseHash.StartsWith("$2", StringComparison.Ordinal))
            {
                try
                {
                    return BCrypt.Net.BCrypt.Verify(password, baseHash);
                }
                catch (SaltParseException)
                {
                    return false;
                }
            }

            var md5 = ComputeMd5Hex(password);
            if (baseHash.Length == 32 && string.Equals(md5, baseHash, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(password, baseHash, StringComparison.Ordinal);
        }

        private static string ComputeMd5Hex(string input)
        {
            using var md5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = md5.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
