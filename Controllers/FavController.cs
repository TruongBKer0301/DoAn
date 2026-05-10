using LapTopBD.Data;
using LapTopBD.Models;
using LapTopBD.Utilities;
using LapTopBD.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LapTopBD.Controllers
{
    public class FavController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FavController(ApplicationDbContext context)
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

            var products = await (
                from p in _context.Product
                join w in _context.Wishlist on p.Id equals w.ProductId
                where w.UserId == userId
                join sc in _context.SubCategories on p.SubCategoryId equals sc.Id into subCatGroup
                from sc in subCatGroup.DefaultIfEmpty()
                join c in _context.Categories on p.CategoryId equals c.Id into catGroup
                from c in catGroup.DefaultIfEmpty()
                select new ProductViewModel
                {
                    Id = p.Id,
                    AdminId = p.AdminId,
                    CategoryId = p.CategoryId,
                    CategoryName = c.CategoryName,
                    SubCategoryId = p.SubCategoryId,
                    SubCategoryName = sc != null ? sc.SubCategoryName : null,
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
                    AverageRating = p.ProductReviews.Any()
                        ? p.ProductReviews.Average(pr => pr.Rating)
                        : 0
                }
            ).ToListAsync();

            ViewBag.ShowBanner = false;
            return View(products);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddToFav(int productId)
        {
            try
            {
                var userId = await GetUserIdAsync();
                if (userId == 0)
                    return Json(new { success = false, message = "Vui lòng đăng nhập!" });

                var existing = await _context.Wishlist
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

                if (existing != null)
                {
                    int currentCount = await _context.Wishlist
                        .CountAsync(c => c.UserId == userId);
                    return Json(new
                    {
                        success = true,
                        message = "Sản phẩm đã có trong danh sách yêu thích!",
                        wishlistcount = currentCount
                    });
                }

                var productExists = await _context.Product
                    .AnyAsync(p => p.Id == productId);
                if (!productExists)
                    return Json(new { success = false, message = "Sản phẩm không tồn tại!" });

                var wishlist = new Wishlist
                {
                    UserId = userId,
                    ProductId = productId,
                    PostingDate = DateTimeHelper.Now
                };

                _context.Wishlist.Add(wishlist);
                await _context.SaveChangesAsync();

                int newCount = await _context.Wishlist
                    .CountAsync(c => c.UserId == userId);

                return Json(new
                {
                    success = true,
                    message = "Đã thêm vào yêu thích!",
                    wishlistcount = newCount
                });
            }
            catch (DbUpdateException ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Lỗi DB: " + (ex.InnerException?.Message ?? ex.Message)
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Lỗi: " + (ex.InnerException?.Message ?? ex.Message)
                });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RemoveFromFav(int productid)
        {
            try
            {
                var userId = await GetUserIdAsync();

                var wishlist = await _context.Wishlist
                    .FirstOrDefaultAsync(c => c.ProductId == productid && c.UserId == userId);

                if (wishlist == null)
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm trong mục yêu thích!" });

                _context.Wishlist.Remove(wishlist);
                await _context.SaveChangesAsync();

                int newCount = await _context.Wishlist
                    .CountAsync(c => c.UserId == userId);

                return Json(new
                {
                    success = true,
                    message = "Đã xóa khỏi mục yêu thích!",
                    wishlistcount = newCount
                });
            }
            catch (DbUpdateException ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Lỗi DB: " + (ex.InnerException?.Message ?? ex.Message)
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Lỗi: " + (ex.InnerException?.Message ?? ex.Message)
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFavProductIds()
        {
            var userId = await GetUserIdAsync();
            if (userId == 0)
                return Json(new { success = false, productIds = Array.Empty<int>() });

            var productIds = await _context.Wishlist
                .Where(c => c.UserId == userId)
                .Select(c => c.ProductId)
                .ToListAsync();

            return Json(new { success = true, productIds });
        }

        [HttpGet]
        public async Task<IActionResult> GetFavCount()
        {
            var userId = await GetUserIdAsync();
            if (userId == 0)
                return Json(new { success = false, wishlistcount = 0 });

            int wishlistcount = await _context.Wishlist
                .Where(c => c.UserId == userId)
                .CountAsync();

            return Json(new { success = true, wishlistcount });
        }

        private async Task<int> GetUserIdAsync()
        {
            var authenticateResult = await HttpContext.AuthenticateAsync("UserAuth");

            if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
                return 0;

            var userIdClaim = authenticateResult.Principal
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return 0;

            return userId;
        }
    }
}