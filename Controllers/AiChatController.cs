using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
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

        public ChatController(IHttpClientFactory httpFactory, IConfiguration config, ILogger<ChatController> logger)
        {
            _httpFactory = httpFactory;
            _config = config;
            _logger = logger;
        }

        public class ChatRequest { public string message { get; set; } = string.Empty; public List<ConversationItem> conversation { get; set; } = new(); }
        public class ConversationItem { public string role { get; set; } = string.Empty; public string content { get; set; } = string.Empty; }
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

        private static readonly string[] DetailKeywords = new[]
        {
            "chi tiết", "thông tin", "thông số", "spec", "cấu hình",
            "xem thêm", "giới thiệu", "mô tả", "tính năng", "đặc điểm",
            "giá của", "giá bao nhiêu", "cho biết", "review"
        };

        private static string Normalize(string s) => (s ?? string.Empty).ToLowerInvariant();

        private static bool IsLaptopIntent(string msg)    => LaptopKeywords.Any(Normalize(msg).Contains);
        private static bool IsPhoneIntent(string msg)     => PhoneKeywords.Any(Normalize(msg).Contains);
        private static bool IsChargerIntent(string msg)   => ChargerKeywords.Any(Normalize(msg).Contains);
        private static bool IsKeyboardIntent(string msg)  => KeyboardKeywords.Any(Normalize(msg).Contains);
        private static bool IsHeadphoneIntent(string msg) => HeadphoneKeywords.Any(Normalize(msg).Contains);
        private static bool IsDetailIntent(string msg)    => DetailKeywords.Any(Normalize(msg).Contains);

        private static List<ChatProduct> SelectRelevantProducts(List<ChatProduct> products, string message)
        {
            if (products == null || products.Count == 0) return new List<ChatProduct>();

            bool MatchesAny(ChatProduct p, string[] keywords) =>
                keywords.Any(k =>
                    (p.CategoryName ?? "").Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    (p.ProductName  ?? "").Contains(k, StringComparison.OrdinalIgnoreCase));

            if (IsChargerIntent(message))    return products.Where(p => MatchesAny(p, ChargerKeywords)).OrderByDescending(p => p.ProductId).ToList();
            if (IsKeyboardIntent(message))   return products.Where(p => MatchesAny(p, KeyboardKeywords)).OrderByDescending(p => p.ProductId).ToList();
            if (IsHeadphoneIntent(message))  return products.Where(p => MatchesAny(p, HeadphoneKeywords)).OrderByDescending(p => p.ProductId).ToList();
            if (IsPhoneIntent(message))      return products.Where(p => MatchesAny(p, PhoneKeywords)).OrderByDescending(p => p.ProductId).ToList();
            if (IsLaptopIntent(message))     return products.Where(p => MatchesAny(p, LaptopKeywords)).OrderByDescending(p => p.ProductId).ToList();

            return products.OrderByDescending(p => p.ProductId).Take(12).ToList();
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

            // ── Log để debug trên Azure ──────────────────────────────────────────
            _logger.LogInformation("[Chat] ApiKey prefix={Prefix}, Url={Url}, Model={Model}",
                groqApiKey.Length > 10 ? groqApiKey[..10] + "..." : "TOO_SHORT",
                groqApiUrl,
                groqModel);

            var products = new List<ChatProduct>();
            try
            {
                // FIX: thêm timeout ngắn để tránh self-call treo trên Azure
                var internalClient = _httpFactory.CreateClient();
                internalClient.Timeout = TimeSpan.FromSeconds(5);

                var internalUrl = $"{Request.Scheme}://{Request.Host}/api/internal/products";
                _logger.LogInformation("[Chat] Calling internal products API: {Url}", internalUrl);

                var payload = JsonSerializer.Serialize(new { message = req.message, conversation = req.conversation });
                var internalReq = new HttpRequestMessage(HttpMethod.Post, internalUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var internalRes = await internalClient.SendAsync(internalReq, cts.Token);
                var internalTxt = await internalRes.Content.ReadAsStringAsync();

                if (internalRes.IsSuccessStatusCode)
                {
                    var parsed = JsonSerializer.Deserialize<List<ChatProduct>>(internalTxt, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed != null) products = parsed;
                    _logger.LogInformation("[Chat] Loaded {Count} products from internal API.", products.Count);
                }
                else
                {
                    _logger.LogWarning("[Chat] Internal products API {Status}: {Body}", (int)internalRes.StatusCode, internalTxt);
                }

                products = SelectRelevantProducts(products, req.message);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("[Chat] Internal products API timeout — bỏ qua, tiếp tục với danh sách trống.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Chat] Không thể tải sản phẩm từ internal API — bỏ qua.");
            }

            bool isDetail = IsDetailIntent(req.message);
            var systemMessage = isDetail
                ? BuildDetailSystemMessage(products)
                : BuildSystemMessage(products);

            try
            {
                var groqClient = _httpFactory.CreateClient();
                groqClient.Timeout = TimeSpan.FromSeconds(30);

                var reply = await InvokeGroqAsync(groqClient, groqApiUrl, groqApiKey, groqModel, systemMessage, req.message, req.conversation ?? new());

                if (string.IsNullOrWhiteSpace(reply))
                {
                    _logger.LogWarning("[Chat] Groq trả về phản hồi rỗng.");
                    return StatusCode(502, new { message = "AI trả về phản hồi rỗng." });
                }

                if (!isDetail)
                {
                    try { reply = EnsureProductImagesInReply(reply, products); }
                    catch (Exception ex) { _logger.LogWarning(ex, "EnsureProductImages failed"); }
                }

                return Ok(new ChatResponse { reply = reply });
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("[Chat] Groq API timeout sau 30 giây.");
                return StatusCode(504, new { message = "AI phản hồi quá chậm, vui lòng thử lại." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Chat] Groq API call failed");
                return StatusCode(500, new { message = "AI chat failed." });
            }
        }

        private static string BuildSystemMessage(List<ChatProduct> products)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Bạn là nhân viên bán hàng AI của shop. Quy tắc BẮT BUỘC:");
            sb.AppendLine("1. CHỈ được tư vấn sản phẩm có trong DANH SÁCH bên dưới. TUYỆT ĐỐI không được bịa, không đề xuất bất kỳ sản phẩm nào ngoài danh sách.");
            sb.AppendLine("2. Nếu danh sách trống hoặc không có sản phẩm phù hợp, trả lời: 'Shop hiện chưa có sản phẩm phù hợp với yêu cầu này.'");
            sb.AppendLine("3. Trả lời bằng tiếng Việt, ngắn gọn, thân thiện. Gợi ý 2-3 sản phẩm phù hợp nhất.");
            sb.AppendLine("4. Mỗi sản phẩm gợi ý phải xuống dòng riêng theo đúng mẫu:");
            sb.AppendLine("   - [Tên sản phẩm] — [Giá] VND — Mua ngay: /Cart/Checkout?selectedProductIds=[ID]");
            sb.AppendLine("   Nếu có ảnh, dòng kế tiếp ghi:   Ảnh: [url ảnh]");
            sb.AppendLine();

            if (products == null || products.Count == 0)
            {
                sb.AppendLine("DANH SÁCH SẢN PHẨM: (trống)");
                return sb.ToString();
            }

            sb.AppendLine($"DANH SÁCH SẢN PHẨM ({products.Count} sản phẩm — chỉ dùng các ID sau):");
            foreach (var p in products)
            {
                sb.Append($"[ID:{p.ProductId}] {p.ProductName}");
                sb.Append($" | Giá: {p.ProductPrice:N0} VND");
                sb.Append($" | Link: /Cart/Checkout?selectedProductIds={p.ProductId}");
                if (!string.IsNullOrWhiteSpace(p.CategoryName))       sb.Append($" | Danh mục: {p.CategoryName}");
                if (!string.IsNullOrWhiteSpace(p.Brand))              sb.Append($" | Hãng: {Shorten(SanitizeInline(p.Brand), 40)}");
                if (!string.IsNullOrWhiteSpace(p.CPU))                sb.Append($" | CPU: {Shorten(SanitizeInline(p.CPU), 80)}");
                if (!string.IsNullOrWhiteSpace(p.RAM))                sb.Append($" | RAM: {Shorten(SanitizeInline(p.RAM), 40)}");
                if (!string.IsNullOrWhiteSpace(p.Storage))            sb.Append($" | SSD: {Shorten(SanitizeInline(p.Storage), 60)}");
                if (!string.IsNullOrWhiteSpace(p.ProductDescription)) sb.Append($" | Mô tả: {Shorten(SanitizeInline(p.ProductDescription), 150)}");
                if (!string.IsNullOrWhiteSpace(p.ProductImage))       sb.Append($" | Ảnh: {p.ProductImage}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string BuildDetailSystemMessage(List<ChatProduct> products)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Bạn là nhân viên bán hàng AI của shop. Quy tắc BẮT BUỘC:");
            sb.AppendLine("1. CHỈ dùng sản phẩm trong DANH SÁCH bên dưới. TUYỆT ĐỐI không bịa.");
            sb.AppendLine("2. Khi người dùng hỏi thông tin / chi tiết / thông số của một sản phẩm, trả về ĐÚNG định dạng sau và KHÔNG thêm bất kỳ text nào khác ngoài block:");
            sb.AppendLine();
            sb.AppendLine("```product-detail");
            sb.AppendLine("id: [ProductId]");
            sb.AppendLine("name: [Tên sản phẩm]");
            sb.AppendLine("price: [Giá dạng số, không có dấu phẩy]");
            sb.AppendLine("brand: [Hãng]");
            sb.AppendLine("cpu: [CPU nếu có, để trống nếu không]");
            sb.AppendLine("ram: [RAM nếu có, để trống nếu không]");
            sb.AppendLine("storage: [Storage nếu có, để trống nếu không]");
            sb.AppendLine("description: [Mô tả ngắn gọn bằng tiếng Việt]");
            sb.AppendLine("image: [url ảnh]");
            sb.AppendLine("link: /Cart/Checkout?selectedProductIds=[ProductId]");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("3. Nếu không tìm thấy sản phẩm phù hợp, chỉ trả lời: 'Shop hiện chưa có sản phẩm phù hợp.'");
            sb.AppendLine();

            if (products == null || products.Count == 0)
            {
                sb.AppendLine("DANH SÁCH SẢN PHẨM: (trống)");
                return sb.ToString();
            }

            sb.AppendLine($"DANH SÁCH SẢN PHẨM ({products.Count} sản phẩm):");
            foreach (var p in products)
            {
                sb.Append($"[ID:{p.ProductId}] {p.ProductName}");
                sb.Append($" | Giá: {p.ProductPrice:N0} VND");
                sb.Append($" | Link: /Cart/Checkout?selectedProductIds={p.ProductId}");
                if (!string.IsNullOrWhiteSpace(p.CategoryName))       sb.Append($" | Danh mục: {p.CategoryName}");
                if (!string.IsNullOrWhiteSpace(p.Brand))              sb.Append($" | Hãng: {SanitizeInline(p.Brand)}");
                if (!string.IsNullOrWhiteSpace(p.CPU))                sb.Append($" | CPU: {Shorten(SanitizeInline(p.CPU), 80)}");
                if (!string.IsNullOrWhiteSpace(p.RAM))                sb.Append($" | RAM: {SanitizeInline(p.RAM)}");
                if (!string.IsNullOrWhiteSpace(p.Storage))            sb.Append($" | SSD: {SanitizeInline(p.Storage)}");
                if (!string.IsNullOrWhiteSpace(p.ProductDescription)) sb.Append($" | Mô tả: {Shorten(SanitizeInline(p.ProductDescription), 200)}");
                if (!string.IsNullOrWhiteSpace(p.ProductImage))       sb.Append($" | Ảnh: {p.ProductImage}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static async Task<string?> InvokeGroqAsync(
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
                            retry = string.Equals(code, "model_decommissioned", StringComparison.OrdinalIgnoreCase)
                                 || (msg?.Contains("decommissioned", StringComparison.OrdinalIgnoreCase) ?? false)
                                 || ((int)res.StatusCode is 400 or 404);
                        }
                    }
                    catch { retry = (int)res.StatusCode is 400 or 404; }

                    if (retry) continue;
                    return null;
                }

                var reply = ParseGroqReply(txt);
                if (!string.IsNullOrWhiteSpace(reply)) return reply;
            }

            return null;
        }

        private static List<object> BuildMessages(string systemMessage, string userMessage, List<ConversationItem> conversation)
        {
            var list = new List<object> { new { role = "system", content = systemMessage } };

            if (conversation != null)
            {
                foreach (var c in conversation.TakeLast(10))
                {
                    var role = string.Equals(c.role, "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
                    list.Add(new { role, content = c.content ?? string.Empty });
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
