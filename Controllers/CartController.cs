using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LapTopBD.Models;
using System.Security.Claims;
using LapTopBD.Data;
using Microsoft.AspNetCore.Authentication;
using LapTopBD.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace LapTopBD.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
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

            var product = await _context.Products.FindAsync(productId);
            if (product == null || !product.ProductAvailability)
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
                    AddedDate = DateTime.UtcNow
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

            if (cartItem.Product == null || !cartItem.Product.ProductAvailability)
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

            int orderCount = await _context.Orders
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
            Console.WriteLine($"[DEBUG] Checkout POST - UserId: {userId}");

            // Kiểm tra dữ liệu nhận được sau khi ánh xạ
            Console.WriteLine($"[DEBUG] Dữ liệu nhận được - Name: '{model.Name}', ContactNo: '{model.ContactNo}', City: '{model.City}', District: '{model.District}', Ward: '{model.Ward}', Address: '{model.Address}', PaymentMethod: '{model.PaymentMethod}'");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("[DEBUG] ModelState không hợp lệ");
                var cartItemsForView = await _context.CartItems
                    .Include(c => c.Product)
                    .Where(c => c.UserId == userId)
                    .Select(c => new LapTopBD.Models.ViewModels.CartItem
                    {
                        ProductId = c.ProductId,
                        ProductName = c.Product.ProductName,
                        ProductPrice = c.Product.ProductPrice,
                        Quantity = c.Quantity
                    })
                    .ToListAsync();

                model.CartItems = cartItemsForView;
                model.TotalPrice = cartItemsForView.Sum(item => item.Subtotal);
                return View(model);
            }

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

            // Kiểm tra thông tin giao hàng
            Console.WriteLine($"[DEBUG] Thông tin giao hàng - City: '{model.City}', District: '{model.District}', Ward: '{model.Ward}', Address: '{model.Address}', PaymentMethod: '{model.PaymentMethod}'");
            if (string.IsNullOrWhiteSpace(model.City) || string.IsNullOrWhiteSpace(model.District) ||
                string.IsNullOrWhiteSpace(model.Ward) || string.IsNullOrWhiteSpace(model.Address))
            {
                Console.WriteLine("[DEBUG] Thiếu thông tin giao hàng");
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin địa chỉ giao hàng!" });
            }

            // Tạo đơn hàng cho từng sản phẩm trong giỏ hàng
            foreach (var item in cartItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null)
                {
                    Console.WriteLine($"[DEBUG] Sản phẩm {item.ProductId} không tồn tại");
                    return Json(new { success = false, message = $"Sản phẩm {item.Product.ProductName} không tồn tại!" });
                }

                var order = new Order
                {
                    UserId = userId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    OrderDate = DateTime.UtcNow,
                    OrderStatus = "Pending",
                    PaymentMethod = model.PaymentMethod,
                    TotalPrice = item.Product.ProductPrice * item.Quantity // Lưu TotalPrice
                };

                _context.Orders.Add(order);
            }

            // Cập nhật thông tin giao hàng của user
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.Name = model.Name;
                user.ContactNo = model.ContactNo;
                user.City = model.City;
                user.District = model.District;
                user.Ward = model.Ward;
                user.Address = model.Address;
                user.UpdationDate = DateTime.UtcNow;
                _context.Users.Update(user);
            }

            // Xóa giỏ hàng sau khi thanh toán thành công
            _context.CartItems.RemoveRange(cartItems);

            await _context.SaveChangesAsync();
            Console.WriteLine("[DEBUG] Thanh toán thành công");

            return Json(new { success = true, message = "Thanh toán thành công!", redirectUrl = Url.Action("OrderConfirmation") });
        }

        // Action OrderConfirmation
        [Authorize(AuthenticationSchemes = "UserAuth")]
        [HttpGet]
        public async Task<IActionResult> OrderConfirmation()
        {
            var userId = await GetUserIdAsync();
            if (userId == 0)
            {
                return RedirectToAction("Login", "UserAuth");
            }

            // Lấy đơn hàng mới nhất của user
            var orders = await _context.Orders
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
            var order = await _context.Orders
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
            _context.Orders.Update(order);

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
