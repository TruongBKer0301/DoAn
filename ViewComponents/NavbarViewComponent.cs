using LapTopBD.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LapTopBD.ViewComponents
{
    public class NavbarViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public NavbarViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Lấy danh sách Categories và bao gồm SubCategories
            var categories = await _context.Categories
                .Include(c => c.SubCategories) // Bao gồm SubCategories liên quan
                .OrderBy(c => c.CategoryName) // Sắp xếp theo tên danh mục
                .ToListAsync();

            return View(categories); // Truyền danh sách Categories vào view
        }
    }
}