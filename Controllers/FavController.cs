using LapTopBD.Data;
using LapTopBD.Models;
using LapTopBD.Models.ViewModels;
using LapTopBD.Utilities;
using LapTopBD.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LapTopBD.Controllers
{
    public class FavController : Controller
    {
        private readonly ApplicationDbContext _context;


        public FavController(
            ApplicationDbContext context)
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

            var wishlist = await _context.Wishlist
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();
            var products = await (from p in _context.Product
                                      // Join với Wishlists và lọc ngay theo UserId
                                  join w in _context.Wishlist on p.Id equals w.ProductId
                                  where w.UserId == userId

                                  // Join với SubCategories (Left Join)
                                  join sc in _context.SubCategories on p.SubCategoryId equals sc.Id into subCatGroup
                                  from sc in subCatGroup.DefaultIfEmpty()

                                      // Join với Categories để lấy CategoryName (Nếu Wishlist không có sẵn CategoryName)
                                  join c in _context.Categories on p.CategoryId equals c.Id into catGroup
                                  from c in catGroup.DefaultIfEmpty()
                                  select new ProductViewModel
                           {
                               Id = p.Id,
                               AdminId = p.AdminId, // Giữ AdminId nếu cần cho logic khác
                               CategoryId = p.CategoryId,
                               CategoryName = c.CategoryName, // Chỉ lấy CategoryName
                               SubCategoryId = p.SubCategoryId,
                               SubCategoryName = sc != null ? sc.SubCategoryName : null, // Lấy SubCategoryName nếu có
                               ProductName = p.ProductName,
                               ProductPrice = p.ProductPrice,
                               ProductPriceBeforeDiscount = p.ProductPriceBeforeDiscount,
                               ProductDescription = p.ProductDescription,
                               ProductImage1 = p.ProductImage1,
                               ProductImage2 = p.ProductImage2,
                               ProductImage3 = p.ProductImage3,
                               quantity = p.quantity,
                               ShippingCharge = p.ShippingCharge,
                               PostingDate = p.PostingDate,
                               UpdationDate = p.UpdationDate,
                               Brand = p.Brand,
                               CPU = p.CPU,
                               RAM = p.RAM,
                               Storage = p.Storage,
                               GPU = p.GPU,
                               VGA = p.VGA,
                               Promotion = p.Promotion,
                               Slug = p.Slug,
                               AverageRating = p.ProductReviews.Any() ? p.ProductReviews.Average(pr => pr.Rating) : 0
                           }).ToListAsync();
            ViewBag.ShowBanner = false;
            return View(products);
        }

        public async Task<IActionResult> AddToFav(int productId)
        {
            var userId = await GetUserIdAsync();
            if (userId == 0) return Json(new { success = false, message = "Vui lòng đăng nhập!" });

            var existing = await _context.Wishlist
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

            if (existing != null)
            {
                return Json(new { success = false, message = "Sản phẩm đã có sẵn trong danh sách ưa thích!" });
            }

            var wishlist = new LapTopBD.Models.Wishlist
            {
                UserId = userId,
                ProductId = productId,
                PostingDate = DateTimeHelper.Now
            };

            _context.Wishlist.Add(wishlist);
            await _context.SaveChangesAsync();

            // Lấy số lượng mới ngay tại đây
            int newCount = await _context.Wishlist.CountAsync(c => c.UserId == userId);

            return Json(new
            {
                success = true,
                message = "Đã thêm vào ưa thích!",
                wishlistcount = newCount // Trả về số lượng mới để JS cập nhật luôn
            });
        }

        //Số lượng sản phẩm trong giỏ hàng
        [HttpGet]
        public async Task<IActionResult> GetFavCount()
        {
            var userId = await GetUserIdAsync();
            if (userId == 0)
            {
                return Json(new { success = false, wishlistcount = 0 });
            }

            int wishlistcount = await _context.Wishlist
                .Where(c => c.UserId == userId)
                .CountAsync();

            return Json(new { success = true, wishlistcount });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromFav(int productid)
        {
            var userId = await GetUserIdAsync();
            var wishlist = await _context.Wishlist
                .FirstOrDefaultAsync(c => c.ProductId == productid && c.UserId == userId);

            if (wishlist == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm trong mục ưa thích!" });
            }

            _context.Wishlist.Remove(wishlist);
            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã xóa khỏi mục ưa thích!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi xóa khỏi mục ưa thích: {ex.Message}" });
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
