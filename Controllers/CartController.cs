using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LapTopBD.Models;
using System.Security.Claims;
using LapTopBD.Data;
using Microsoft.AspNetCore.Authentication;
using LapTopBD.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using LapTopBD.Utilities;

namespace LapTopBD.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IVnPayService _vnPayService;
        private readonly IPendingCheckoutStore _pendingCheckoutStore;

        public CartController(
            ApplicationDbContext context,
            IVnPayService vnPayService,
            IPendingCheckoutStore pendingCheckoutStore)
        {
            _context = context;
            _vnPayService = vnPayService;
            _pendingCheckoutStore = pendingCheckoutStore;
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

            int cartItemCount = await _context.CartItems
                .Where(c => c.UserId == userId)
                .SumAsync(c => c.Quantity);

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

            int orderCount = await _context.Order
                .Where(o => o.UserId == userId && o.OrderStatus != "Cancelled")
                .CountAsync();

            return Json(new { success = true, orderCount });
        }

        [Authorize(AuthenticationSchemes = "UserAuth")]
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var userId = await GetUserIdAsync();
            if (userId == 0)
            {
                return RedirectToAction("Login", "UserAuth");
            }

            // Lấy thông tin user để điền sẵn
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Login", "UserAuth");
            }

            // Fix for CS8602: Dereference of a possibly null reference.
            var cartItems = await _context.CartItems
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

            if (cartItems == null || !cartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống!";
                return RedirectToAction("Index");
            }

            // Tính tổng tiền
            decimal totalPrice = cartItems.Sum(item => item.Subtotal);

            // Tạo model cho view
            var model = new CheckoutViewModel
            {
                Name = user.Name,
                ContactNo = user.ContactNo,
                City = user.City ?? "",
                District = user.District ?? "",
                Ward = user.Ward ?? "",
                Address = user.Address ?? "",
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
            var userId = await GetUserIdAsync();

            if (userId == 0)
            {
                Console.WriteLine("[DEBUG] UserId = 0, yêu cầu đăng nhập");
                return Json(new { success = false, message = "Vui lòng đăng nhập để thanh toán!" });
            }

            // Lấy giỏ hàng từ bảng CartItems
            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            if (cartItems == null || !cartItems.Any())
            {
                Console.WriteLine("[DEBUG] Giỏ hàng trống");
                return Json(new { success = false, message = "Giỏ hàng của bạn đang trống!" });
            }

            if (string.IsNullOrWhiteSpace(model.City) || string.IsNullOrWhiteSpace(model.District) ||
                string.IsNullOrWhiteSpace(model.Ward) || string.IsNullOrWhiteSpace(model.Address))
            {
                Console.WriteLine("[DEBUG] Thiếu thông tin giao hàng");
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin địa chỉ giao hàng!" });
            }
            var errors = cartItems
                .Where(i => i.Product != null && i.Quantity > i.Product.quantity)
                .Select(i => new
                {
                    productId = i.ProductId,
                    message = $"Sản phẩm {i.Product.ProductName} chỉ còn {i.Product.quantity}"
                })
                .ToList();

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
                    .Where(item => item.Product != null)
                    .Select(item => new PendingCheckoutItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Product!.ProductPrice
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
                    District = model.District,
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
            List<LapTopBD.Models.CartItem> cartItems,
            string paymentMethod)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try {
            foreach (var item in cartItems)
            {
                var product = await _context.Product.FindAsync(item.ProductId);
                if (product == null)
                {
                    return (false, $"Sản phẩm ID {item.ProductId} không tồn tại!");
                }
                    if (item.Quantity > product.quantity)
                    {
                        return (false, $"Sản phẩm {item.Product.ProductName} chỉ còn {product.quantity}");
                    }

                var order = new Order
                {
                    City = model.City,
                    District = model.District,
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
                user.Name = model.Name;
                user.ContactNo = model.ContactNo;
                user.City = model.City;
                user.District = model.District;
                user.Ward = model.Ward;
                user.Address = model.Address;
                user.UpdationDate = DateTimeHelper.Now;
                _context.Users.Update(user);
            }

            _context.CartItems.RemoveRange(cartItems);
            await _context.SaveChangesAsync();
                await transaction.CommitAsync();

            return (true, "OK");
        }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, ex.Message);
            }
        }

        private async Task<(bool Success, string Message)> CreateOrdersFromPendingAsync(
            PendingCheckoutData pendingCheckout,
            string paymentMethod,
            string orderStatus)
        {
            if (pendingCheckout.Items.Count == 0)
            {
                return (false, "Không có sản phẩm để tạo đơn hàng.");
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
                        UserId = pendingCheckout.UserId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        TotalPrice = item.UnitPrice * item.Quantity,

                    City = pendingCheckout.City,
                    District = pendingCheckout.District,
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
                user.Name = pendingCheckout.Name;
                user.ContactNo = pendingCheckout.ContactNo;
                user.City = pendingCheckout.City;
                user.District = pendingCheckout.District;
                user.Ward = pendingCheckout.Ward;
                user.Address = pendingCheckout.Address;
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

            return (true, "OK");
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
                .Take(10) // Lấy 10 đơn hàng gần nhất
                .ToListAsync();
            ViewBag.ShowBanner = false;
            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var userId = await GetUserIdAsync();
            Console.WriteLine($"[DEBUG] CancelOrder - UserId: {userId}, OrderId: {orderId}");

            if (userId == 0)
            {
                Console.WriteLine("[DEBUG] UserId = 0, yêu cầu đăng nhập");
                return Json(new { success = false, message = "Vui lòng đăng nhập để hủy đơn hàng!" });
            }

            // Tìm đơn hàng
            var order = await _context.Order
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
            {
                Console.WriteLine($"[DEBUG] Không tìm thấy đơn hàng - OrderId: {orderId}, UserId: {userId}");
                return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });
            }

            // Kiểm tra trạng thái đơn hàng (không phân biệt hoa thường)
            if (!string.Equals(order.OrderStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[DEBUG] Đơn hàng không thể hủy - OrderStatus: {order.OrderStatus}");
                return Json(new { success = false, message = "Đơn hàng không thể hủy vì không ở trạng thái Pending!" });
            }

            // Cập nhật trạng thái đơn hàng thành "Cancelled"
            order.OrderStatus = "Cancelled";
            _context.Order.Update(order);

            try
            {
                await _context.SaveChangesAsync();
                Console.WriteLine($"[DEBUG] Hủy đơn hàng thành công - OrderId: {orderId}");
                return Json(new { success = true, message = "Đơn hàng đã được hủy thành công!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Lỗi khi hủy đơn hàng - OrderId: {orderId}, Error: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi khi hủy đơn hàng: {ex.Message}" });
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
