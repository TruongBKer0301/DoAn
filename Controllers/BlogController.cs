using System.Text.RegularExpressions;
using LapTopBD.Data;
using LapTopBD.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LapTopBD.Controllers;

[Route("blog")]
public class BlogController : Controller
{
    private readonly ApplicationDbContext _context;

    public BlogController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 9)
    {
        ViewBag.ShowBanner = false;

        var totalItems = await _context.BlogPosts.CountAsync(p => p.IsPublished);
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        if (totalPages == 0) totalPages = 1;
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        var posts = await _context.BlogPosts
            .AsNoTracking()
            .Include(b => b.Admin)
            .Where(b => b.IsPublished)
            .OrderByDescending(b => b.PublishedAt ?? b.CreatedAt)
            .ThenByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var featured = posts.Take(1).FirstOrDefault() ?? await _context.BlogPosts
            .AsNoTracking()
            .Include(b => b.Admin)
            .Where(b => b.IsPublished)
            .OrderByDescending(b => b.PublishedAt ?? b.CreatedAt)
            .FirstOrDefaultAsync();

        ViewBag.FeaturedPost = featured;
        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentPage = page;

        return View(posts);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        ViewBag.ShowBanner = false;

        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var post = await _context.BlogPosts
            .AsNoTracking()
            .Include(b => b.Admin)
            .FirstOrDefaultAsync(b => b.Slug == slug && b.IsPublished);

        if (post == null)
        {
            return NotFound();
        }

        ViewBag.RelatedPosts = await _context.BlogPosts
            .AsNoTracking()
            .Where(b => b.IsPublished && b.Id != post.Id)
            .OrderByDescending(b => b.PublishedAt ?? b.CreatedAt)
            .Take(3)
            .ToListAsync();

        return View(post);
    }

    private static string BuildExcerpt(string? html, int length = 180)
    {
        var text = Regex.Replace(html ?? string.Empty, "<.*?>", string.Empty).Trim();
        if (text.Length <= length)
        {
            return text;
        }

        return text[..length].TrimEnd() + "...";
    }
}
