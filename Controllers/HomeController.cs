using LapTopBD.Data;
using LapTopBD.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LapTopBD.Utilities; 
using Microsoft.AspNetCore.Authentication;
using LapTopBD.Models;
using System.Security.Claims;

namespace LapTopBD.Controllers
{
 
   
    [Route("user")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;
        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [Route("/")]
        public async Task<IActionResult> Index()
        {
            var result = await HttpContext.AuthenticateAsync("UserAuth");
            if (result?.Succeeded == true)
            {
               
                HttpContext.User = result.Principal;
            }
          
            var newProducts = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .OrderByDescending(p => p.PostingDate)
                .Take(7)
                .Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    ProductName = p.ProductName,
                    ProductImage1 = p.ProductImage1,
                    ProductPrice = p.ProductPrice,
                    ProductPriceBeforeDiscount = p.ProductPriceBeforeDiscount,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.CategoryName : string.Empty,
                    SubCategoryId = p.SubCategoryId,
                    SubCategoryName = p.SubCategory != null ? p.SubCategory.SubCategoryName : null,
                    ProductAvailability = p.ProductAvailability ? "Còn hàng" : "Hết hàng",
                    Slug = p.Slug
                })
                .ToListAsync();

            var hotDeals = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Where(p => p.ProductPriceBeforeDiscount.HasValue && p.ProductPriceBeforeDiscount > p.ProductPrice)
                .OrderByDescending(p => p.ProductPriceBeforeDiscount.HasValue
                    ? (double)(p.ProductPriceBeforeDiscount.Value - p.ProductPrice) / (double)p.ProductPriceBeforeDiscount.Value
                    : 0)
                .Take(5)
                .Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    ProductName = p.ProductName,
                    ProductImage1 = p.ProductImage1,
                    ProductPrice = p.ProductPrice,
                    ProductPriceBeforeDiscount = p.ProductPriceBeforeDiscount,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.CategoryName : string.Empty,
                    SubCategoryId = p.SubCategoryId,
                    SubCategoryName = p.SubCategory != null ? p.SubCategory.SubCategoryName : null,
                    ProductAvailability = p.ProductAvailability ? "Còn hàng" : "Hết hàng",
                    Slug = p.Slug
                })
                .ToListAsync();

            ViewBag.NewProducts = newProducts;
            ViewBag.HotDeals = hotDeals ?? new List<ProductViewModel>();
            ViewBag.ShowBanner = true;

            return View();
        }

        [Route("Detail/{slug}")]
        public async Task<IActionResult> Detail(string slug, int page = 1)
        {
            var result = await HttpContext.AuthenticateAsync("UserAuth");
            if (result?.Succeeded == true)
            {
                HttpContext.User = result.Principal;
            }

            if (string.IsNullOrEmpty(slug))
            {
                return NotFound("Slug không hợp lệ.");
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.ProductReviews)
                .Where(p => p.Slug == slug)
                .Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    Slug = p.Slug,
                    ProductName = p.ProductName,
                    ProductImage1 = p.ProductImage1,
                    ProductImage2 = p.ProductImage2,
                    ProductImage3 = p.ProductImage3,
                    ProductPrice = p.ProductPrice,
                    ProductPriceBeforeDiscount = p.ProductPriceBeforeDiscount,
                    ProductDescription = p.ProductDescription,
                    Brand = p.Brand,
                    CPU = p.CPU,
                    RAM = p.RAM,
                    Storage = p.Storage,
                    GPU = p.GPU,
                    VGA = p.VGA,
                    Promotion = p.Promotion,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.CategoryName : string.Empty,
                    SubCategoryId = p.SubCategoryId,
                    SubCategoryName = p.SubCategory != null ? p.SubCategory.SubCategoryName : null,
                    ProductAvailability = p.ProductAvailability ? "Còn hàng" : "Hết hàng",
                    Reviews = p.ProductReviews
                        .OrderByDescending(pr => pr.ReviewDate) 
                        .ToList(), 
                    TotalReviews = p.ProductReviews.Count, 
                    AverageRating = p.ProductReviews.Any() ? p.ProductReviews.Average(pr => pr.Rating) : 0 
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound("Sản phẩm không tồn tại.");
            }

            var relatedProducts = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
                .Take(5)
                .Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    ProductName = p.ProductName,
                    ProductImage1 = p.ProductImage1,
                    ProductPrice = p.ProductPrice,
                    ProductPriceBeforeDiscount = p.ProductPriceBeforeDiscount,
                    ProductAvailability = p.ProductAvailability ? "Còn hàng" : "Hết hàng",
                    Slug = p.Slug
                })
                .ToListAsync();

            ViewBag.ShowBanner = false;
            ViewBag.RelatedProducts = relatedProducts;
            ViewBag.TotalReviews = product.TotalReviews;

            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> UpdateAllSlugs()
        {
            var products = await _context.Products.ToListAsync();
            foreach (var product in products)
            {
                product.Slug = SlugHelper.GenerateSlug((product.ProductName ?? ""));
            }
            await _context.SaveChangesAsync();
            return Content("Slugs updated successfully");
        }

        [HttpPost]
        [Route("SubmitReview")]
        public async Task<IActionResult> SubmitReview(ProductReviewViewModel reviewModel)
        {
            // Lấy sản phẩm để kiểm tra hợp lệ
            var product = await _context.Products.FindAsync(reviewModel.ProductId);
            if (product == null)
            {
                return NotFound("Sản phẩm không tồn tại.");
            }

            // Add this authentication code at the beginning
            var authResult = await HttpContext.AuthenticateAsync("UserAuth");
            if (authResult?.Succeeded == true)
            {
                HttpContext.User = authResult.Principal;
            }

            // Now check if authenticated
            if (User?.Identity?.IsAuthenticated != true)
            {
                TempData["ErrorMessage"] = "Bạn cần đăng nhập để gửi đánh giá.";

                // Get the product for redirection
                var products = await _context.Products.FindAsync(reviewModel.ProductId);
                return RedirectToAction("Detail", new { slug = product?.Slug });
            }

            // Lấy thông tin người dùng từ Claims
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = User.Identity?.Name;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            // Kiểm tra dữ liệu hợp lệ
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin đánh giá.";
                return RedirectToAction("Detail", new { slug = product.Slug });
            }

            // Tạo đánh giá mới
            var productReview = new ProductReview
            {
                ProductId = reviewModel.ProductId,
                Rating = reviewModel.Rating,
                Review = reviewModel.Review ?? string.Empty,
                ReviewDate = DateTime.Now,
                UserId = userId,
                UserName = username,
                Email = email
            };

            try
            {
                _context.ProductReviews.Add(productReview);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đánh giá của bạn đã được gửi thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError("Lỗi khi lưu đánh giá: {0}", ex.ToString());
                return StatusCode(500, "Lỗi server: " + ex.InnerException?.Message);
            }

            // Chuyển hướng về trang chi tiết sản phẩm
            return RedirectToAction("Detail", new { slug = product.Slug });
        }

    }
}