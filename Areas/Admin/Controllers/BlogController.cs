using LapTopBD.Data;
using LapTopBD.Models;
using LapTopBD.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LapTopBD.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin,Seller")]
[Route("admin/blog")]
public class BlogController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public BlogController(ApplicationDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet("")]
    [HttpGet("list")]
    public async Task<IActionResult> Index(string? q = null)
    {
        ViewBag.SearchQuery = q;

        var postsQuery = _context.BlogPosts
            .AsNoTracking()
            .Include(b => b.Admin)
            .OrderByDescending(b => b.CreatedAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            postsQuery = postsQuery.Where(b =>
                EF.Functions.Like(b.Title, $"%{keyword}%") ||
                EF.Functions.Like(b.Summary ?? string.Empty, $"%{keyword}%") ||
                EF.Functions.Like(b.ContentHtml, $"%{keyword}%"));
        }

        var posts = await postsQuery.ToListAsync();

        return View(posts);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        return View(new BlogPost());
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BlogPost model, IFormFile? CoverImageFile)
    {
        var adminId = GetAdminId();
        if (adminId == null)
        {
            return Unauthorized();
        }

        model.Title = model.Title?.Trim() ?? string.Empty;
        model.Summary = model.Summary?.Trim();
        model.CoverImageUrl = model.CoverImageUrl?.Trim();
        model.ContentHtml = model.ContentHtml?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(BlogPost.Title), "Vui lòng nhập tiêu đề bài viết.");
        }

        if (string.IsNullOrWhiteSpace(model.ContentHtml))
        {
            ModelState.AddModelError(nameof(BlogPost.ContentHtml), "Vui lòng nhập nội dung bài viết.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.CoverImageUrl = await SaveCoverImageAsync(CoverImageFile, model.CoverImageUrl);
        var baseSlug = SlugHelper.GenerateSlug(model.Title);
        model.Slug = await BuildUniqueSlugAsync(baseSlug);
        model.AdminId = adminId.Value;
        model.CreatedAt = DateTime.Now;
        model.UpdatedAt = DateTime.Now;
        model.PublishedAt = model.IsPublished ? DateTime.Now : null;

        _context.BlogPosts.Add(model);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã tạo bài viết blog thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await _context.BlogPosts.FindAsync(id);
        if (post == null)
        {
            return NotFound();
        }

        return View(post);
    }

    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BlogPost model, IFormFile? CoverImageFile)
    {
        var post = await _context.BlogPosts.FindAsync(id);
        if (post == null)
        {
            return NotFound();
        }

        model.Title = model.Title?.Trim() ?? string.Empty;
        model.Summary = model.Summary?.Trim();
        model.CoverImageUrl = model.CoverImageUrl?.Trim();
        model.ContentHtml = model.ContentHtml?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(BlogPost.Title), "Vui lòng nhập tiêu đề bài viết.");
        }

        if (string.IsNullOrWhiteSpace(model.ContentHtml))
        {
            ModelState.AddModelError(nameof(BlogPost.ContentHtml), "Vui lòng nhập nội dung bài viết.");
        }

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.AdminId = post.AdminId;
            model.CreatedAt = post.CreatedAt;
            model.Slug = post.Slug;
            return View(model);
        }

        model.CoverImageUrl = await SaveCoverImageAsync(CoverImageFile, model.CoverImageUrl ?? post.CoverImageUrl);

        if (!string.Equals(post.Title, model.Title, StringComparison.OrdinalIgnoreCase))
        {
            var newSlug = SlugHelper.GenerateSlug(model.Title);
            post.Slug = await BuildUniqueSlugAsync(newSlug, id);
        }

        post.Title = model.Title;
        post.Summary = model.Summary;
        post.ContentHtml = model.ContentHtml;
        post.CoverImageUrl = model.CoverImageUrl;
        post.IsPublished = model.IsPublished;
        post.UpdatedAt = DateTime.Now;
        post.PublishedAt = post.IsPublished ? post.PublishedAt ?? DateTime.Now : null;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã cập nhật bài viết blog.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await _context.BlogPosts.FindAsync(id);
        if (post == null)
        {
            TempData["Error"] = "Không tìm thấy bài viết để xóa.";
            return RedirectToAction(nameof(Index));
        }

        _context.BlogPosts.Remove(post);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã xóa bài viết blog.";
        return RedirectToAction(nameof(Index));
    }

    private int? GetAdminId()
    {
        var claim = User.FindFirst("AdminId")?.Value;
        if (int.TryParse(claim, out var adminId))
        {
            return adminId;
        }

        return null;
    }

    private async Task<string?> SaveCoverImageAsync(IFormFile? coverImageFile, string? fallbackUrl)
    {
        if (coverImageFile == null || coverImageFile.Length == 0)
        {
            return string.IsNullOrWhiteSpace(fallbackUrl) ? null : fallbackUrl.Trim();
        }

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads/blog");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(coverImageFile.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await coverImageFile.CopyToAsync(stream);
        }

        return $"/uploads/blog/{fileName}";
    }

    private async Task<string> BuildUniqueSlugAsync(string baseSlug, int? ignoreId = null)
    {
        var safeBase = string.IsNullOrWhiteSpace(baseSlug) ? "blog-post" : baseSlug;
        var candidate = safeBase;
        var counter = 2;

        while (await _context.BlogPosts.AnyAsync(b => b.Slug == candidate && (!ignoreId.HasValue || b.Id != ignoreId.Value)))
        {
            candidate = $"{safeBase}-{counter}";
            counter++;
        }

        return candidate;
    }
}
