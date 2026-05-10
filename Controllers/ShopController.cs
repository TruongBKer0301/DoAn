using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LapTopBD.ViewModels;
using LapTopBD.Data;
using Microsoft.AspNetCore.Authentication;

namespace LapTopBD.Controllers
{
    public class ShopController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShopController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string categoryId, string subCategoryId, string search, string sortBy)
        {
            var result = await HttpContext.AuthenticateAsync("UserAuth");

            if (result?.Succeeded == true)
            {
                HttpContext.User = result.Principal;
            }

            // ===== DANH MỤC =====
            var categories = await _context.Categories
                .Select(c => new CategoryViewModel
                {
                    Id = c.Id,
                    CategoryName = c.CategoryName,
                    AdminId = c.AdminId
                })
                .ToListAsync();

            ViewBag.Categories = categories;

            // ===== SUB CATEGORY =====
            if (!string.IsNullOrEmpty(categoryId))
            {
                int catId = int.Parse(categoryId);

                var subCategories = await _context.SubCategories
                    .Where(sc => sc.CategoryId == catId)
                    .Select(sc => new SubCategoryViewModel
                    {
                        Id = sc.Id,
                        CategoryId = sc.CategoryId,
                        SubCategoryName = sc.SubCategoryName
                    })
                    .ToListAsync();

                ViewBag.SubCategories = subCategories;
            }

            // ===== VIEWBAG =====
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SelectedSubCategoryId = subCategoryId;
            ViewBag.SearchTerm = search;
            ViewBag.SortBy = sortBy;

            // ===== QUERY PRODUCT =====
            var products = from p in _context.Product
                           join c in _context.Categories
                           on p.CategoryId equals c.Id

                           join sc in _context.SubCategories
                           on p.SubCategoryId equals sc.Id into subCatGroup

                           from sc in subCatGroup.DefaultIfEmpty()

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
                           };

            // ===== FILTER CATEGORY =====
            if (!string.IsNullOrEmpty(categoryId))
            {
                int catId = int.Parse(categoryId);

                products = products.Where(p => p.CategoryId == catId);
            }

            // ===== FILTER SUBCATEGORY =====
            if (!string.IsNullOrEmpty(subCategoryId))
            {
                int subCatId = int.Parse(subCategoryId);

                products = products.Where(p => p.SubCategoryId == subCatId);
            }

            // ===== SMART SEARCH =====
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();

                var keywords = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var keyword in keywords)
                {
                    products = products.Where(p =>

                        // Tên sản phẩm
                        (p.ProductName != null &&
                         p.ProductName.ToLower().Contains(keyword))

                        ||

                        // Brand
                        (p.Brand != null &&
                         p.Brand.ToLower().Contains(keyword))

                        ||

                        // CPU
                        (p.CPU != null &&
                         p.CPU.ToLower().Contains(keyword))

                        ||

                        // RAM
                        (p.RAM != null &&
                         p.RAM.ToLower().Contains(keyword))

                        ||

                        // Storage
                        (p.Storage != null &&
                         p.Storage.ToLower().Contains(keyword))

                        ||

                        // GPU
                        (p.GPU != null &&
                         p.GPU.ToLower().Contains(keyword))

                        ||

                        // VGA
                        (p.VGA != null &&
                         p.VGA.ToLower().Contains(keyword))

                        ||

                        // Category
                        (p.CategoryName != null &&
                         p.CategoryName.ToLower().Contains(keyword))

                        ||

                        // SubCategory
                        (p.SubCategoryName != null &&
                         p.SubCategoryName.ToLower().Contains(keyword))
                    );
                }
            }

            // ===== SORT =====
            switch (sortBy)
            {
                case "price-asc":
                    products = products.OrderBy(p => p.ProductPrice);
                    break;

                case "price-desc":
                    products = products.OrderByDescending(p => p.ProductPrice);
                    break;

                case "latest":
                default:
                    products = products.OrderByDescending(p => p.PostingDate);
                    break;
            }

            ViewBag.ShowBanner = false;

            var productList = await products.ToListAsync();

            return View(productList);
        }

        // ===== LOAD SUB CATEGORY =====
        [HttpGet]
        public async Task<IActionResult> GetSubCategories(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId))
            {
                return Json(new List<object>());
            }

            int catId = int.Parse(categoryId);

            var subCategories = await _context.SubCategories
                .Where(sc => sc.CategoryId == catId)
                .Select(sc => new
                {
                    id = sc.Id,
                    name = sc.SubCategoryName
                })
                .ToListAsync();

            return Json(subCategories);
        }

        // ===== SEARCH SUGGESTION =====
        [HttpGet]
        public async Task<IActionResult> SearchSuggestions(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Json(new List<object>());
            }

            term = term.Trim().ToLower();

            var query = _context.Product
                .Where(p =>
                    (p.ProductName != null && p.ProductName.ToLower().Contains(term)) ||
                    (p.Brand != null && p.Brand.ToLower().Contains(term))
                );

            // đếm tổng số để biết có "xem thêm"
            var totalCount = await query.CountAsync();

            var suggestions = await query
                .OrderBy(p => p.ProductName)
                .Take(5)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.ProductName,
                    slug = p.Slug,
                    image = p.ProductImage1,
                    price = p.ProductPrice
                })
                .ToListAsync();

            return Json(new
            {
                items = suggestions,
                hasMore = totalCount > 5
            });
        }
    }
}