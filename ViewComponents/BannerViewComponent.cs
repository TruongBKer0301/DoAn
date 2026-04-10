using LapTopBD.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LapTopBD.ViewComponents
{
    public class BannerViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public BannerViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Lấy danh sách banner đang hoạt động và sắp xếp theo Position
            var banners = await _context.Banner
                .Where(b => b.Status) // Chỉ lấy banner có Status = true
                .OrderBy(b => b.Position) // Sắp xếp theo Position
                .ToListAsync();

            return View(banners); // Truyền danh sách banner vào view
        }
    }
}