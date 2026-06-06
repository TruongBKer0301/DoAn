using System.Linq;
using System.Threading.Tasks;
using LapTopBD.Data;
using LapTopBD.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LapTopBD.Controllers
{
    [Route("api/internal/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public class ProductRequest
        {
            public string message { get; set; } = string.Empty;
            public List<ConversationItem> conversation { get; set; } = new();
        }
        public class ConversationItem
        {
            public string role { get; set; } = string.Empty;
            public string content { get; set; } = string.Empty;
        }
        public class ProductDto
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string CategoryName { get; set; } = string.Empty;
            public string Brand { get; set; } = string.Empty;
            public string CPU { get; set; } = string.Empty;
            public string RAM { get; set; } = string.Empty;
            public string Storage { get; set; } = string.Empty;
            public decimal ProductPrice { get; set; }
            public string ProductDescription { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string ProductImage { get; set; } = string.Empty;
        }

        public ProductsController(ApplicationDbContext db) => _db = db;

        // ── Từ khóa nhận biết intent xem chi tiết ────────────────────────────────
        private static readonly string[] DetailKeywords = new[]
        {
            "chi tiết", "thông tin", "thông số", "spec", "cấu hình",
            "xem thêm", "giới thiệu", "mô tả", "tính năng", "đặc điểm",
            "giá của", "giá bao nhiêu", "cho biết", "review"
        };

        private static bool IsDetailIntent(string msg) =>
            DetailKeywords.Any((msg ?? string.Empty).ToLowerInvariant().Contains);

        // ── Tách các "từ có nghĩa" từ message để tìm theo tên sản phẩm ───────────
        // Lọc bỏ stop-words tiếng Việt và các từ khóa detail thừa
        private static readonly string[] StopWords = new[]
        {
            "chi tiết", "thông tin", "thông số", "spec", "cấu hình",
            "xem thêm", "giới thiệu", "mô tả", "tính năng", "đặc điểm",
            "giá của", "giá bao nhiêu", "cho biết", "review",
            "của", "bạn", "cho", "tôi", "biết", "về", "sản phẩm",
            "là", "gì", "có", "không", "thế", "nào", "như", "vậy",
            "ơi", "ai", "ạ", "nhé", "nha", "với"
        };

        /// Trích từ khóa sản phẩm từ message sau khi bỏ stop-words và detail-words.
        /// Trả về danh sách token để dùng tìm theo tên.
        private static List<string> ExtractProductTokens(string message)
        {
            var lower = (message ?? string.Empty).ToLowerInvariant();

            // Xóa stop-words (dài trước, ngắn sau để tránh cắt nhầm)
            foreach (var sw in StopWords.OrderByDescending(s => s.Length))
                lower = lower.Replace(sw, " ");

            return lower.Split(new[] { ' ', ',', '.', '?', '!', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(t => t.Length >= 2)
                        .Distinct()
                        .ToList();
        }

        /// Trả về tên CategoryName đúng trong DB dựa vào message.
        /// Chạy phía C# — KHÔNG được dùng bên trong EF query.
        private static string DetectCategory(string message)
        {
            var m = (message ?? string.Empty).ToLowerInvariant();

            if (m.Contains("sạc") || m.Contains("sac")
                || m.Contains("charger") || m.Contains("adapter")
                || m.Contains("cáp") || m.Contains("cap")
                || m.Contains("cable")
                || m.Contains("pin sạc") || m.Contains("pin sac")
                || m.Contains("cus"))
                return "Sạc";

            if (m.Contains("bàn phím") || m.Contains("ban phim")
                || m.Contains("keyboard") || m.Contains("kb")
                || m.Contains("mechanical")
                || m.Contains("keycap")
                || m.Contains("switch"))
                return "Bàn phím cơ";

            if (m.Contains("tai nghe") || m.Contains("tai nghe bluetooth")
                || m.Contains("headphone") || m.Contains("headset")
                || m.Contains("earbuds") || m.Contains("earbud")
                || m.Contains("airpods")
                || m.Contains("tws"))
                return "Tai nghe";

            if (m.Contains("điện thoại") || m.Contains("dien thoai")
                || m.Contains("phone") || m.Contains("dt")
                || m.Contains("smartphone")
                || m.Contains("iphone")
                || m.Contains("samsung")
                || m.Contains("xiaomi")
                || m.Contains("oppo")
                || m.Contains("realme")
                || m.Contains("vivo")
                || m.Contains("pixel")
                || m.Contains("redmi"))
                return "Điện thoại";

            if (m.Contains("laptop") || m.Contains("lap top")
                || m.Contains("laptop gaming")
                || m.Contains("notebook")
                || m.Contains("máy tính") || m.Contains("may tinh")
                || m.Contains("mt")
                || m.Contains("macbook")
                || m.Contains("thinkpad")
                || m.Contains("legion")
                || m.Contains("rog")
                || m.Contains("vivobook")
                || m.Contains("zenbook")
                || m.Contains("dell")
                || m.Contains("lenovo")
                || m.Contains("asus")
                || m.Contains("acer")
                || m.Contains("hp"))
                return "Laptop";

            return string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ProductRequest req)
        {
            if (req == null) return BadRequest("Missing request");

            // ── NEW: nếu là detail intent → tìm theo tên sản phẩm ───────────────
            if (IsDetailIntent(req.message))
            {
                var tokens = ExtractProductTokens(req.message);

                if (tokens.Count > 0)
                {
                    // Lấy toàn bộ sản phẩm về C# rồi lọc (tránh lỗi EF translation với Contains vòng lặp)
                    var allProducts = await _db.Product
                        .AsNoTracking()
                        .Include(p => p.Category)
                        .Select(p => new
                        {
                            p.Id,
                            p.ProductName,
                            CategoryName  = p.Category != null ? p.Category.CategoryName : string.Empty,
                            p.Brand,
                            p.CPU,
                            p.RAM,
                            p.Storage,
                            p.ProductPrice,
                            p.ProductDescription,
                            p.Slug,
                            p.ProductImage1,
                            p.ProductImage2,
                            p.ProductImage3
                        })
                        .ToListAsync();

                    // Tính điểm khớp: số token xuất hiện trong ProductName (không phân biệt hoa thường)
                    var scored = allProducts
                        .Select(p => new
                        {
                            Product = p,
                            Score   = tokens.Count(t =>
                                (p.ProductName ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                (p.Brand       ?? string.Empty).Contains(t, StringComparison.OrdinalIgnoreCase))
                        })
                        .Where(x => x.Score > 0)
                        .OrderByDescending(x => x.Score)
                        .Take(3)   // trả tối đa 3 sản phẩm khớp nhất
                        .ToList();

                    if (scored.Count > 0)
                    {
                        var detailDto = scored.Select(x => new ProductDto
                        {
                            ProductId          = x.Product.Id,
                            ProductName        = x.Product.ProductName        ?? string.Empty,
                            CategoryName       = x.Product.CategoryName       ?? string.Empty,
                            Brand              = x.Product.Brand              ?? string.Empty,
                            CPU                = x.Product.CPU                ?? string.Empty,
                            RAM                = x.Product.RAM                ?? string.Empty,
                            Storage            = x.Product.Storage            ?? string.Empty,
                            ProductPrice       = x.Product.ProductPrice,
                            ProductDescription = Truncate(SanitizeInline(x.Product.ProductDescription ?? string.Empty), 500),
                            Slug               = string.IsNullOrWhiteSpace(x.Product.Slug)
                                                    ? SlugHelper.GenerateSlug(x.Product.ProductName ?? string.Empty)
                                                    : x.Product.Slug,
                            ProductImage       = !string.IsNullOrWhiteSpace(x.Product.ProductImage1) ? x.Product.ProductImage1
                                                    : !string.IsNullOrWhiteSpace(x.Product.ProductImage2) ? x.Product.ProductImage2
                                                    : (x.Product.ProductImage3 ?? string.Empty)
                        }).ToList();

                        return Ok(detailDto);
                    }
                    // Nếu không khớp tên → fallback xuống logic gợi ý theo danh mục bên dưới
                }
            }

            // ── Logic gợi ý theo danh mục (giữ nguyên) ──────────────────────────
            var category = DetectCategory(req.message);

            var raw = await _db.Product
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p =>
                    category == string.Empty
                    || (p.Category != null && p.Category.CategoryName == category))
                .Select(p => new
                {
                    p.Id,
                    p.ProductName,
                    CategoryName = p.Category != null ? p.Category.CategoryName : string.Empty,
                    p.Brand,
                    p.CPU,
                    p.RAM,
                    p.Storage,
                    p.ProductPrice,
                    p.ProductDescription,
                    p.Slug,
                    p.ProductImage1,
                    p.ProductImage2,
                    p.ProductImage3,
                    HasImage = p.ProductImage1 != null && p.ProductImage1 != ""
                })
                .OrderByDescending(p => p.HasImage)
                .ThenByDescending(p => p.Id)
                .Take(12)
                .ToListAsync();

            var dto = raw.Select(p => new ProductDto
            {
                ProductId          = p.Id,
                ProductName        = p.ProductName        ?? string.Empty,
                CategoryName       = p.CategoryName       ?? string.Empty,
                Brand              = p.Brand              ?? string.Empty,
                CPU                = p.CPU                ?? string.Empty,
                RAM                = p.RAM                ?? string.Empty,
                Storage            = p.Storage            ?? string.Empty,
                ProductPrice       = p.ProductPrice,
                ProductDescription = Truncate(SanitizeInline(p.ProductDescription ?? string.Empty), 300),
                Slug               = string.IsNullOrWhiteSpace(p.Slug)
                                        ? SlugHelper.GenerateSlug(p.ProductName ?? string.Empty)
                                        : p.Slug,
                ProductImage       = !string.IsNullOrWhiteSpace(p.ProductImage1) ? p.ProductImage1
                                        : !string.IsNullOrWhiteSpace(p.ProductImage2) ? p.ProductImage2
                                        : (p.ProductImage3 ?? string.Empty)
            }).ToList();

            return Ok(dto);
        }

        private static string SanitizeInline(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace('\n', ' ').Replace('\r', ' ').Trim();

        private static string Truncate(string v, int max) =>
            string.IsNullOrWhiteSpace(v) ? string.Empty
            : v.Length <= max ? v
            : v[..(max - 1)].TrimEnd() + "…";
    }
}
