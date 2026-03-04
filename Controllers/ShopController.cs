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
            // Lấy danh sách danh mục
            var categories = await _context.Categories
                .Select(c => new CategoryViewModel
                {
                    Id = c.Id,
                    CategoryName = c.CategoryName,
                    CategoryDescription = c.CategoryDescription,
                    AdminId = c.AdminId
                    // Không lấy AdminName
                })
                .ToListAsync();
            ViewBag.Categories = categories;

            // Lấy danh sách tiểu danh mục nếu có danh mục được chọn
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

            // Lưu các giá trị filter để hiển thị trên view
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SelectedSubCategoryId = subCategoryId;
            ViewBag.SearchTerm = search;
            ViewBag.SortBy = sortBy;

            // Query sản phẩm với join chỉ để lấy CategoryName (nếu cần)
            var products = from p in _context.Products
                           join c in _context.Categories on p.CategoryId equals c.Id
                           join sc in _context.SubCategories on p.SubCategoryId equals sc.Id into subCatGroup
                           from sc in subCatGroup.DefaultIfEmpty() // Left join vì SubCategoryId có thể null
                           select new ProductViewModel
                           {
                               Id = p.Id,
                               AdminId = p.AdminId, // Giữ AdminId nếu cần cho logic khác
                               CategoryId = p.CategoryId,
                               CategoryName = c.CategoryName, // Chỉ lấy CategoryName
                               SubCategoryId = p.SubCategoryId,
                               SubCategoryName = sc != null ? sc.SubCategoryName : null, // Lấy SubCategoryName nếu có
                               ProductName = p.ProductName,
                               ProductCompany = p.ProductCompany,
                               ProductPrice = p.ProductPrice,
                               ProductPriceBeforeDiscount = p.ProductPriceBeforeDiscount,
                               ProductDescription = p.ProductDescription,
                               ProductImage1 = p.ProductImage1,
                               ProductImage2 = p.ProductImage2,
                               ProductImage3 = p.ProductImage3,
                               ProductAvailability = p.ProductAvailability ? "Còn hàng" : "Hết hàng",
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
                               Slug = p.Slug
                           };

            // Filter theo danh mục
            if (!string.IsNullOrEmpty(categoryId))
            {
                int catId = int.Parse(categoryId);
                products = products.Where(p => p.CategoryId == catId);
            }

            // Filter theo tiểu danh mục
            if (!string.IsNullOrEmpty(subCategoryId))
            {
                int subCatId = int.Parse(subCategoryId);
                products = products.Where(p => p.SubCategoryId == subCatId);
            }

            // Tìm kiếm theo tên sản phẩm
            if (!string.IsNullOrEmpty(search))
            {
                products = products.Where(p => (p.ProductName != null && p.ProductName.Contains(search)) ||
                                              (p.ProductCompany != null && p.ProductCompany.Contains(search)) ||
                                              (p.Brand != null && p.Brand.Contains(search)));
            }

            // Sắp xếp
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
    }
}