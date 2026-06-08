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
using Microsoft.Extensions.Logging;

namespace LapTopBD.Controllers
{
    [Route("api/ai/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<ChatController> _logger;
        private readonly ApplicationDbContext _db;  // ← inject thẳng DbContext

        public ChatController(
            IHttpClientFactory httpFactory,
            IConfiguration config,
            ILogger<ChatController> logger,
            ApplicationDbContext db)  // ← thêm vào constructor
        {
            _httpFactory = httpFactory;
            _config = config;
            _logger = logger;
            _db = db;
        }

        public class ChatRequest
        {
            public string message { get; set; } = string.Empty;
            public List<ConversationItem> conversation { get; set; } = new();
        }
        public class ConversationItem
        {
            public string role { get; set; } = string.Empty;
            public string content { get; set; } = string.Empty;
        }
        public class ChatResponse { public string reply { get; set; } = string.Empty; }

        private sealed class ChatProduct
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

        private sealed class GroqCallResult
        {
            public string? Reply { get; set; }
            public string? Error { get; set; }
            public int? StatusCode { get; set; }
            public string Model { get; set; } = string.Empty;
            public string RawBody { get; set; } = string.Empty;
        }

        private static readonly string[] ModelFallbacks = new[]
        {
            "llama-3.3-70b-versatile",
            "llama-3.1-70b-versatile",
            "llama-3.1-8b-instant"
        };

        private static readonly string[] LaptopKeywords   = new[] { "laptop", "lap top", "notebook", "máy tính xách tay", "gaming", "ultrabook", "workstation", "thinkpad", "legion", "rog", "vivobook", "zenbook", "macbook", "inspiron", "latitude", "xps", "dell", "lenovo", "asus", "acer" };
        private static readonly string[] PhoneKeywords     = new[] { "điện thoại", "phone", "iphone", "samsung", "xiaomi", "oppo", "realme", "vivo", "pixel" };
        private static readonly string[] ChargerKeywords   = new[] { "sạc", "charger", "adapter", "cáp", "cable", "pin sạc" };
        private static readonly string[] KeyboardKeywords  = new[] { "bàn phím", "bàn phím cơ", "keyboard", "mechanical", "gaming keyboard" };
        private static readonly string[] HeadphoneKeywords = new[] { "tai nghe", "headphone", "headset", "earbuds", "bluetooth" };
        private static readonly string[] GamingKeywords    = new[] { "game", "gaming", "choi game", "chơi game", "laptop gaming", "gaming laptop", "stream game", "do hoa game", "đồ họa game" };

        private static readonly string[] DetailKeywords = new[]
        {
            "chi tiết", "thông tin", "thông số", "spec", "cấu hình",
            "xem thêm", "giới thiệu", "mô tả", "tính năng", "đặc điểm",
            "giá của", "giá bao nhiêu", "cho biết", "review"
        };

        private static readonly string[] StopWords = new[]
        {
            "chi tiết", "thông tin", "thông số", "spec", "cấu hình",
            "xem thêm", "giới thiệu", "mô tả", "tính năng", "đặc điểm",
            "giá của", "giá bao nhiêu", "cho biết", "review",
            "của", "bạn", "cho", "tôi", "biết", "về", "sản phẩm",
            "là", "gì", "có", "không", "thế", "nào", "như", "vậy",
            "ơi", "ai", "ạ", "nhé", "nha", "với"
        };

        private static string Normalize(string s) => (s ?? string.Empty).ToLowerInvariant();
        private static bool IsDetailIntent(string msg) => DetailKeywords.Any(Normalize(msg).Contains);
        private static bool IsLaptopIntent(string msg) => LaptopKeywords.Any(Normalize(msg).Contains) || GamingKeywords.Any(Normalize(msg).Contains);
        private static bool IsPhoneIntent(string msg)  => PhoneKeywords.Any(Normalize(msg).Contains);
        private static bool IsChargerIntent(string msg)   => ChargerKeywords.Any(Normalize(msg).Contains);
        private static bool IsKeyboardIntent(string msg)  => KeyboardKeywords.Any(Normalize(msg).Contains);
        private static bool IsHeadphoneIntent(string msg) => HeadphoneKeywords.Any(Normalize(msg).Contains);

        private static List<string> ExtractProductTokens(string message)
        {
            var lower = Normalize(message);
            foreach (var sw in StopWords.OrderByDescending(s => s.Length))
                lower = lower.Replace(sw, " ");
            return lower.Split(new[] { ' ', ',', '.', '?', '!', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(t => t.Length >= 2)
                        .Distinct()
                        .ToList();
        }

        private static string DetectCategory(string message)
        {
            var m = Normalize(message);
            if (m.Contains("sạc") || m.Contains("sac") || m.Contains("charger") || m.Contains("adapter") || m.Contains("cáp") || m.Contains("cable")) return "Sạc";
            if (m.Contains("bàn phím") || m.Contains("ban phim") || m.Contains("keyboard") || m.Contains("mechanical")) return "Bàn phím cơ";
            if (m.Contains("tai nghe") || m.Contains("headphone") || m.Contains("headset") || m.Contains("earbuds") || m.Contains("airpods")) return "Tai nghe";
            if (m.Contains("điện thoại") || m.Contains("dien thoai") || m.Contains("phone") || m.Contains("iphone") || m.Contains("samsung") || m.Contains("xiaomi") || m.Contains("oppo")) return "Điện thoại";
            if (IsLaptopIntent(message)) return "Laptop";
            return string.Empty;
        }

        // ── Thay self-call HTTP bằng query thẳng vào DB ─────────────────────────
        private async Task<List<ChatProduct>> LoadProductsAsync(string message)
        {
            List<ChatProduct> result;

            if (IsDetailIntent(message))
            {
                var tokens = ExtractProductTokens(message);
                if (tokens.Count > 0)
                {
                    var all = await _db.Product
                        .AsNoTracking()
                        .Include(p => p.Category)
                        .Select(p => new ChatProduct
                        {
                            ProductId          = p.Id,
                            ProductName        = p.ProductName        ?? string.Empty,
                            CategoryName       = p.Category != null ? p.Category.CategoryName : string.Empty,
                            Brand              = p.Brand              ?? string.Empty,
                            CPU                = p.CPU                ?? string.Empty,
                            RAM                = p.RAM                ?? string.Empty,
                            Storage            = p.Storage            ?? string.Empty,
                            ProductPrice       = p.ProductPrice,
                            ProductDescription = p.ProductDescription ?? string.Empty,
                            Slug               = p.Slug               ?? string.Empty,
                            ProductImage       = p.ProductImage1 != null && p.ProductImage1 != "" ? p.ProductImage1
                                                 : p.ProductImage2 != null && p.ProductImage2 != "" ? p.ProductImage2
                                                 : (p.ProductImage3 ?? string.Empty)
                        })
                        .ToListAsync();

                    var scored = all
                        .Select(p => new {
                            Product = p,
                            Score = tokens.Count(t =>
                                p.ProductName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                p.Brand.Contains(t, StringComparison.OrdinalIgnoreCase))
                        })
                        .Where(x => x.Score > 0)
                        .OrderByDescending(x => x.Score)
                        .Take(3)
                        .Select(x => x.Product)
                        .ToList();

                    if (scored.Count > 0) return scored;
                }
            }

            var category = DetectCategory(message);
            var raw = await _db.Product
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => category == string.Empty || (p.Category != null && p.Category.CategoryName == category))
                .Select(p => new ChatProduct
                {
                    ProductId          = p.Id,
                    ProductName        = p.ProductName        ?? string.Empty,
                    CategoryName       = p.Category != null ? p.Category.CategoryName : string.Empty,
                    Brand              = p.Brand              ?? string.Empty,
                    CPU                = p.CPU                ?? string.Empty,
                    RAM                = p.RAM                ?? string.Empty,
                    Storage            = p.Storage            ?? string.Empty,
                    ProductPrice       = p.ProductPrice,
                    ProductDescription = p.ProductDescription ?? string.Empty,
                    Slug               = p.Slug               ?? string.Empty,
                    ProductImage       = p.ProductImage1 != null && p.ProductImage1 != "" ? p.ProductImage1
                                         : p.ProductImage2 != null && p.ProductImage2 != "" ? p.ProductImage2
                                         : (p.ProductImage3 ?? string.Empty)
                })
                .OrderByDescending(p => p.ProductImage != "")
                .ThenByDescending(p => p.ProductId)
                .Take(12)
                .ToListAsync();

            return raw;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.message))
                return BadRequest("Missing message");

            var groqApiKey = _config["Groq:ApiKey"];
            if (string.IsNullOrWhiteSpace(groqApiKey))
            {
                _logger.LogError("Groq:ApiKey chưa được cấu hình.");
                return StatusCode(500, new { message = "Groq:ApiKey chưa được cấu hình." });
            }

            var groqApiUrl = _config["Groq:ApiUrl"] ?? "https://api.groq.com/openai/v1/chat/completions";
            var groqModel  = _config["Groq:Model"]  ?? "llama-3.3-70b-versatile";

            _logger.LogInformation("[Chat] ApiKey prefix={Prefix}, Model={Model}",
                groqApiKey.Length > 10 ? groqApiKey[..10] + "..." : "TOO_SHORT", groqModel);

            // ── Query thẳng DB, không self-call HTTP ─────────────────────────────
            var products = new List<ChatProduct>();
            try
            {
                products = await LoadProductsAsync(req.message);
                _logger.LogInformation("[Chat] Loaded {Count} products from DB.", products.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Chat] Không thể tải sản phẩm từ DB — tiếp tục với danh sách trống.");
            }

            var systemMessage = BuildFlexibleProductSystemMessage(products);

            try
            {
                var groqClient = _httpFactory.CreateClient();
                groqClient.Timeout = TimeSpan.FromSeconds(30);

                var result = await InvokeGroqAsync(groqClient, groqApiUrl, groqApiKey, groqModel, systemMessage, req.message, req.conversation ?? new());
                var reply = result.Reply;

                if (string.IsNullOrWhiteSpace(reply))
                {
                    _logger.LogWarning("[Chat] Groq empty. Model={Model}, Status={Status}, Error={Error}, Body={Body}",
                        result.Model, result.StatusCode, result.Error, Shorten(SanitizeInline(result.RawBody), 500));
                    reply = BuildLocalFallbackReply(products);
                }

                if (string.IsNullOrWhiteSpace(reply))
                    return StatusCode(502, new { message = "AI trả về phản hồi rỗng." });

                try { reply = EnsureProductImagesInReply(reply, products); }
                catch (Exception ex) { _logger.LogWarning(ex, "EnsureProductImages failed"); }

                return Ok(new ChatResponse { reply = reply });
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("[Chat] Groq API timeout.");
                return StatusCode(504, new { message = "AI phản hồi quá chậm, vui lòng thử lại." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Chat] Groq API call failed");
                return StatusCode(500, new { message = "AI chat failed." });
            }
        }

        private static string BuildFlexibleProductSystemMessage(List<ChatProduct> products)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ban la nhan vien tu van AI cua shop laptop/linh kien.");
            sb.AppendLine("Muc tieu: tra loi linh hoat, tu nhien, thong minh nhu dang tu van that, nhung khi de xuat san pham thi phai dung san pham trong danh sach duoi day de frontend render thanh box co nut mua ngay.");
            sb.AppendLine("Khong mo dau bang cau xin loi kieu 'toi khong co thong tin cu the'. Neu cau hoi chung, hay gioi thieu ngan gon cac nhom lua chon va hoi them nhu cau/ngan sach.");
            sb.AppendLine("Neu nguoi dung co nhu cau ro rang, hay chon 2-3 san pham phu hop nhat, giai thich vi sao hop, uu/nhuoc diem ngan gon.");
            sb.AppendLine("Duoc noi chuyen mem mai truoc va sau danh sach san pham. Khong can may moc theo mot mau duy nhat.");
            sb.AppendLine("Quan trong: moi san pham de xuat phai viet thanh block rieng theo dung cau truc sau:");
            sb.AppendLine("- [Ten san pham] - [Gia] VND - Mua ngay: /Cart/Checkout?selectedProductIds=[ID]");
            sb.AppendLine("  Điểm phù hợp: [mot cau ngan gon]");
            sb.AppendLine("Khong bia ID, link, gia, anh hoac ten san pham ngoai danh sach.");
            sb.AppendLine("Neu danh sach trong hoac khong co san pham phu hop, hay tu van cach chon laptop theo nhu cau va hoi them thong tin.");
            sb.AppendLine();

            if (products == null || products.Count == 0)
            {
                sb.AppendLine("DANH SACH SAN PHAM: (trong)");
                return sb.ToString();
            }

            sb.AppendLine($"DANH SACH SAN PHAM ({products.Count} san pham):");
            foreach (var p in products.Take(12))
            {
                sb.Append($"[ID:{p.ProductId}] {p.ProductName}");
                sb.Append($" | Gia: {p.ProductPrice:N0} VND");
                sb.Append($" | Link: /Cart/Checkout?selectedProductIds={p.ProductId}");
                if (!string.IsNullOrWhiteSpace(p.CategoryName))       sb.Append($" | Danh muc: {p.CategoryName}");
                if (!string.IsNullOrWhiteSpace(p.Brand))              sb.Append($" | Hang: {Shorten(SanitizeInline(p.Brand), 40)}");
                if (!string.IsNullOrWhiteSpace(p.CPU))                sb.Append($" | CPU: {Shorten(SanitizeInline(p.CPU), 80)}");
                if (!string.IsNullOrWhiteSpace(p.RAM))                sb.Append($" | RAM: {Shorten(SanitizeInline(p.RAM), 50)}");
                if (!string.IsNullOrWhiteSpace(p.Storage))            sb.Append($" | SSD: {Shorten(SanitizeInline(p.Storage), 60)}");
                if (!string.IsNullOrWhiteSpace(p.ProductDescription)) sb.Append($" | Mo ta: {Shorten(SanitizeInline(p.ProductDescription), 180)}");
                if (!string.IsNullOrWhiteSpace(p.ProductImage))       sb.Append($" | Anh: {p.ProductImage}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static async Task<GroqCallResult> InvokeGroqAsync(
            HttpClient client, string apiUrl, string apiKey, string model,
            string systemMessage, string userMessage, List<ConversationItem> conversation)
        {
            var modelsToTry = new[] { model }
                .Concat(ModelFallbacks)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .ToArray();

            foreach (var candidateModel in modelsToTry)
            {
                var currentResult = new GroqCallResult { Model = candidateModel };
                var messages = BuildMessages(systemMessage, userMessage, conversation);
                var payload  = JsonSerializer.Serialize(new
                {
                    model       = candidateModel,
                    messages,
                    temperature = 0.2,
                    max_tokens  = 600
                });

                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var res = await client.SendAsync(request);
                var txt = await res.Content.ReadAsStringAsync();
                currentResult.StatusCode = (int)res.StatusCode;
                currentResult.RawBody = txt;

                if (!res.IsSuccessStatusCode)
                {
                    bool retry = false;
                    try
                    {
                        using var errDoc = JsonDocument.Parse(txt);
                        if (errDoc.RootElement.TryGetProperty("error", out var errEl))
                        {
                            var code = errEl.TryGetProperty("code",    out var c) ? c.GetString() : null;
                            var msg  = errEl.TryGetProperty("message", out var m) ? m.GetString() : null;
                            currentResult.Error = string.IsNullOrWhiteSpace(code) ? msg : $"{code}: {msg}";
                            retry = string.Equals(code, "model_decommissioned", StringComparison.OrdinalIgnoreCase)
                                 || (msg?.Contains("decommissioned", StringComparison.OrdinalIgnoreCase) ?? false)
                                 || ((int)res.StatusCode is 400 or 404);
                        }
                    }
                    catch { retry = (int)res.StatusCode is 400 or 404; }

                    if (retry) continue;
                    if (string.IsNullOrWhiteSpace(currentResult.Error))
                        currentResult.Error = $"Groq HTTP {(int)res.StatusCode}";
                    return currentResult;
                }

                currentResult.Reply = ParseGroqReply(txt);
                if (!string.IsNullOrWhiteSpace(currentResult.Reply)) return currentResult;
            }

            return new GroqCallResult
            {
                Model = string.Join(", ", modelsToTry),
                Error = "Tat ca model fallback deu khong tra ve noi dung hop le."
            };
        }

        private static List<object> BuildMessages(string systemMessage, string userMessage, List<ConversationItem> conversation)
        {
            var list = new List<object> { new { role = "system", content = systemMessage } };
            if (conversation != null)
            {
                foreach (var c in conversation.TakeLast(10))
                {
                    if (string.IsNullOrWhiteSpace(c.content)) continue;
                    var role = string.Equals(c.role, "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
                    list.Add(new { role, content = c.content });
                }
            }
            list.Add(new { role = "user", content = userMessage });
            return list;
        }

        private static string ParseGroqReply(string txt)
        {
            try
            {
                using var doc = JsonDocument.Parse(txt);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
                {
                    var first = choices.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind != JsonValueKind.Undefined
                        && first.TryGetProperty("message", out var msg)
                        && msg.TryGetProperty("content", out var content))
                        return content.GetString() ?? string.Empty;
                }
            }
            catch { }
            return txt;
        }

        private static string BuildLocalFallbackReply(List<ChatProduct> products)
        {
            if (products == null || products.Count == 0)
                return "Mình chưa nhận được phản hồi từ AI lúc này. Bạn thử hỏi lại ngắn hơn hoặc cho mình biết ngân sách, nhu cầu để mình tư vấn tiếp nhé.";

            var sb = new StringBuilder();
            sb.AppendLine("Mình gợi ý vài sản phẩm phù hợp trong shop:");
            foreach (var p in products.Take(3))
            {
                sb.AppendLine($"- {p.ProductName} - {p.ProductPrice:N0} VND - Mua ngay: /Cart/Checkout?selectedProductIds={p.ProductId}");
                if (!string.IsNullOrWhiteSpace(p.ProductImage))
                    sb.AppendLine($"  Ảnh: {p.ProductImage}");
            }
            return sb.ToString();
        }

        private static string EnsureProductImagesInReply(string reply, List<ChatProduct> products)
        {
            if (string.IsNullOrWhiteSpace(reply) || products == null || products.Count == 0) return reply;
            var text = reply;
            foreach (var p in products)
            {
                if (string.IsNullOrWhiteSpace(p.ProductImage)) continue;
                var buyToken = $"/Cart/Checkout?selectedProductIds={p.ProductId}";
                if (!text.Contains(buyToken)) continue;
                if (text.Contains($"Ảnh: {p.ProductImage}")) continue;

                var idx = text.IndexOf($"- {p.ProductName}", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var lineEnd  = text.IndexOf('\n', idx);
                    var insertAt = lineEnd >= 0 ? lineEnd + 1 : text.Length;
                    text = text.Insert(insertAt, $"  Ảnh: {p.ProductImage}\n");
                }
                else
                {
                    text += $"\nẢnh: {p.ProductImage}";
                }
            }
            return text;
        }

        private static string SanitizeInline(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('\n', ' ').Replace('\r', ' ').Trim();

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength) return value ?? string.Empty;
            return value[..(maxLength - 1)].TrimEnd() + "…";
        }
    }
}