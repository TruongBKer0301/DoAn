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
    [Route("banner")]
    public class BannerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public BannerController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Hiển thị danh sách banner
        [HttpGet]
        [Route("")]
        [Route("list-banner")]
        public async Task<IActionResult> ListBanner()
        {
            var banners = await _context.Banner
                .Select(b => new BannerViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    ImageUrl = b.ImageUrl,
                    Status = b.Status,
                    Position = b.Position,
                    CreationDate = b.CreationDate,
                    UpdationDate = b.UpdationDate
                })
                .OrderBy(b => b.Position) // Đảm bảo sắp xếp theo Position
                .ToListAsync();

            return View(banners);
        }

        //Thêm banner
        [HttpPost]
        [Route("add-banner")]
        public async Task<IActionResult> AddBanner([FromForm] BannerViewModel model)
        {
            try
            {
                if (model.ImageFile == null || model.ImageFile.Length == 0)
                {
                    return Json(new { success = false, message = "Vui lòng chọn hình ảnh!" });
                }

                var adminIdClaim = User.FindFirst("AdminId");
                if (adminIdClaim == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy AdminId, vui lòng đăng nhập lại." });
                }

                int adminId = int.Parse(adminIdClaim.Value);
                var adminExists = await _context.admin.AnyAsync(a => a.Id == adminId);
                if (!adminExists)
                {
                    return Json(new { success = false, message = "Admin không tồn tại!" });
                }

                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads/banner");
                Directory.CreateDirectory(uploadsFolder);

                string fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.ImageFile.FileName)}";
                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }

                // Đầu tiên, tăng Position của tất cả các banner hiện có
                var existingBanners = await _context.Banner.ToListAsync();
                foreach (var existingBanner in existingBanners)
                {
                    existingBanner.Position += 1;
                }

                var banner = new Banner
                {
                    Title = model.Title ?? string.Empty,
                    ImageUrl = $"/uploads/banner/{fileName}",
                    Status = model.Status,
                    CreationDate = DateTime.Now,
                    UpdationDate = DateTime.Now,
                    Position = 1, // Banner mới luôn ở vị trí đầu tiên
                    AdminId = adminId
                };

                _context.Banner.Add(banner);
                await _context.SaveChangesAsync();

                // Lấy danh sách banner đã cập nhật để trả về cho client
                var updatedBanners = await _context.Banner
                    .Select(b => new BannerViewModel
                    {
                        Id = b.Id,
                        Title = b.Title,
                        ImageUrl = b.ImageUrl,
                        Status = b.Status,
                        Position = b.Position,
                        CreationDate = b.CreationDate,
                        UpdationDate = b.UpdationDate
                    })
                    .OrderBy(b => b.Position)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    message = "Thêm banner thành công!",
                    id = banner.Id,
                    imageUrl = banner.ImageUrl,
                    position = 1,
                    banners = updatedBanners // Trả về danh sách banner đã cập nhật
                });
            }
            catch (DbUpdateException dbEx)
            {
                return Json(new { success = false, message = "Lỗi database: " + dbEx.InnerException?.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi Server: " + ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("edit-banner/{id}")]
        public async Task<IActionResult> EditBanner(int id)
        {
            var banner = await _context.Banner.FindAsync(id);
            if (banner == null)
            {
                return NotFound();
            }

            var model = new BannerViewModel
            {
                Id = banner.Id,
                Title = banner.Title,
                ImageUrl = banner.ImageUrl,
                Status = banner.Status,
                Position = banner.Position,
                CreationDate = banner.CreationDate,
                UpdationDate = banner.UpdationDate
            };

            return View("EditBanner", model);
        }

        [HttpPost]
        [Route("edit-banner/{id}")]
        public async Task<IActionResult> EditBanner(int id, [FromForm] BannerViewModel model)
        {
            try
            {
                var banner = await _context.Banner.FindAsync(id);
                if (banner == null)
                {
                    return Json(new { success = false, message = "Banner không tồn tại!" });
                }

                var adminIdClaim = User.FindFirst("AdminId");
                if (adminIdClaim == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy AdminId, vui lòng đăng nhập lại." });
                }

                int adminId = int.Parse(adminIdClaim.Value);
                var adminExists = await _context.admin.AnyAsync(a => a.Id == adminId);
                if (!adminExists)
                {
                    return Json(new { success = false, message = "Admin không tồn tại!" });
                }

                // Cập nhật các trường
                banner.Title = model.Title ?? string.Empty;
                banner.Status = model.Status;
                banner.UpdationDate = DateTime.Now;

                // Nếu có file hình ảnh mới, cập nhật hình ảnh
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    // Xóa hình ảnh cũ nếu tồn tại
                    if (!string.IsNullOrEmpty(banner.ImageUrl))
                    {
                        string oldImagePath = Path.Combine(_env.WebRootPath, banner.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    // Lưu hình ảnh mới
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads/banner");
                    Directory.CreateDirectory(uploadsFolder);

                    string fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.ImageFile.FileName)}";
                    string filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ImageFile.CopyToAsync(stream);
                    }

                    banner.ImageUrl = $"/uploads/banner/{fileName}";
                }

                _context.Banner.Update(banner);
                await _context.SaveChangesAsync();

                var updatedBanners = await _context.Banner
                    .Select(b => new BannerViewModel
                    {
                        Id = b.Id,
                        Title = b.Title,
                        ImageUrl = b.ImageUrl,
                        Status = b.Status,
                        Position = b.Position,
                        CreationDate = b.CreationDate,
                        UpdationDate = b.UpdationDate
                    })
                    .OrderBy(b => b.Position)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    message = "Chỉnh sửa banner thành công!",
                    banner = new BannerViewModel
                    {
                        Id = banner.Id,
                        Title = banner.Title,
                        ImageUrl = banner.ImageUrl,
                        Status = banner.Status,
                        Position = banner.Position,
                        CreationDate = banner.CreationDate,
                        UpdationDate = banner.UpdationDate
                    },
                    banners = updatedBanners
                });
            }
            catch (DbUpdateException dbEx)
            {
                return Json(new { success = false, message = "Lỗi database: " + dbEx.InnerException?.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi Server: " + ex.Message });
            }
        }

        //Xóa banner
        [HttpPost]
        [Route("delete-banner")]
        public async Task<IActionResult> DeleteBanner([FromBody] int id)
        {
            try
            {
                var banner = await _context.Banner.FindAsync(id);
                if (banner == null)
                {
                    return Json(new { success = false, message = "Banner không tồn tại!" });
                }

                // Kiểm tra nếu ImageUrl không phải NULL trước khi xóa ảnh
                if (!string.IsNullOrEmpty(banner.ImageUrl))
                {
                    string imagePath = Path.Combine(_env.WebRootPath, banner.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                _context.Banner.Remove(banner);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa banner thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi Server: " + ex.Message });
            }
        }

        //Kéo thả để sắp xếp banner
        [HttpPost]
        [Route("update-positions")]
        public async Task<IActionResult> UpdatePositions([FromBody] List<int> sortedIds)
        {
            try
            {
                if (sortedIds == null || sortedIds.Count == 0)
                {
                    return Json(new { success = false, message = "Danh sách không hợp lệ!" });
                }

                // Lấy tất cả các banner hiện có để kiểm tra
                var banners = await _context.Banner.ToListAsync();
                if (banners.Count != sortedIds.Count)
                {
                    return Json(new { success = false, message = "Số lượng banner không khớp!" });
                }

                // Cập nhật vị trí cho mỗi banner
                foreach (var b in banners)
                {
                    b.Position = 0;  // Reset vị trí trước khi cập nhật
                }
                await _context.SaveChangesAsync();

                for (int i = 0; i < sortedIds.Count; i++)
                {
                    var id = sortedIds[i];
                    var banner = await _context.Banner.FindAsync(id);
                    if (banner == null)
                    {
                        return Json(new { success = false, message = $"Banner với ID {id} không tồn tại!" });
                    }
                    banner.Position = sortedIds.IndexOf(banner.Id) + 1;

                }
                await _context.SaveChangesAsync();


                // Trả về danh sách banner đã cập nhật để client có thể đồng bộ
                var updatedBanners = await _context.Banner
                    .Select(b => new BannerViewModel
                    {
                        Id = b.Id,
                        Title = b.Title,
                        ImageUrl = b.ImageUrl,
                        Status = b.Status,
                        Position = b.Position,
                        CreationDate = b.CreationDate,
                        UpdationDate = b.UpdationDate
                    })
                    .OrderBy(b => b.Position)
                    .ToListAsync();

                return Json(new { success = true, message = "Cập nhật vị trí thành công!", banners = updatedBanners });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Json(new { success = false, message = "Lỗi Server: " + ex.Message });
            }
        }
    }
}
