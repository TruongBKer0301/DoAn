using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LapTopBD.Data;
using LapTopBD.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace LapTopBD.Controllers
{
    [Route("api/ai/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<ChatController> _logger;

        public ChatController(ApplicationDbContext db, IHttpClientFactory httpFactory, IConfiguration config, ILogger<ChatController> logger)
        {
            _db = db;
            _httpFactory = httpFactory;
            _config = config;
            _logger = logger;
        }

        public class ChatRequest { public string message { get; set; } public List<ConversationItem> conversation { get; set; } }
        public class ConversationItem { public string role { get; set; } public string content { get; set; } }
        public class ChatResponse { public string reply { get; set; } }
        private sealed class ChatProduct
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public string CategoryName { get; set; }
            public string Brand { get; set; }
            public string Cpu { get; set; }
            public string Ram { get; set; }
            public string Storage { get; set; }
            public decimal ProductPrice { get; set; }
            public string ProductDescription { get; set; }
            public string Slug { get; set; }
            public string ProductImage { get; set; }
        }

        private static readonly string[] ModelFallbacks = new[]
        {
            "llama-3.3-70b-versatile",
            "llama-3.1-8b-instant",
            "llama-3.1-70b-versatile"
        };

        private static readonly string[] LaptopKeywords = new[]
        {
            "laptop", "lap top", "notebook", "máy tính xách tay", "gaming", "ultrabook", "workstation", "thinkpad", "legion", "rog", "vivobook", "zenbook", "macbook", "inspiron", "latitude", "xps", "dell", "lenovo", "asus", "acer"
        };

        private static bool IsLaptopIntent(string message)
        {
            var normalized = (message ?? string.Empty).ToLowerInvariant();
            return normalized.Contains("laptop")
                   || normalized.Contains("máy tính xách tay")
                   || normalized.Contains("notebook")
                   || normalized.Contains("máy tính")
                   || LaptopKeywords.Any(normalized.Contains);
        }

        private static bool IsProductIntent(string message)
        {
            var normalized = (message ?? string.Empty).ToLowerInvariant();
            // broader product-related intents: mua, giá, sản phẩm, gợi ý, tìm, cần
            var keywords = new[] { "mua", "giá", "sản phẩm", "sản phẩm nào", "gợi ý", "tư vấn", "tìm", "cần", "muốn mua", "show", "catalog", "shop" };
            return keywords.Any(k => normalized.Contains(k));
        }

        private static int? ParseRequestedRam(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return null;
            var m = Regex.Match(message.ToLowerInvariant(), @"\b(\d{1,3})\s*(gb|g)\b");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var val)) return val;
            return null;
        }

        private static bool IsPhoneIntent(string message)
        {
            var normalized = (message ?? string.Empty).ToLowerInvariant();
            var phoneKeywords = new[] { "điện thoại", "phone", "samsung", "iphone", "xiaomi", "camera", "pixel", "oppo", "realme", "vivo" };
            return phoneKeywords.Any(normalized.Contains);
        }

        private static readonly string[] AccessoryKeywords = new[] { "sạc", "sạc ô tô", "sạc ôtô", "charger", "car charger", "ô tô", "oto", "ôto", "car" };

        private static bool ConversationIndicatesPhone(List<ConversationItem> conversation)
        {
            if (conversation == null) return false;
            foreach (var item in conversation)
            {
                if (IsPhoneIntent(item?.content)) return true;
            }
            return false;
        }

        private static bool IsColorAdviceIntent(string message)
        {
            var normalized = (message ?? string.Empty).ToLowerInvariant();
            return normalized.Contains("mệnh")
                   || normalized.Contains("phong thủy")
                   || normalized.Contains("hợp màu")
                   || normalized.Contains("màu nào")
                   || normalized.Contains("màu gì")
                   || normalized.Contains("màu điện thoại")
                   || normalized.Contains("màu phone");
        }

        private static string BuildPhoneColorAdvice(string message)
        {
            var normalized = (message ?? string.Empty).ToLowerInvariant();
            string mệnh = null;

            if (normalized.Contains("kim")) mệnh = "kim";
            else if (normalized.Contains("mộc")) mệnh = "mộc";
            else if (normalized.Contains("thủy") || normalized.Contains("thuy")) mệnh = "thủy";
            else if (normalized.Contains("hỏa") || normalized.Contains("hoa")) mệnh = "hỏa";
            else if (normalized.Contains("thổ") || normalized.Contains("tho")) mệnh = "thổ";

            return mệnh switch
            {
                "kim" => "Nếu bạn mệnh Kim, nên chọn điện thoại màu trắng, bạc, xám, vàng ánh kim. Hạn chế các màu đỏ, hồng, tím.\nNếu muốn, tôi có thể gợi ý luôn mẫu điện thoại phù hợp với mệnh Kim.",
                "mộc" => "Nếu bạn mệnh Mộc, nên chọn điện thoại màu xanh lá, xanh rêu, xanh dương hoặc đen. Hạn chế màu trắng, bạc.\nNếu muốn, tôi có thể gợi ý luôn mẫu điện thoại phù hợp với mệnh Mộc.",
                "thủy" => "Nếu bạn mệnh Thủy, nên chọn điện thoại màu đen, xanh dương, xanh navy. Hạn chế màu vàng, nâu đất.\nNếu muốn, tôi có thể gợi ý luôn mẫu điện thoại phù hợp với mệnh Thủy.",
                "hỏa" => "Nếu bạn mệnh Hỏa, nên chọn điện thoại màu đỏ, cam, hồng, tím. Hạn chế màu đen, xanh nước biển.\nNếu muốn, tôi có thể gợi ý luôn mẫu điện thoại phù hợp với mệnh Hỏa.",
                "thổ" => "Nếu bạn mệnh Thổ, nên chọn điện thoại màu vàng, nâu đất, cam đất. Hạn chế màu xanh lá.\nNếu muốn, tôi có thể gợi ý luôn mẫu điện thoại phù hợp với mệnh Thổ.",
                _ => "Bạn cho tôi biết mệnh của bạn (Kim, Mộc, Thủy, Hỏa, Thổ), tôi sẽ gợi ý ngay màu điện thoại hợp mệnh. Ví dụ: mệnh Kim hợp trắng/bạc/xám; mệnh Mộc hợp xanh lá/xanh dương; mệnh Thủy hợp đen/xanh navy; mệnh Hỏa hợp đỏ/cam/tím; mệnh Thổ hợp vàng/nâu đất."
            };
        }

        private static List<ChatProduct> SelectRelevantProducts(List<ChatProduct> products, string message)
        {
            if (!IsLaptopIntent(message))
            {
                return products;
            }

            var laptopMatches = products
                .Where(p =>
                    (p.CategoryName ?? string.Empty).Contains("laptop", StringComparison.OrdinalIgnoreCase) ||
                    (p.ProductName ?? string.Empty).Contains("laptop", StringComparison.OrdinalIgnoreCase) ||
                    (p.ProductName ?? string.Empty).Contains("notebook", StringComparison.OrdinalIgnoreCase) ||
                    (p.ProductDescription ?? string.Empty).Contains("laptop", StringComparison.OrdinalIgnoreCase) ||
                    (p.ProductDescription ?? string.Empty).Contains("gaming", StringComparison.OrdinalIgnoreCase) ||
                    (p.ProductDescription ?? string.Empty).Contains("macbook", StringComparison.OrdinalIgnoreCase) ||
                    LaptopKeywords.Any(k => (p.ProductName ?? string.Empty).Contains(k, StringComparison.OrdinalIgnoreCase) || (p.ProductDescription ?? string.Empty).Contains(k, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return laptopMatches.Count > 0 ? laptopMatches : products;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.message)) return BadRequest("Missing message");

            var products = new List<ChatProduct>();
            var isLaptopIntent = IsLaptopIntent(req.message);

            var sb = new StringBuilder();
            sb.AppendLine("Bạn là nhân viên bán hàng thân thiện của shop laptop. Trả lời bằng tiếng Việt ngắn gọn, tư vấn đúng nhu cầu, chủ động gợi ý 2-3 sản phẩm phù hợp. Mỗi sản phẩm phải xuống dòng riêng theo mẫu: - Tên — Giá — Mua ngay: /Cart/Checkout?selectedProductIds=id");

            try
            {
                var productQuery = _db.Product
                    .AsNoTracking()
                    .Include(p => p.Category)
                    .OrderByDescending(p => p.Id)
                    .AsQueryable();

                // If the user asked for a RAM size (eg. "16gb"), prefer laptop results and filter by RAM
                int? requestedRam = ParseRequestedRam(req.message);

                // Determine whether prior conversation indicates phone intent. If so, prefer phone results instead.
                var convIndicatesPhone = ConversationIndicatesPhone(req.conversation);
                var preferPhone = IsPhoneIntent(req.message) || convIndicatesPhone;
                var preferLaptop = !preferPhone && (isLaptopIntent || requestedRam != null);

                if (preferLaptop)
                {
                    productQuery = productQuery.Where(p =>
                        (p.Category != null && p.Category.CategoryName != null && p.Category.CategoryName.Contains("Laptop")) ||
                        (p.ProductName != null && (p.ProductName.Contains("laptop") || p.ProductName.Contains("notebook"))) ||
                        (p.ProductDescription != null && (p.ProductDescription.Contains("laptop") || p.ProductDescription.Contains("notebook"))) ||
                        (requestedRam != null && p.RAM != null && p.RAM.Contains(requestedRam.ToString())) ||
                        (requestedRam != null && p.ProductName != null && p.ProductName.Contains(requestedRam.ToString()))
                    );
                }
                else if (preferPhone)
                {
                    // Prefer phone/category phone-like products when conversation indicates phones
                    productQuery = productQuery.Where(p =>
                        (p.Category != null && p.Category.CategoryName != null && p.Category.CategoryName.ToLower().Contains("điện thoại")) ||
                        (p.ProductName != null && (p.ProductName.ToLower().Contains("điện thoại") || p.ProductName.ToLower().Contains("iphone") || p.ProductName.ToLower().Contains("samsung") || p.ProductName.ToLower().Contains("xiaomi"))) ||
                        (p.ProductDescription != null && p.ProductDescription.ToLower().Contains("điện thoại")) ||
                        (requestedRam != null && p.RAM != null && p.RAM.Contains(requestedRam.ToString())) ||
                        (requestedRam != null && p.ProductName != null && p.ProductName.Contains(requestedRam.ToString()))
                    );
                }

                // If user queries accessories like car chargers, narrow search to accessory-related products
                var msgLower = (req.message ?? string.Empty).ToLowerInvariant();
                // If user explicitly asks for "all products" or similar, skip accessory filtering.
                var allKeywords = new[] { "tất cả", "tất cả sản phẩm", "mọi", "toàn bộ", "toàn bộ sản phẩm", "all products", "show all", "show all products", "all" };
                var wantsAll = allKeywords.Any(k => msgLower.Contains(k));

                if (!wantsAll && AccessoryKeywords.Any(k => msgLower.Contains(k)))
                {
                    productQuery = productQuery.Where(p =>
                        (p.ProductName != null && (
                            (p.ProductName.ToLower().Contains("sạc")) ||
                            (p.ProductName.ToLower().Contains("charger")) ||
                            (p.ProductName.ToLower().Contains("car")) ||
                            (p.ProductName.ToLower().Contains("ô tô")) ||
                            (p.ProductName.ToLower().Contains("oto"))
                        ))
                        || (p.ProductDescription != null && (
                            p.ProductDescription.ToLower().Contains("sạc") ||
                            p.ProductDescription.ToLower().Contains("charger") ||
                            p.ProductDescription.ToLower().Contains("car")
                        ))
                        || (p.Category != null && p.Category.CategoryName != null && (
                            p.Category.CategoryName.ToLower().Contains("sạc") ||
                            p.Category.CategoryName.ToLower().Contains("phụ kiện") ||
                            p.Category.CategoryName.ToLower().Contains("charger") ||
                            p.Category.CategoryName.ToLower().Contains("ô tô") ||
                            p.Category.CategoryName.ToLower().Contains("oto")
                        ))
                    );
                }

                var rawProducts = await productQuery
                    .Take(8)
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
                        p.ProductImage3
                    })
                    .ToListAsync();

                products = rawProducts
                    .Select(p => new ChatProduct
                    {
                        ProductId = p.Id,
                        ProductName = p.ProductName,
                        CategoryName = p.CategoryName,
                        Brand = p.Brand,
                        Cpu = p.CPU,
                        Ram = p.RAM,
                        Storage = p.Storage,
                        ProductPrice = p.ProductPrice,
                        ProductDescription = p.ProductDescription,
                        Slug = string.IsNullOrWhiteSpace(p.Slug)
                            ? SlugHelper.GenerateSlug(p.ProductName ?? string.Empty)
                            : p.Slug,
                        ProductImage = !string.IsNullOrWhiteSpace(p.ProductImage1) ? p.ProductImage1 : (!string.IsNullOrWhiteSpace(p.ProductImage2) ? p.ProductImage2 : p.ProductImage3)
                    })
                    .ToList();

                // If no products matched earlier filters, try a generic keyword search in the catalog
                if (products.Count == 0)
                {
                    var stopwords = new[] { "tôi", "mình", "là", "có", "cần", "muốn", "mua", "giá", "xin", "vui", "nhé", "đưa", "cho", "là", "anh", "chị", "các", "và" };
                    var tokens = Regex.Matches(req.message ?? string.Empty, "\\p{L}[\\p{L}\\p{N}]+")
                        .Select(m => m.Value.ToLowerInvariant())
                        .Where(w => w.Length > 1 && !stopwords.Contains(w))
                        .Distinct()
                        .Take(6)
                        .ToList();

                    if (tokens.Count > 0)
                    {
                        var foundQuery = _db.Product
                            .AsNoTracking()
                            .Include(p => p.Category)
                            .Where(p => tokens.Any(t =>
                                (p.ProductName != null && p.ProductName.ToLower().Contains(t)) ||
                                (p.ProductDescription != null && p.ProductDescription.ToLower().Contains(t)) ||
                                (p.Category != null && p.Category.CategoryName != null && p.Category.CategoryName.ToLower().Contains(t))
                            ))
                            .OrderByDescending(p => p.Id)
                            .Take(8);

                        var foundRaw = await foundQuery.Select(p => new
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
                            p.Slug
                        }).ToListAsync();

                        var foundProducts = foundRaw.Select(p => new ChatProduct
                        {
                            ProductId = p.Id,
                            ProductName = p.ProductName,
                            CategoryName = p.CategoryName,
                            Brand = p.Brand,
                            Cpu = p.CPU,
                            Ram = p.RAM,
                            Storage = p.Storage,
                            ProductPrice = p.ProductPrice,
                            ProductDescription = p.ProductDescription,
                            Slug = string.IsNullOrWhiteSpace(p.Slug) ? SlugHelper.GenerateSlug(p.ProductName ?? string.Empty) : p.Slug
                        }).ToList();

                        if (foundProducts.Count > 0)
                        {
                            products = foundProducts;
                        }
                    }
                }

                // If user asked about phones but initial filtering returned no products,
                // try an explicit phone search across the catalog and return those if found.
                if (IsPhoneIntent(req.message) && products.Count == 0)
                {
                    var phoneQuery = _db.Product
                        .AsNoTracking()
                        .Include(p => p.Category)
                        .Where(p =>
                            (p.Category != null && p.Category.CategoryName != null && p.Category.CategoryName.ToLower().Contains("điện thoại"))
                            || (p.ProductName != null && (p.ProductName.ToLower().Contains("điện thoại") || p.ProductName.ToLower().Contains("iphone") || p.ProductName.ToLower().Contains("samsung") || p.ProductName.ToLower().Contains("xiaomi") || p.ProductName.ToLower().Contains("oppo") || p.ProductName.ToLower().Contains("vivo")))
                            || (p.ProductDescription != null && p.ProductDescription.ToLower().Contains("điện thoại"))
                        )
                        .OrderByDescending(p => p.Id)
                        .Take(8);

                    var phoneRaw = await phoneQuery.Select(p => new
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
                        p.Slug
                    }).ToListAsync();

                    var phoneProducts = phoneRaw.Select(p => new ChatProduct
                    {
                        ProductId = p.Id,
                        ProductName = p.ProductName,
                        CategoryName = p.CategoryName,
                        Brand = p.Brand,
                        Cpu = p.CPU,
                        Ram = p.RAM,
                        Storage = p.Storage,
                        ProductPrice = p.ProductPrice,
                        ProductDescription = p.ProductDescription,
                        Slug = string.IsNullOrWhiteSpace(p.Slug) ? SlugHelper.GenerateSlug(p.ProductName ?? string.Empty) : p.Slug
                    }).ToList();

                    if (phoneProducts.Count > 0)
                    {
                        products = phoneProducts;
                    }
                }

                if (isLaptopIntent && products.Count == 0)
                {
                    return Ok(new ChatResponse { reply = BuildFallbackReply(products, req.message) });
                }

                sb.AppendLine("Dữ liệu sản phẩm của shop:");
                foreach (var p in products)
                {
                    var desc = Shorten(SanitizeInline(p.ProductDescription), 150);
                    var cpu = Shorten(SanitizeInline(p.Cpu), 60);
                    var ram = Shorten(SanitizeInline(p.Ram), 40);
                    var storage = Shorten(SanitizeInline(p.Storage), 60);
                    var brand = Shorten(SanitizeInline(p.Brand), 40);

                    sb.AppendLine($"- {p.ProductName} — {p.ProductPrice:N0} VND — Mua ngay: /Cart/Checkout?selectedProductIds={p.ProductId}");
                    var specLine = new StringBuilder();
                    if (!string.IsNullOrWhiteSpace(brand)) specLine.Append($"Hãng: {brand}; ");
                    if (!string.IsNullOrWhiteSpace(cpu)) specLine.Append($"CPU: {cpu}; ");
                    if (!string.IsNullOrWhiteSpace(ram)) specLine.Append($"RAM: {ram}; ");
                    if (!string.IsNullOrWhiteSpace(storage)) specLine.Append($"SSD: {storage}; ");

                    if (specLine.Length > 0)
                    {
                        sb.AppendLine($"  {specLine.ToString().TrimEnd().TrimEnd(';')}");
                    }

                    if (!string.IsNullOrWhiteSpace(desc))
                    {
                        sb.AppendLine($"  Mô tả: {desc}");
                    }

                    // Include a canonical image line so the model (and client parser) can reference thumbnails.
                    var imageUrl = p.ProductImage ?? "/images/laptop.png";
                    sb.AppendLine($"  Ảnh: {imageUrl}");
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load products for AI chat");
                sb.AppendLine("Dữ liệu sản phẩm hiện tạm thời không tải được. Hãy tư vấn chung theo nhu cầu của khách, không tự ý chuyển sang danh mục khác.");
            }

            sb.AppendLine($"Câu hỏi của khách: {req.message}");

            var apiUrl = _config["Groq:ApiUrl"] ?? "https://api.groq.com/openai/v1/chat/completions";
            var apiKey = _config["Groq:ApiKey"];
            var preferredModel = _config["Groq:Model"] ?? "llama-3.3-70b-versatile";
            var modelsToTry = new[] { preferredModel }
                .Concat(ModelFallbacks)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .ToArray();
            if (string.IsNullOrWhiteSpace(apiUrl) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("Groq API not configured. Set Groq:ApiUrl and Groq:ApiKey.");
                return StatusCode(500, new { message = "AI chưa được cấu hình (Groq). Vui lòng cấu hình Groq:ApiKey." });
            }

            var localFallback = BuildFallbackReply(products, req.message);

            if (IsColorAdviceIntent(req.message) && (IsPhoneIntent(req.message) || ConversationIndicatesPhone(req.conversation) || req.message.ToLowerInvariant().Contains("điện thoại") || req.message.ToLowerInvariant().Contains("phone")))
            {
                return Ok(new ChatResponse { reply = BuildPhoneColorAdvice(req.message) });
            }

            // Previously we returned a fast rule-based reply for first-turn product queries.
            // Remove that early return so Groq is always invoked and the model can answer.

            try
            {
                var client = _httpFactory.CreateClient();
                var systemMessage = IsLaptopIntent(req.message)
                    ? "Bạn là nhân viên bán hàng AI cho shop laptop. Chỉ tư vấn laptop và phụ kiện laptop. Tuyệt đối không chuyển sang điện thoại hay danh mục khác. Nếu không có laptop phù hợp trong danh sách thì nói rõ là chưa có sản phẩm laptop phù hợp. Mỗi sản phẩm phải xuống dòng riêng theo đúng mẫu: - Tên — Giá — Mua ngay: /Cart/Checkout?selectedProductIds=id"
                    : "Bạn là nhân viên bán hàng AI cho shop laptop. Ưu tiên tư vấn theo nhu cầu, đưa ra gợi ý bán hàng tự nhiên và ngắn gọn. Mỗi sản phẩm phải xuống dòng riêng theo đúng mẫu: - Tên — Giá — Mua ngay: /Cart/Checkout?selectedProductIds=id";
                var userMessage = sb.ToString();

                foreach (var model in modelsToTry)
                {
                    // Build the chat-style messages: system + prior conversation items (if any) + current user message
                    var messageList = new List<object>
                    {
                        new { role = "system", content = systemMessage }
                    };

                    if (req.conversation != null)
                    {
                        foreach (var c in req.conversation)
                        {
                            var role = string.Equals(c.role, "assistant", System.StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
                            messageList.Add(new { role = role, content = c.content ?? string.Empty });
                        }
                    }

                    messageList.Add(new { role = "user", content = userMessage });

                    var payload = JsonSerializer.Serialize(new
                    {
                        model = model,
                        messages = messageList,
                        temperature = 0.4,
                        max_tokens = 500
                    });

                    var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                    var res = await client.SendAsync(request);
                    var txt = await res.Content.ReadAsStringAsync();

                    if (!res.IsSuccessStatusCode)
                    {
                        var shouldTryNext = false;
                        try
                        {
                            using var errorDoc = JsonDocument.Parse(txt);
                            if (errorDoc.RootElement.TryGetProperty("error", out var errorEl))
                            {
                                var code = errorEl.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null;
                                var message = errorEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
                                shouldTryNext = string.Equals(code, "model_decommissioned", StringComparison.OrdinalIgnoreCase)
                                                 || (!string.IsNullOrWhiteSpace(message) && message.Contains("decommissioned", StringComparison.OrdinalIgnoreCase))
                                                 || ((int)res.StatusCode == 400 && !string.IsNullOrWhiteSpace(message));
                            }
                        }
                        catch
                        {
                            shouldTryNext = (int)res.StatusCode == 400 || (int)res.StatusCode == 404;
                        }

                        if (shouldTryNext)
                        {
                            continue;
                        }

                        _logger.LogWarning("Groq returned {StatusCode}: {Body}", (int)res.StatusCode, txt);
                        return Ok(new ChatResponse
                        {
                            reply = localFallback
                        });
                    }

                    // Parse Groq/OpenAI-compatible response
                    string reply = txt;
                    try
                    {
                        using var doc = JsonDocument.Parse(txt);
                        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
                        {
                            var first = choices.EnumerateArray().FirstOrDefault();
                            if (first.ValueKind != JsonValueKind.Undefined && first.TryGetProperty("message", out var messageEl) && messageEl.TryGetProperty("content", out var contentEl))
                            {
                                reply = contentEl.GetString() ?? txt;
                            }
                        }
                    }
                    catch { /* ignore parse errors */ }

                    // Ensure the AI reply includes product image lines the client expects.
                    try
                    {
                        reply = EnsureProductImagesInReply(reply, products);
                    }
                    catch (System.Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to ensure product images in AI reply");
                    }

                    return Ok(new ChatResponse { reply = reply });
                }

                return Ok(new ChatResponse { reply = localFallback });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "AI chat failed");
                return Ok(new ChatResponse { reply = localFallback });
            }
        }

        private static string BuildFallbackReply(List<ChatProduct> products, string message)
        {
            if (products.Count == 0)
            {
                return IsLaptopIntent(message)
                    ? "Xin lỗi, hiện tại chưa có sản phẩm laptop phù hợp trong danh mục. Bạn có thể xem danh mục Laptop hoặc cho mình biết mức giá mong muốn."
                    : "Xin chào! Hiện tôi chưa tìm thấy sản phẩm phù hợp. Bạn hãy cho mình biết nhu cầu hoặc ngân sách nhé.";
            }

            var intro = IsLaptopIntent(message)
                ? "Xin chào! Mình gợi ý một số laptop phù hợp:\n"
                : "Xin chào! Hiện tôi có thể gợi ý các sản phẩm sau:\n";

            return intro + string.Join("\n", products.Select(FormatProductBlock));
        }

        private static string FormatProductBlock(ChatProduct product)
        {
            var lines = new List<string>
            {
                $"- {product.ProductName} — {product.ProductPrice:N0} VND — Mua ngay: /Cart/Checkout?selectedProductIds={product.ProductId}"
            };

            var specParts = new List<string>();

            var brand = Shorten(SanitizeInline(product.Brand), 40);
            var cpu = Shorten(SanitizeInline(product.Cpu), 60);
            var ram = Shorten(SanitizeInline(product.Ram), 40);
            var storage = Shorten(SanitizeInline(product.Storage), 60);
            var desc = Shorten(SanitizeInline(product.ProductDescription), 150);
            if (!string.IsNullOrWhiteSpace(brand)) specParts.Add($"Hãng: {brand}");
            if (!string.IsNullOrWhiteSpace(cpu)) specParts.Add($"CPU: {cpu}");
            if (!string.IsNullOrWhiteSpace(ram)) specParts.Add($"RAM: {ram}");
            if (!string.IsNullOrWhiteSpace(storage)) specParts.Add($"SSD: {storage}");

            if (specParts.Count > 0)
            {
                lines.Add($"  {string.Join("; ", specParts)}");
            }

            // If no explicit product description, synthesize a short description from specs.
            if (string.IsNullOrWhiteSpace(desc))
            {
                var synth = new List<string>();
                if (!string.IsNullOrWhiteSpace(brand)) synth.Add(brand);
                if (!string.IsNullOrWhiteSpace(cpu)) synth.Add(cpu);
                if (!string.IsNullOrWhiteSpace(ram)) synth.Add(ram + " RAM");
                if (!string.IsNullOrWhiteSpace(storage)) synth.Add(storage + " storage");

                if (synth.Count > 0)
                {
                    desc = Shorten(string.Join(" · ", synth), 120);
                }
            }

            if (!string.IsNullOrWhiteSpace(desc))
            {
                lines.Add($"  Mô tả: {desc}");
            }

            return string.Join("\n", lines);
        }

        private static string SanitizeInline(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace('\n', ' ').Replace('\r', ' ').Trim();
        }

        private static string EnsureProductImagesInReply(string reply, List<ChatProduct> products)
        {
            if (string.IsNullOrWhiteSpace(reply) || products == null || products.Count == 0) return reply;

            var text = reply;

            foreach (var p in products)
            {
                var buyToken = $"/Cart/Checkout?selectedProductIds={p.ProductId}";
                if (!text.Contains(buyToken)) continue;

                // If exact image line already present, skip
                if (text.Contains($"Ảnh: {p.ProductImage}")) continue;

                // Try to insert image line immediately after the product line if found
                var prodLine = $"- {p.ProductName}";
                var idx = text.IndexOf(prodLine, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var lineEnd = text.IndexOf('\n', idx);
                    var insertAt = lineEnd >= 0 ? lineEnd + 1 : text.Length;
                    text = text.Insert(insertAt, $"  Ảnh: {p.ProductImage}\n");
                }
                else
                {
                    // Fallback: append an image line at the end
                    text = text + $"\nẢnh: {p.ProductImage}";
                }
            }

            return text;
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLength - 1).TrimEnd() + "…";
        }
    }
}
