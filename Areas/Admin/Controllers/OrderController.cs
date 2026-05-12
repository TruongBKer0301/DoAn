using LapTopBD.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LapTopBD.Utilities;
using LapTopBD.Models.ViewModels.Admin;

namespace LapTopBD.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin,Seller")]
    [Route("admin/order")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("list-orders")]
        public async Task<IActionResult> ListOrders()
        {
            var orders = await _context.Order
                .Include(o => o.User)
                .Include(o => o.Product)
                .Select(o => new
                {
                    o.Id,
                    UserName = o.User != null ? o.User.Name : "Không xác định",
                    ProductName = o.Product != null ? o.Product.ProductName : "Không xác định",
                    o.Quantity,
                    o.OrderDate,
                    o.OrderStatus,
                    o.TotalPrice,
                    o.City,
                    o.Ward,
                    o.Address,
                    o.PaymentMethod
                })
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new OrderViewModel
                {
                    Id = o.Id,
                    UserName = o.UserName,
                    ProductName = o.ProductName,
                    Quantity = o.Quantity,
                    OrderDate = o.OrderDate,
                    OrderStatus = o.OrderStatus,
                    TotalPrice = o.TotalPrice,
                    City = o.City,
                    Ward = o.Ward,
                    Address = o.Address,
                    PaymentMethod = o.PaymentMethod
                })
                .ToListAsync();

            return View(orders);
        }

        [Route("get-new-orders-count")]
        [HttpGet]
        public async Task<IActionResult> GetNewOrdersCount()
        {
            try
            {
                var nowLocal = DateTimeHelper.Now;

                var startDate = nowLocal.Date.AddDays(-2);

                var endDate = nowLocal.Date.AddDays(1).AddTicks(-1);

                // Đếm số lượng đơn hàng trong 3 ngày gần nhất, không tính đơn hàng đã hủy
                var newOrdersCount = await _context.Order
                    .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && o.OrderStatus != "Cancelled")
                    .CountAsync();

                return Json(new { success = true, count = newOrdersCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lấy số lượng đơn hàng: " + ex.Message });
            }
        }

        [HttpPost]
        [Route("update-order-status")]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.Order
                    .Include(o => o.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    Console.WriteLine($"[ERROR] Order not found: {orderId}");
                    return Json(new { success = false, message = "Đơn hàng không tồn tại!" });
                }

                var validStatuses = new[] { "Pending", "Shipping", "Delivered", "Cancelled", "Paid" };

                if (!validStatuses.Contains(status))
                {
                    Console.WriteLine($"[ERROR] Invalid status: {status}");
                    return Json(new { success = false, message = "Trạng thái không hợp lệ!" });
                }

                // Nếu hủy đơn hàng, khôi phục lại số lượng sản phẩm
                if (status == "Cancelled" && order.OrderStatus != "Cancelled")
                {
                    Console.WriteLine($"[INFO] Cancelling order {orderId}, restoring product quantity");
                    if (order.Product != null)
                    {
                        Console.WriteLine($"[INFO] Product ID: {order.ProductId}, Current quantity: {order.Product.quantity}, Quantity to restore: {order.Quantity}");
                        order.Product.quantity += order.Quantity;
                        _context.Product.Update(order.Product);
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING] Product is null for order {orderId}");
                    }
                }

                order.OrderStatus = status;
                _context.Order.Update(order);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"[SUCCESS] Order {orderId} status updated to {status}");
                return Json(new { success = true, message = "Cập nhật trạng thái đơn hàng thành công!" });
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
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }
    }
}