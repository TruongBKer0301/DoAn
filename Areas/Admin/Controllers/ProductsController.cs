using LapTopBD.Data;
using LapTopBD.Models;
using LapTopBD.Utilities;
using LapTopBD.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LapTopBD.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    [Route("products")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("list-products")]
        public async Task<IActionResult> ListProducts(int page = 1, int pageSize = 3) 
        {
            var totalItems = await _context.Products.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            if (totalPages == 0) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Admin)
                .OrderByDescending(p => p.PostingDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    AdminName = p.Admin != null ? p.Admin.FullName : "Không rõ",
                    CategoryName = p.Category != null ? p.Category.CategoryName : "Không rõ",
                    SubCategoryName = p.SubCategory != null ? p.SubCategory.SubCategoryName : "N/A",
                    ProductName = p.ProductName,
                    ProductImage1 = p.ProductImage1,
                    ProductImage2 = p.ProductImage2,
                    ProductImage3 = p.ProductImage3,
                    ProductAvailability = p.ProductAvailability ? "Còn hàng" : "Hết hàng",
                    ProductPrice = p.ProductPrice,
                    ProductPriceBeforeDiscount = p.ProductPriceBeforeDiscount
                })
                .ToListAsync();

            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.SubCategories = _context.SubCategories.ToList();

            ViewBag.TotalPages = totalPages;
            ViewBag.CurrentPage = page;

            return View("ListProducts", products);
        }

        //Thêm sản phẩm
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("add-product")]
        public async Task<IActionResult> AddProduct([FromForm] ProductViewModel model, List<IFormFile> ProductImages)
        {
            try
            {
                // Kiểm tra dữ liệu nhập vào
                if (string.IsNullOrWhiteSpace(model.ProductName) || model.ProductPrice == 0 || model.CategoryId == 0 || model.SubCategoryId == 0)
                    return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin sản phẩm." });

                if (model.ProductPriceBeforeDiscount < model.ProductPrice)
                    return Json(new { success = false, message = "Giá trước giảm không thể nhỏ hơn giá bán." });

                // Lấy AdminId từ Claims
                var adminIdClaim = User.FindFirst("AdminId");
                if (adminIdClaim == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy AdminId, vui lòng đăng nhập lại." });
                }

                int adminId = int.Parse(adminIdClaim.Value);

                // Kiểm tra AdminId có tồn tại trong DB không
                var adminExists = await _context.admin.AnyAsync(a => a.Id == adminId);
                if (!adminExists)
                {
                    return Json(new { success = false, message = "AdminId không hợp lệ." });
                }

                // Tạo đối tượng sản phẩm
                var newProduct = new Product
                {
                    ProductName = model.ProductName,
                    CategoryId = model.CategoryId,
                    SubCategoryId = model.SubCategoryId,
                    Slug = SlugHelper.GenerateSlug(model.ProductName),
                    ProductPrice = model.ProductPrice,
                    ProductPriceBeforeDiscount = model.ProductPriceBeforeDiscount,
                    Brand = model.Brand,
                    CPU = model.CPU,
                    RAM = model.RAM,
                    Storage = model.Storage,
                    GPU = model.GPU,
                    VGA = model.VGA,
                    Promotion = model.Promotion,
                    ProductDescription = model.ProductDescription,
                    ProductAvailability = model.ProductAvailability == "Còn hàng",
                    PostingDate = DateTime.Now,
                    UpdationDate = DateTime.Now,
                    AdminId = adminId // Gán AdminId để tránh lỗi FK
                };

                _context.Products.Add(newProduct);
                await _context.SaveChangesAsync();

                // Lưu ảnh sản phẩm
                string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/productimages");
                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                for (int i = 0; i < ProductImages.Count && i < 3; i++)
                {
                    var file = ProductImages[i];
                    if (file != null && file.Length > 0)
                    {
                        string fileName = $"{Guid.NewGuid()}_{file.FileName}";
                        string filePath = Path.Combine(uploadPath, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        if (i == 0) newProduct.ProductImage1 = "/uploads/productimages/" + fileName;
                        if (i == 1) newProduct.ProductImage2 = "/uploads/productimages/" + fileName;
                        if (i == 2) newProduct.ProductImage3 = "/uploads/productimages/" + fileName;
                    }
                }

                _context.Products.Update(newProduct);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Thêm sản phẩm thành công!",
                    productId = newProduct.Id,
                    productImage1 = newProduct.ProductImage1,
                    productImage2 = newProduct.ProductImage2,
                    productImage3 = newProduct.ProductImage3
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }


        //Chỉnh sửa sản phẩm 
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("edit-product/{id}")]
        public async Task<IActionResult> EditProducts(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Admin)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            var model = new ProductViewModel
            {
                Id = product.Id,
                ProductName = product.ProductName,
                CategoryId = product.CategoryId,
                SubCategoryId = product.SubCategoryId,
                ProductPrice = product.ProductPrice,
                ProductPriceBeforeDiscount = product.ProductPriceBeforeDiscount,
                Brand = product.Brand,
                CPU = product.CPU,
                RAM = product.RAM,
                Storage = product.Storage,
                GPU = product.GPU,
                VGA = product.VGA,
                Promotion = product.Promotion,
                ProductDescription = product.ProductDescription,
                ProductAvailability = product.ProductAvailability ? "Còn hàng" : "Hết hàng",
                ProductImage1 = product.ProductImage1,
                ProductImage2 = product.ProductImage2,
                ProductImage3 = product.ProductImage3,
                AdminName = product.Admin != null ? product.Admin.FullName : "Không rõ",
                CategoryName = product.Category != null ? product.Category.CategoryName : "Không rõ",
                SubCategoryName = product.SubCategory != null ? product.SubCategory.SubCategoryName : "N/A"
            };

            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.SubCategories = await _context.SubCategories.ToListAsync();

            return View("EditProducts", model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("edit-product/{id}")]
        public async Task<IActionResult> EditProducts(int id, [FromForm] ProductViewModel model, List<IFormFile> ProductImages)
        {

            try
            {

                // Kiểm tra sản phẩm tồn tại
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    return Json(new { success = false, message = "Sản phẩm không tồn tại!" });
                }

                // Kiểm tra dữ liệu nhập vào
                if (string.IsNullOrWhiteSpace(model.ProductName) || model.ProductPrice == 0 || model.CategoryId == 0 || model.SubCategoryId == 0)
                    return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin sản phẩm." });

                if (model.ProductPriceBeforeDiscount < model.ProductPrice)
                    return Json(new { success = false, message = "Giá trước giảm không thể nhỏ hơn giá bán." });

                // Lấy AdminId từ Claims
                var adminIdClaim = User.FindFirst("AdminId");
                if (adminIdClaim == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy AdminId, vui lòng đăng nhập lại." });
                }

                int adminId = int.Parse(adminIdClaim.Value);

                // Kiểm tra AdminId có tồn tại trong DB không
                var adminExists = await _context.admin.AnyAsync(a => a.Id == adminId);
                if (!adminExists)
                {
                    return Json(new { success = false, message = "AdminId không hợp lệ." });
                }

                // Cập nhật thông tin sản phẩm
                product.ProductName = model.ProductName;
                product.CategoryId = model.CategoryId;
                product.SubCategoryId = model.SubCategoryId;
                product.ProductPrice = model.ProductPrice;
                product.ProductPriceBeforeDiscount = model.ProductPriceBeforeDiscount;
                product.Brand = model.Brand;
                product.CPU = model.CPU;
                product.RAM = model.RAM;
                product.Storage = model.Storage;
                product.GPU = model.GPU;
                product.VGA = model.VGA;
                product.Promotion = model.Promotion;
                product.ProductDescription = model.ProductDescription;
                product.ProductAvailability = model.ProductAvailability == "true";

                product.UpdationDate = DateTime.Now;
                product.AdminId = adminId;

                // Lưu ảnh sản phẩm nếu có
                if (ProductImages != null && ProductImages.Any())
                {
                    string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/productimages");
                    if (!Directory.Exists(uploadPath))
                    {
                        Directory.CreateDirectory(uploadPath);
                    }

                    for (int i = 0; i < ProductImages.Count && i < 3; i++)
                    {
                        var file = ProductImages[i];
                        if (file != null && file.Length > 0)
                        {
                            // Xóa ảnh cũ nếu có
                            string? oldImagePath = null;
                            if (i == 0 && !string.IsNullOrEmpty(product.ProductImage1))
                                oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.ProductImage1.TrimStart('/'));
                            else if (i == 1 && !string.IsNullOrEmpty(product.ProductImage2))
                                oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.ProductImage2.TrimStart('/'));
                            else if (i == 2 && !string.IsNullOrEmpty(product.ProductImage3))
                                oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.ProductImage3.TrimStart('/'));

                            if (!string.IsNullOrEmpty(oldImagePath) && System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }

                            // Lưu ảnh mới
                            string fileName = $"{Guid.NewGuid()}_{file.FileName}";
                            string filePath = Path.Combine(uploadPath, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            if (i == 0) product.ProductImage1 = "/uploads/productimages/" + fileName;
                            else if (i == 1) product.ProductImage2 = "/uploads/productimages/" + fileName;
                            else if (i == 2) product.ProductImage3 = "/uploads/productimages/" + fileName;
                        }
                    }
                }

                // Cập nhật database
                _context.Products.Update(product);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Dữ liệu nhận được: ProductAvailability = {model.ProductAvailability}");
               
                return Json(new
                {
                    success = true,
                    message = "Cập nhật sản phẩm thành công!",
                    productId = product.Id,
                    productAvailability = product.ProductAvailability, // Trả về giá trị để kiểm tra
                    productImage1 = product.ProductImage1,
                    productImage2 = product.ProductImage2,
                    productImage3 = product.ProductImage3
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        //Xoá sản phẩm
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("delete-product")]
        public async Task<IActionResult> DeleteProduct(int productId)
        {
            try
            {
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm." });
                }
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Xoá sản phẩm thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        //Sổ dữ liệu danh mục con..
        [HttpGet]
        [Route("get-sub-categories")]
        public JsonResult GetSubCategories(int categoryId)
        {
            var subCategories = _context.SubCategories
                .Where(sc => sc.CategoryId == categoryId)
                .Select(sc => new { sc.Id, sc.SubCategoryName })
                .ToList();

            return Json(subCategories);
        }
    }
}
