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

    [Route("subcategory")]
    public class SubCategoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubCategoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("list-subcategory")]
        public async Task<IActionResult> ListSubCategory(int page = 1, int pageSize = 5)
        {
            // Lấy danh sách danh mục chính
            ViewBag.Categories = await _context.Categories.ToListAsync();

            var totalItems = await _context.SubCategories.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var subCategories = await _context.SubCategories
                .Include(sc => sc.Category)
                .OrderByDescending(sc => sc.CreationDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(sc => new SubCategoryViewModel
                {
                    Id = sc.Id,
                    SubCategoryName = sc.SubCategoryName,
                    CategoryName = sc.Category != null ? sc.Category.CategoryName : string.Empty,
                    CreationDate = sc.CreationDate.HasValue ? sc.CreationDate.Value.ToString("dd/MM/yyyy") : "N/A",
                    UpdationDate = sc.UpdationDate.HasValue ? sc.UpdationDate.Value.ToString("dd/MM/yyyy") : "Chưa cập nhật"
                })
                .ToListAsync();

            // Truyền dữ liệu phân trang giống ListCategory
            ViewBag.TotalPages = totalPages;
            ViewBag.CurrentPage = page;

            return View("ListSubCategory", subCategories);
        }



        [HttpGet]
        [Route("add-subcategory")]
        public async Task<IActionResult> AddSubCategory()
        {
            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View("AddSubCategory");
        }

        //Thêm danh mục phụ
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("add-subcategory")]
        public async Task<IActionResult> AddSubCategory([FromForm] SubCategoryViewModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.SubCategoryName))
                {
                    return Json(new { success = false, message = "Tên danh mục phụ không được để trống." });
                }

                var existingSubCategory = await _context.SubCategories
                    .FirstOrDefaultAsync(sc => (sc.SubCategoryName ?? "").ToLower() == model.SubCategoryName.ToLower());

                if (existingSubCategory != null)
                {
                    return Json(new { success = false, message = "Danh mục phụ này đã tồn tại." });
                }

                // Lấy thông tin danh mục chính
                var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == model.CategoryId);
                if (category == null)
                {
                    return Json(new { success = false, message = "Danh mục chính không tồn tại." });
                }

                // Lấy thông tin admin từ Claims (giả định bạn lưu AdminId trong Claims giống ProductsController)
                var adminIdClaim = User.FindFirst("AdminId");
                if (adminIdClaim == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy AdminId, vui lòng đăng nhập lại." });
                }
                int adminId = int.Parse(adminIdClaim.Value);
                var admin = await _context.admin.FirstOrDefaultAsync(a => a.Id == adminId);
                if (admin == null)
                {
                    return Json(new { success = false, message = "Admin không tồn tại." });
                }

                var subCategory = new SubCategory
                {
                    SubCategoryName = model.SubCategoryName,
                    CategoryId = model.CategoryId,
                    CreationDate = DateTime.Now
                };

                _context.SubCategories.Add(subCategory);
                await _context.SaveChangesAsync();

                // Trả về thông tin để thêm vào bảng
                return Json(new
                {
                    success = true,
                    message = "Danh mục phụ đã được thêm thành công!",
                    subCategoryId = subCategory.Id,
                    subCategoryName = subCategory.SubCategoryName,
                    categoryName = category.CategoryName,
                    creationDate = subCategory.CreationDate.Value.ToString("dd/MM/yyyy"),
                    updationDate = "Chưa cập nhật", // Vì mới tạo, chưa có ngày cập nhật
                    adminName = admin.FullName
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong AddSubCategory: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi máy chủ: {ex.Message}" });
            }
        }

        [HttpGet]
        [Route("edit-subcategory/{id}")]
        public async Task<IActionResult> EditSubCategory(int id)
        {
            var subCategory = await _context.SubCategories.FindAsync(id);

            if (subCategory == null)
            {
                return NotFound();
            }

            var model = new SubCategoryViewModel
            {
                Id = subCategory.Id,
                SubCategoryName = subCategory.SubCategoryName,
                CategoryId = subCategory.CategoryId
            };

            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View("EditSubCategory", model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("edit-subcategory")]
        public async Task<IActionResult> EditSubCategory([FromBody] SubCategoryViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.SubCategoryName))
            {
                return Json(new { success = false, message = "Tên danh mục phụ không được để trống." });
            }

            var subCategory = await _context.SubCategories.FindAsync(model.Id);
            if (subCategory == null)
            {
                return Json(new { success = false, message = "Danh mục phụ không tồn tại." });
            }

            // Kiểm tra xem tên danh mục phụ đã tồn tại chưa
            var existingSubCategory = await _context.SubCategories
                .FirstOrDefaultAsync(sc => (sc.SubCategoryName ?? "").ToLower() == model.SubCategoryName.ToLower() && sc.Id != model.Id);

            if (existingSubCategory != null)
            {
                return Json(new { success = false, message = "Tên danh mục phụ đã tồn tại." });
            }

            subCategory.SubCategoryName = model.SubCategoryName;
            subCategory.CategoryId = model.CategoryId;
            subCategory.UpdationDate = DateTime.Now;

            _context.SubCategories.Update(subCategory);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật danh mục phụ thành công!" });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("delete-subcategory")]
        public async Task<IActionResult> DeleteSubCategory([FromBody] int id)
        {
            try
            {
                var subCategory = await _context.SubCategories.FindAsync(id);
                if (subCategory == null)
                {
                    return Json(new { success = false, message = "Danh mục phụ không tồn tại!" });
                }

                _context.SubCategories.Remove(subCategory);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Danh mục phụ đã được xóa thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa danh mục phụ: " + ex.Message });
            }
        }


    }
}
