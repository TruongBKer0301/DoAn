namespace LapTopBD.Utilities
{
    public static class SlugHelper
    {
        public static string GenerateSlug(string input)
        {
            if (string.IsNullOrEmpty(input)) return "san-pham";
            return input.ToLower()
                        .Replace("đ", "d")
                        .Replace("Đ", "D")
                        .Replace(" ", "-")
                        .Replace("'", "")
                        .Replace(".", "")
                        .Replace(",", "")
                        .Replace(":", "")
                        .Replace(";", "")
                        .Replace("!", "")
                        .Replace("?", "")
                        .Replace("&", "")
                        .Replace("(", "")
                        .Replace(")", "");
        }
    }
}