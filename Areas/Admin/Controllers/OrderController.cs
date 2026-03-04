using LapTopBD.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            var orders = await _context.Orders
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
                    o.TotalPrice
                })
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        [HttpGet]
        [Route("get-new-orders-count")]
        public async Task<IActionResult> GetNewOrdersCount()
        {
            try
            {
                // Lấy thời gian hiện tại theo múi giờ UTC
                var nowUtc = DateTime.UtcNow;

                // Tính ngày bắt đầu (3 ngày trước, 00:00 UTC)
                var startDate = nowUtc.Date.AddDays(-2); // Hôm nay - 2 ngày = hôm kia

                // Tính ngày kết thúc (hôm nay, 23:59:59 UTC)
                var endDate = nowUtc.Date.AddDays(1).AddTicks(-1); // Cuối ngày hôm nay

                // Đếm số lượng đơn hàng trong 3 ngày gần nhất, không tính đơn hàng đã hủy
                var newOrdersCount = await _context.Orders
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
            try
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return Json(new { success = false, message = "Đơn hàng không tồn tại!" });
                }

                // Kiểm tra trạng thái hợp lệ
                var validStatuses = new[] { "Pending", "Shipped", "Delivered", "Cancelled" };
                if (!validStatuses.Contains(status))
                {
                    return Json(new { success = false, message = "Trạng thái không hợp lệ!" });
                }

                // Cập nhật trạng thái
                order.OrderStatus = status;
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Cập nhật trạng thái đơn hàng thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi cập nhật trạng thái: " + ex.Message });
            }
        }
    }
}