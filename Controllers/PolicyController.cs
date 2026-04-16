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
    public async Task<IActionResult> Warranty()
    {
        var content = await _policyStore.GetAsync();
        ViewBag.ShowBanner = false;
        return View("Page", new PolicyPageViewModel
        {
            PageTitle = "Chinh sach bao hanh",
            HtmlContent = content.WarrantyHtml
        });
    }

    [HttpGet("giao-hang")]
    public async Task<IActionResult> Shipping()
    {
        var content = await _policyStore.GetAsync();
        ViewBag.ShowBanner = false;
        return View("Page", new PolicyPageViewModel
        {
            PageTitle = "Chinh sach giao hang",
            HtmlContent = content.ShippingHtml
        });
    }
}
