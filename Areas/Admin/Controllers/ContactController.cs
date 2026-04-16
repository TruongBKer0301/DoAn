using LapTopBD.Data;
using LapTopBD.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LapTopBD.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    [Route("admin/contact")]
    public class ContactController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContactController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("list-contacts")]
        public async Task<IActionResult> ListContacts()
        {
            var contacts = await _context.ContactRequests
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(contacts);
        }

        [HttpPost]
        [Route("mark-read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var contact = await _context.ContactRequests.FindAsync(id);
            if (contact == null)
            {
                return Json(new { success = false, message = "Liên hệ không tồn tại." });
            }

            contact.IsRead = true;
            contact.ReadAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã đánh dấu đã xử lý." });
        }

        [HttpPost]
        [Route("delete-contact")]
        public async Task<IActionResult> DeleteContact(int id)
        {
            var contact = await _context.ContactRequests.FindAsync(id);
            if (contact == null)
            {
                return Json(new { success = false, message = "Liên hệ không tồn tại." });
            }

            _context.ContactRequests.Remove(contact);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa liên hệ." });
        }
    }
}