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
            try
            {
                var banners = await _context.Banner
                    .AsNoTracking()
                    .Where(b => b.Status)
                    .OrderBy(b => b.Position)
                    .ToListAsync();

                return View(banners);
            }
            catch
            {
                return View(Enumerable.Empty<LapTopBD.Models.Banner>());
            }
        }
    }
}