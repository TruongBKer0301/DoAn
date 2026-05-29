using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LapTopBD.Models;
using System.Security.Claims;
using LapTopBD.Data;
using Microsoft.AspNetCore.Authentication;
using LapTopBD.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using LapTopBD.Utilities;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace LapTopBD.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IVnPayService _vnPayService;
        private readonly IPendingCheckoutStore _pendingCheckoutStore;
        private readonly ILogger<CartController> _logger;

        public CartController(
            ApplicationDbContext context,
            IVnPayService vnPayService,
            IPendingCheckoutStore pendingCheckoutStore,
            ILogger<CartController> logger)
        {
            _context = context;
            _vnPayService = vnPayService;
            _pendingCheckoutStore = pendingCheckoutStore;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
             var result = await HttpContext.AuthenticateAsync("UserAuth");
            if (result?.Succeeded == true)
            {
               
                HttpContext.User = result.Principal;
            }
            var userId = await GetUserIdAsync();
            if (userId == 0)
            {
                return RedirectToAction("Login", "UserAuth");
            }

            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();
            ViewBag.ShowBanner = false;
            return View(cartItems);
        }

        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            var userId = await GetUserIdAsync();
            Console.WriteLine($"[DEBUG] AddToCart - UserId: {userId}, ProductId: {productId}, Quantity: {quantity}");

            if (userId == 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            }

            var product = await _context.Product.FindAsync(productId);
            if (product == null || product.quantity < 1)
            {
                return Json(new { success = false, message = "Sản phẩm hiện tại đã hết hàng vui lòng liên hệ bên dưới để được tư vấn!" });
            }

            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

            if (cartItem != null)
            {
                cartItem.Quantity += quantity;
            }
            else
            {
                cartItem = new LapTopBD.Models.CartItem
                {
                    UserId = userId,
                    ProductId = productId,
                    Quantity = quantity,
                    AddedDate = DateTimeHelper.Now
                };
                _context.CartItems.Add(cartItem);
            }

            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã thêm vào giỏ hàng!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi lưu giỏ hàng: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
        {
            var userId = await GetUserIdAsync();
            var cartItem = await _context.CartItems
                .Include(c => c.Product)
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

            if (cartItem == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm trong giỏ hàng!" });
            }

            if (cartItem.Product == null || cartItem.Product.quantity < 1)
            {
                return Json(new { success = false, message = "Sản phẩm không còn sẵn có!" });
            }

            if (quantity <= 0)
            {
                _context.CartItems.Remove(cartItem);
            }
            else
            {
                cartItem.Quantity = quantity;
            }

            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã cập nhật giỏ hàng!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi cập nhật giỏ hàng: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            var userId = await GetUserIdAsync();
            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

            if (cartItem == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm trong giỏ hàng!" });
            }

            _context.CartItems.Remove(cartItem);
            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã xóa khỏi giỏ hàng!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi xóa khỏi giỏ hàng: {ex.Message}" });
            }
        }


        //Số lượng sản phẩm trong giỏ hàng
        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            var userId = await GetUserIdAsync();
            if (userId == 0)
            {
                return Json(new { success = false, cartItemCount = 0 });
            }

            // Count distinct cart items (number of rows) rather than sum of quantities
            int cartItemCount = await _context.CartItems
                .Where(c => c.UserId == userId)
                .CountAsync();

            return Json(new { success = true, cartItemCount });
        }

        //Số lượng sản phẩm đã đặt hàng
        [HttpGet]
        public async Task<IActionResult> GetOrderCount()
        {
            var userId = await GetUserIdAsync();
            if (userId == 0)
            {
                return Json(new { success = false, orderCount = 0 });
            }

            // Count all orders for the user (match what OrderConfirmation shows)
            int orderCount = await _context.Order
                .Where(o => o.UserId == userId)
                .CountAsync();

            return Json(new { success = true, orderCount });
        }

        [Authorize(AuthenticationSchemes = "UserAuth")]
        [HttpGet]
        public async Task<IActionResult> Checkout([FromQuery] List<int>? selectedProductIds)
        {
            var userId = await GetUserIdAsync();
            if (userId == 0)
            {
                return RedirectToAction("Login", "UserAuth");
            }

            // Lấy thông tin user để điền sẵn - AsNoTracking để lấy dữ liệu mới nhất
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return RedirectToAction("Login", "UserAuth");
            }

            _logger.LogInformation($"Checkout - User {userId} data: Name={user.Name}, City={user.City}, Ward={user.Ward}, Address={user.Address}");

            // Support alternative query formats for selectedProductIds (e.g. comma-separated strings produced by external links)
            try
            {
                var raw = Request.Query["selectedProductIds"].ToString();
                if ((selectedProductIds == null || !selectedProductIds.Any()) && !string.IsNullOrWhiteSpace(raw))
                {
                    // raw may be like "75,76" or "[75,76]" or "75%2C76" or "75"
                    var cleaned = raw.Trim();
                    // Remove surrounding brackets if present
                    if (cleaned.StartsWith("[") && cleaned.EndsWith("]")) cleaned = cleaned.Substring(1, cleaned.Length - 2);
                    var parts = cleaned.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList();

                    var parsed = new List<int>();
                    foreach (var part in parts)
                    {
                        if (int.TryParse(part, out var v))
                        {
                            parsed.Add(v);
                        }
                        else
                        {
                            // fallback: extract digits
                            var m = Regex.Matches(part, "\\d+");
                            foreach (Match mm in m)
                            {
                                if (int.TryParse(mm.Value, out var vv)) parsed.Add(vv);
                            }
                        }
                    }

                    if (parsed.Any())
                    {
                        selectedProductIds = parsed.Distinct().ToList();
                        _logger.LogInformation($"Parsed selectedProductIds from raw query: {string.Join(',', selectedProductIds)}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse raw selectedProductIds query string");
            }

            var cartItems = await ResolveCheckoutItemsAsync(userId, selectedProductIds);

            if (cartItems == null || !cartItems.Any())
            {
                TempData["Error"] = selectedProductIds != null && selectedProductIds.Any()
                    ? "Không có sản phẩm được chọn để thanh toán!"
                    : "Giỏ hàng của bạn đang trống!";
                return RedirectToAction("Index");
            }

            var finalSelectedIds = (selectedProductIds != null && selectedProductIds.Any())
                ? selectedProductIds.Distinct().ToList()
                : cartItems.Select(item => item.ProductId).Distinct().ToList();

            // Tính tổng tiền
            decimal totalPrice = cartItems.Sum(item => item.Subtotal);

            // Tạo model cho view
            var model = new CheckoutViewModel
            {
                Name = user.Name,
                ContactNo = user.ContactNo,
                City = user.City ?? "",
                Ward = user.Ward ?? "",
                Address = user.Address ?? "",
                SelectedProductIds = finalSelectedIds,
                CartItems = cartItems,
                TotalPrice = totalPrice
            };
            ViewBag.ShowBanner = false;
            return View(model);
        }

        [Authorize(AuthenticationSchemes = "UserAuth")]
        [HttpPost]
        public async Task<IActionResult> Checkout([FromBody] CheckoutViewModel model)
        {
            if (model == null)
            {
                return Json(new { success = false, message = "Dữ liệu thanh toán không hợp lệ!" });
            }

            var userId = await GetUserIdAsync();

            if (userId == 0)
            {
                Console.WriteLine("[DEBUG] UserId = 0, yêu cầu đăng nhập");
                return Json(new { success = false, message = "Vui lòng đăng nhập để thanh toán!" });
            }

            var cartItems = await ResolveCheckoutItemsAsync(userId, model.SelectedProductIds);

            if (cartItems == null || !cartItems.Any())
            {
                Console.WriteLine("[DEBUG] Giỏ hàng trống hoặc không có sản phẩm được chọn");
                return Json(new { success = false, message = model.SelectedProductIds != null && model.SelectedProductIds.Any() ? "Không có sản phẩm được chọn để thanh toán!" : "Giỏ hàng của bạn đang trống!" });
            }

            model.Name = model.Name?.Trim();
            model.ContactNo = model.ContactNo?.Trim();
            model.City = model.City?.Trim();
            model.Ward = model.Ward?.Trim();
            model.Address = model.Address?.Trim();

            if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.ContactNo) ||
                string.IsNullOrWhiteSpace(model.City) || string.IsNullOrWhiteSpace(model.Ward) ||
                string.IsNullOrWhiteSpace(model.Address))
            {
                Console.WriteLine("[DEBUG] Thiếu thông tin giao hàng");
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin giao hàng!" });
            }

            if (!Regex.IsMatch(model.ContactNo, @"^(0|\+84)(3|5|7|8|9)[0-9]{8}$"))
            {
                return Json(new { success = false, message = "Số điện thoại không hợp lệ!" });
            }

            // Filter selected items
            if (model.SelectedProductIds != null && model.SelectedProductIds.Any())
            {
                cartItems = cartItems.Where(c => model.SelectedProductIds.Contains(c.ProductId)).ToList();
            }

            if (!cartItems.Any())
            {
                return Json(new { success = false, message = "Không có sản phẩm được chọn để thanh toán!" });
            }

            var errors = new List<object>();
            foreach (var item in cartItems)
            {
                var product = await _context.Product.FindAsync(item.ProductId);
                if (product == null)
                {
                    errors.Add(new
                    {
                        productId = item.ProductId,
                        message = $"Sản phẩm ID {item.ProductId} không tồn tại!"
                    });
                    continue;
                }

                if (item.Quantity > product.quantity)
                {
                    errors.Add(new
                    {
                        productId = item.ProductId,
                        message = $"Sản phẩm {product.ProductName} chỉ còn {product.quantity}"
                    });
                }
            }

            if (errors.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Có sản phẩm vượt quá số lượng",
                    errors = errors
                });
            }

            var normalizedPaymentMethod = NormalizePaymentMethod(model.PaymentMethod);
            if (string.IsNullOrEmpty(normalizedPaymentMethod))
            {
                return Json(new { success = false, message = "Phương thức thanh toán không hợp lệ!" });
            }

            if (normalizedPaymentMethod == "VNPAY")
            {
                var pendingItems = cartItems
                    .Select(item => new PendingCheckoutItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.ProductPrice
                    })
                    .ToList();

                if (pendingItems.Count == 0)
                {
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm hợp lệ trong giỏ hàng!" });
                }

                var totalPrice = ConvertToVnPayAmount(pendingItems.Sum(item => item.UnitPrice * item.Quantity));
                var transactionRef = $"{userId}{DateTimeHelper.Now:yyyyMMddHHmmssfff}";

                var pendingCheckout = new PendingCheckoutData
                {
                    UserId = userId,
                    Name = model.Name,
                    ContactNo = model.ContactNo,
                    City = model.City,
                    Ward = model.Ward,
                    Address = model.Address,
                    TransactionRef = transactionRef,
                    TotalPrice = totalPrice,
                    Items = pendingItems
                };

                await _pendingCheckoutStore.SaveAsync(pendingCheckout);

                var orderInfo = $"Thanh toan don hang {transactionRef}";
                var paymentUrl = _vnPayService.CreatePaymentUrl(HttpContext, totalPrice, orderInfo, transactionRef);

                return Json(new { success = true, message = "Đang chuyển đến cổng thanh toán VNPay...", redirectUrl = paymentUrl });
            }

            var checkoutResult = await CreateOrdersFromCartAsync(userId, model, cartItems, "COD");
            if (!checkoutResult.Success)
            {
                return Json(new { success = false, message = checkoutResult.Message });
            }

            Console.WriteLine("[DEBUG] Thanh toán COD thành công");
            return Json(new { success = true, message = "Đặt hàng thành công!", redirectUrl = Url.Action("OrderConfirmation") });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> VnPayReturn()
        {
            var userId = await GetUserIdAsync();
            var successReturnUrl = Url.Action("OrderConfirmation", "Cart", new { paymentResult = "success" }) ?? "/Cart/OrderConfirmation?paymentResult=success";

            var paymentResult = _vnPayService.ProcessReturn(Request.Query);
            if (!paymentResult.IsValidSignature)
            {
                TempData["Error"] = "Chữ ký VNPay không hợp lệ. Vui lòng thử lại.";
                if (userId == 0)
                {
                    return RedirectToAction("Login", "UserAuth", new { returnUrl = Url.Action("Checkout", "Cart") });
                }

                return RedirectToAction("Checkout");
            }

            if (!paymentResult.IsSuccess)
            {
                TempData["Error"] = "Thanh toán VNPay không thành công hoặc đã bị hủy.";
                if (userId == 0)
                {
                    return RedirectToAction("Login", "UserAuth", new { returnUrl = Url.Action("Checkout", "Cart") });
                }

                return RedirectToAction("Checkout");
            }

            var pendingCheckout = await _pendingCheckoutStore.GetAsync(paymentResult.TransactionRef);
            if (pendingCheckout == null)
            {
                if (userId == 0)
                {
                    TempData["Error"] = "Không tìm thấy phiên thanh toán VNPay.";
                    return RedirectToAction("Login", "UserAuth", new { returnUrl = successReturnUrl });
                }

                TempData["Success"] = "Thanh toán VNPay thành công! Đơn hàng đã được cập nhật.";
                return RedirectToAction("OrderConfirmation", new { paymentResult = "success" });
            }

            if ((userId != 0 && pendingCheckout.UserId != userId)
                || !string.Equals(pendingCheckout.TransactionRef, paymentResult.TransactionRef, StringComparison.Ordinal)
                || pendingCheckout.TotalPrice != paymentResult.Amount)
            {
                TempData["Error"] = "Thông tin thanh toán VNPay không khớp.";
                return RedirectToAction("Checkout");
            }

            if (!pendingCheckout.IsProcessed)
            {
                var checkoutResult = await CreateOrdersFromPendingAsync(pendingCheckout, "VNPAY", "Paid");
                if (!checkoutResult.Success)
                {
                    TempData["Error"] = checkoutResult.Message;
                    return RedirectToAction("Checkout");
                }

                await _pendingCheckoutStore.MarkProcessedAsync(pendingCheckout.TransactionRef);
            }

            await _pendingCheckoutStore.RemoveAsync(pendingCheckout.TransactionRef);
            TempData["Success"] = "Thanh toán VNPay thành công! Đơn hàng đã được cập nhật.";

            if (userId == 0)
            {
                return RedirectToAction("Login", "UserAuth", new { returnUrl = successReturnUrl });
            }

            return RedirectToAction("OrderConfirmation", new { paymentResult = "success" });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> VnPayIpn()
        {
            var paymentResult = _vnPayService.ProcessReturn(Request.Query);
            if (!paymentResult.IsValidSignature)
            {
                return Json(new { RspCode = "97", Message = "Invalid signature" });
            }

            var pendingCheckout = await _pendingCheckoutStore.GetAsync(paymentResult.TransactionRef);
            if (pendingCheckout == null)
            {
                return Json(new { RspCode = "01", Message = "Order not found" });
            }

            if (pendingCheckout.TotalPrice != paymentResult.Amount)
            {
                return Json(new { RspCode = "04", Message = "Invalid amount" });
            }

            if (pendingCheckout.IsProcessed)
            {
                return Json(new { RspCode = "02", Message = "Order already confirmed" });
            }

            if (!paymentResult.IsSuccess)
            {
                await _pendingCheckoutStore.RemoveAsync(pendingCheckout.TransactionRef);
                return Json(new { RspCode = "00", Message = "Payment failed" });
            }

            var checkoutResult = await CreateOrdersFromPendingAsync(pendingCheckout, "VNPAY", "Paid");
            if (!checkoutResult.Success)
            {
                return Json(new { RspCode = "99", Message = checkoutResult.Message });
            }

            await _pendingCheckoutStore.MarkProcessedAsync(pendingCheckout.TransactionRef);
            return Json(new { RspCode = "00", Message = "Confirm Success" });
        }

        private async Task<(bool Success, string Message)> CreateOrdersFromCartAsync(
            int userId,
            CheckoutViewModel model,
            List<LapTopBD.Models.ViewModels.CartItem> cartItems,
            string paymentMethod)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate checkout data
                if (string.IsNullOrWhiteSpace(model.City) || model.City.StartsWith("--") ||
                    string.IsNullOrWhiteSpace(model.Ward) || model.Ward.StartsWith("--") ||
                    string.IsNullOrWhiteSpace(model.Address) ||
                    string.IsNullOrWhiteSpace(model.Name) ||
                    string.IsNullOrWhiteSpace(model.ContactNo))
                {
                    return (false, "Vui lòng nhập đầy đủ và chính xác thông tin giao hàng!");
                }

                foreach (var item in cartItems)
                {
                    var product = await _context.Product.FindAsync(item.ProductId);
                    if (product == null)
                    {
                        return (false, $"Sản phẩm ID {item.ProductId} không tồn tại!");
                    }

                    if (item.Quantity > product.quantity)
                    {
                        return (false, $"Sản phẩm {product.ProductName ?? "Unknown"} chỉ còn {product.quantity}");
                    }

                    var order = new Order
                    {
                        City = model.City,
                        Ward = model.Ward,
                        Address = model.Address,
                        UserId = userId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        OrderDate = DateTimeHelper.Now,
                        OrderStatus = "Pending",
                        PaymentMethod = paymentMethod,
                        TotalPrice = product.ProductPrice * item.Quantity
                    };

                    _context.Order.Add(order);
                    product.quantity -= item.Quantity;
                }

                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.Name = model.Name?.Trim() ?? user.Name;
                    user.ContactNo = model.ContactNo?.Trim() ?? user.ContactNo;
                    user.City = model.City?.Trim() ?? user.City;
                    user.Ward = model.Ward?.Trim() ?? user.Ward;
                    user.Address = model.Address?.Trim() ?? user.Address;
                    user.UpdationDate = DateTimeHelper.Now;
                    _context.Users.Update(user);
                }

                var productIds = cartItems.Select(item => item.ProductId).ToList();
                var cartItemsToRemove = await _context.CartItems
                    .Where(c => c.UserId == userId && productIds.Contains(c.ProductId))
                    .ToListAsync();

                if (cartItemsToRemove.Count > 0)
                {
                    _context.CartItems.RemoveRange(cartItemsToRemove);
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine("[DEBUG] CreateOrdersFromCartAsync completed successfully");
                return (true, "OK");
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                Console.WriteLine($"[ERROR] DbUpdateException: {innerMessage}");
                return (false, $"Lỗi cơ sở dữ liệu: {innerMessage}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[ERROR] Checkout error: {ex.GetType().Name} - {ex.Message}");
                return (false, $"Lỗi khi tạo đơn hàng: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> CreateOrdersFromPendingAsync(
            PendingCheckoutData pendingCheckout,
            string paymentMethod,
            string orderStatus)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (pendingCheckout.Items.Count == 0)
                {
                    return (false, "Không có sản phẩm để tạo đơn hàng.");
                }

                // Validate address data
                if (string.IsNullOrWhiteSpace(pendingCheckout.City) || pendingCheckout.City.StartsWith("--") ||
                    string.IsNullOrWhiteSpace(pendingCheckout.Ward) || pendingCheckout.Ward.StartsWith("--") ||
                    string.IsNullOrWhiteSpace(pendingCheckout.Address) ||
                    string.IsNullOrWhiteSpace(pendingCheckout.Name) ||
                    string.IsNullOrWhiteSpace(pendingCheckout.ContactNo))
                {
                    return (false, "Thông tin giao hàng không hợp lệ.");
                }

                foreach (var item in pendingCheckout.Items)
                {
                    var product = await _context.Product.FindAsync(item.ProductId);

                    if (product == null)
                    {
                        return (false, $"Sản phẩm ID {item.ProductId} không tồn tại!");
                    }

                    if (product.quantity < item.Quantity)
                    {
                        return (false, $"Sản phẩm {product.ProductName} chỉ còn {product.quantity}");
                    }

                    // Tạo order
                    var order = new Order
                    {
                        City = pendingCheckout.City,
                        Ward = pendingCheckout.Ward,
                        Address = pendingCheckout.Address,
                        UserId = pendingCheckout.UserId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        OrderDate = DateTimeHelper.Now,
                        OrderStatus = orderStatus,
                        PaymentMethod = paymentMethod,
                        TotalPrice = item.UnitPrice * item.Quantity
                    };

                    _context.Order.Add(order);

                    // TRỪ KHO
                    product.quantity -= item.Quantity;
                }

                var user = await _context.Users.FindAsync(pendingCheckout.UserId);
                if (user != null)
                {
                    user.Name = pendingCheckout.Name?.Trim() ?? user.Name;
                    user.ContactNo = pendingCheckout.ContactNo?.Trim() ?? user.ContactNo;
                    user.City = pendingCheckout.City?.Trim() ?? user.City;
                    user.Ward = pendingCheckout.Ward?.Trim() ?? user.Ward;
                    user.Address = pendingCheckout.Address?.Trim() ?? user.Address;
                    user.UpdationDate = DateTimeHelper.Now;
                    _context.Users.Update(user);
                }

                var productIds = pendingCheckout.Items.Select(x => x.ProductId).ToList();
                var cartItems = await _context.CartItems
                    .Where(c => c.UserId == pendingCheckout.UserId && productIds.Contains(c.ProductId))
                    .ToListAsync();

                if (cartItems.Count > 0)
                {
                    _context.CartItems.RemoveRange(cartItems);
                }

                // Lưu DB
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine("[DEBUG] CreateOrdersFromPendingAsync completed successfully");
                return (true, "OK");
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                Console.WriteLine($"[ERROR] DbUpdateException in VNPay: {innerMessage}");
                return (false, $"Lỗi cơ sở dữ liệu: {innerMessage}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[ERROR] CreateOrdersFromPendingAsync error: {ex.GetType().Name} - {ex.Message}");
                return (false, $"Lỗi khi tạo đơn hàng: {ex.Message}");
            }
        }

        private static long ConvertToVnPayAmount(decimal amount)
        {
            return (long)Math.Round(amount, MidpointRounding.AwayFromZero);
        }

        private static string NormalizePaymentMethod(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
            {
                return string.Empty;
            }

            var normalized = paymentMethod.Trim().ToUpperInvariant();
            if (normalized == "ONLINE")
            {
                return "VNPAY";
            }

            return normalized is "COD" or "VNPAY" ? normalized : string.Empty;
        }

        private async Task<List<LapTopBD.Models.ViewModels.CartItem>> ResolveCheckoutItemsAsync(int userId, IEnumerable<int>? selectedProductIds)
        {
            var cartItems = await _context.CartItems
                .AsNoTracking()
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .Select(c => new LapTopBD.Models.ViewModels.CartItem
                {
                    ProductId = c.ProductId,
                    ProductName = c.Product != null ? c.Product.ProductName : string.Empty,
                    ProductPrice = c.Product != null ? c.Product.ProductPrice : 0,
                    Quantity = c.Quantity,
                    ProductImage = c.Product != null ? c.Product.ProductImage1 : string.Empty
                })
                .ToListAsync();

            var selectedIds = selectedProductIds?.Distinct().ToList() ?? new List<int>();
            if (selectedIds.Count == 0)
            {
                return cartItems;
            }

            var cartByProductId = cartItems.ToDictionary(item => item.ProductId);
            var resolvedItems = new List<LapTopBD.Models.ViewModels.CartItem>();
            var missingIds = new List<int>();

            foreach (var productId in selectedIds)
            {
                if (cartByProductId.TryGetValue(productId, out var cartItem))
                {
                    resolvedItems.Add(cartItem);
                }
                else
                {
                    missingIds.Add(productId);
                }
            }

            if (missingIds.Count > 0)
            {
                var products = await _context.Product
                    .AsNoTracking()
                    .Where(p => missingIds.Contains(p.Id))
                    .Select(p => new
                    {
                        p.Id,
                        p.ProductName,
                        p.ProductPrice,
                        p.ProductImage1
                    })
                    .ToListAsync();

                var productById = products.ToDictionary(p => p.Id);
                foreach (var productId in missingIds)
                {
                    if (productById.TryGetValue(productId, out var product))
                    {
                        resolvedItems.Add(new LapTopBD.Models.ViewModels.CartItem
                        {
                            ProductId = product.Id,
                            ProductName = product.ProductName ?? string.Empty,
                            ProductPrice = product.ProductPrice,
                            Quantity = 1,
                            ProductImage = product.ProductImage1 ?? string.Empty
                        });
                    }
                }
            }

            return resolvedItems;
        }

        // Action OrderConfirmation
        [Authorize(AuthenticationSchemes = "UserAuth")]
        [HttpGet]
        public async Task<IActionResult> OrderConfirmation(string? paymentResult = null)
        {
            var userId = await GetUserIdAsync();
            if (userId == 0)
            {
                return RedirectToAction("Login", "UserAuth");
            }

            if (string.Equals(paymentResult, "success", StringComparison.OrdinalIgnoreCase)
                && TempData["Success"] == null)
            {
                TempData["Success"] = "Thanh toán VNPay thành công! Đơn hàng đã được cập nhật.";
            }

            // Lấy đơn hàng mới nhất của user
            var orders = await _context.Order
                .Include(o => o.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            ViewBag.ShowBanner = false;
            return View(orders);
        }

        [Authorize(AuthenticationSchemes = "UserAuth")]
        [HttpPost]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetUserIdAsync();
                if (userId == 0)
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập để hủy đơn hàng!" });
                }

                var order = await _context.Order
                    .Include(o => o.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Đơn hàng không tồn tại!" });
                }

                // Chỉ cho hủy khi trạng thái là Pending
                if (order.OrderStatus != "Pending")
                {
                    return Json(new { success = false, message = $"Không thể hủy đơn hàng với trạng thái {order.OrderStatus}!" });
                }

                // Khôi phục lại số lượng sản phẩm
                if (order.Product != null)
                {
                    order.Product.quantity += order.Quantity;
                    _context.Product.Update(order.Product);
                }

                order.OrderStatus = "Cancelled";
                _context.Order.Update(order);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"[SUCCESS] Order {orderId} cancelled by user {userId}");
                return Json(new { success = true, message = "Đơn hàng đã được hủy thành công!" });
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                Console.WriteLine($"[ERROR] DbUpdateException: {innerMessage}");
                return Json(new { success = false, message = $"Lỗi cơ sở dữ liệu: {innerMessage}" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[ERROR] Exception: {ex.GetType().Name} - {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        private async Task<int> GetUserIdAsync()
        {
            var authenticateResult = await HttpContext.AuthenticateAsync("UserAuth");

            if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
            {
                return 0;
            }

            var userIdClaim = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return 0;
            }

            return userId;
        }
    }
}
