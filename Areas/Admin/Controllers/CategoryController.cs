using LapTopBD.Data;
using LapTopBD.Models;
using LapTopBD.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LapTopBD.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    [Route("category")]
    public class CategoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("list-category")]
        public async Task<IActionResult> ListCategory(int page = 1, int pageSize = 5)
        {
            var totalItems = await _context.Categories.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var categories = await _context.Categories
                .Include(c => c.Admin)
                .OrderByDescending(c => c.CreationDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CategoryViewModel
                {
                    Id = c.Id,
                    CategoryName = c.CategoryName,
                    CategoryDescription = c.CategoryDescription,
                    CreationDate = c.CreationDate.HasValue ? c.CreationDate.Value.ToString("dd/MM/yyyy") : "N/A",
                    UpdationDate = c.UpdationDate.HasValue ? c.UpdationDate.Value.ToString("dd/MM/yyyy") : "Chưa Cập Nhật",
                    AdminName = c.Admin != null ? c.Admin.FullName : string.Empty
                })
                .ToListAsync();

            // Cập nhật ViewBag cho phân trang
            ViewBag.TotalPages = totalPages;
            ViewBag.CurrentPage = page;

            return View("ListCategory", categories);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("add-category")]
        public async Task<IActionResult> AddCategory([FromForm] CategoryViewModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.CategoryName))
                {
                    return Json(new { success = false, message = "Tên danh mục không được để trống." });
                }

                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => (c.CategoryName ?? "").ToLower() == (model.CategoryName ?? "").ToLower());

                if (existingCategory != null)
                {
                    return Json(new { success = false, message = "Danh mục này đã tồn tại." });
                }

                // Lấy AdminId từ Claims
                var adminIdClaim = User.FindFirst("AdminId");
                if (adminIdClaim == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy AdminId, vui lòng đăng nhập lại." });
                }

                int adminId = int.Parse(adminIdClaim.Value);

                // Kiểm tra admin có tồn tại không và lấy thông tin admin
                var admin = await _context.admin.FindAsync(adminId);
                if (admin == null)
                {
                    return Json(new { success = false, message = "Admin không tồn tại." });
                }

                // Tạo đối tượng Category
                var category = new Category
                {
                    CategoryName = model.CategoryName,
                    CategoryDescription = model.CategoryDescription,
                    CreationDate = DateTime.Now,
                    AdminId = adminId
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                // Trả về thông tin cần thiết để hiển thị trong bảng
                return Json(new
                {
                    success = true,
                    message = "Danh mục đã được thêm thành công!",
                    categoryId = category.Id,
                    categoryName = category.CategoryName,
                    categoryDescription = category.CategoryDescription ?? "",
                    creationDate = category.CreationDate.Value.ToString("dd/MM/yyyy"),
                    updationDate = category.UpdationDate.HasValue ? category.UpdationDate.Value.ToString("dd/MM/yyyy") : "Chưa cập nhật",
                    adminName = admin.FullName
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong AddCategory: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi máy chủ: {ex.Message}" });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("edit-category/{id}")]
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            var model = new CategoryViewModel
            {
                Id = category.Id,
                CategoryName = category.CategoryName,
                CategoryDescription = category.CategoryDescription
            };

            return View("EditCategory", model);
        }
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("edit-category/{id}")]
        public async Task<IActionResult> EditCategory(int id, [FromForm] CategoryViewModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.CategoryName))
                {
                    return Json(new { success = false, message = "Tên danh mục không được để trống." });
                }

                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    return Json(new { success = false, message = "Danh mục không tồn tại." });
                }

                // Kiểm tra xem có danh mục nào trùng tên không (trừ chính nó)
                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => (c.CategoryName ?? "").ToLower() == model.CategoryName.ToLower() && c.Id != id);

                if (existingCategory != null)
                {
                    return Json(new { success = false, message = "Tên danh mục đã tồn tại." });
                }

                category.CategoryName = model.CategoryName;
                category.CategoryDescription = model.CategoryDescription;
                category.UpdationDate = DateTime.Now;

                _context.Categories.Update(category);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Cập nhật danh mục thành công!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong UpdateCategory: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi máy chủ: {ex.Message}" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("delete-category/{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    return Json(new { success = false, message = "Danh mục không tồn tại." });
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa danh mục thành công!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong DeleteCategory: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi máy chủ: {ex.Message}" });
            }
        }


    }
}
