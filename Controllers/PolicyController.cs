using LapTopBD.Models.ViewModels.Users;
using LapTopBD.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace LapTopBD.Controllers;

[Route("chinh-sach")]
public class PolicyController : Controller
{
    private readonly IPolicyContentStore _policyStore;

    public PolicyController(IPolicyContentStore policyStore)
    {
        _policyStore = policyStore;
    }

    [HttpGet("bao-hanh")]
    public Task<IActionResult> Warranty() => ShowPolicy("bao-hanh");

    [HttpGet("giao-hang")]
    public Task<IActionResult> Shipping() => ShowPolicy("giao-hang");

    [HttpGet("{slug}")]
    public Task<IActionResult> Detail(string slug) => ShowPolicy(slug);

    private async Task<IActionResult> ShowPolicy(string slug)
    {
        var policy = await _policyStore.GetBySlugAsync(slug);
        if (policy is null)
        {
            return NotFound();
        }

        ViewBag.ShowBanner = false;
        return View("Page", new PolicyPageViewModel
        {
            PageTitle = policy.Title,
            HtmlContent = policy.HtmlContent
        });
    }
}
