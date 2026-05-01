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
            try
            {
                var categories = await _context.Categories
                    .AsNoTracking()
                    .Include(c => c.SubCategories)
                    .OrderBy(c => c.CategoryName)
                    .ToListAsync();

                return View(categories);
            }
            catch
            {
                return View(Enumerable.Empty<LapTopBD.Models.Category>());
            }
        }
    }
}