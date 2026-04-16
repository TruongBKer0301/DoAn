using LapTopBD.Models.ViewModels.Admin;
using LapTopBD.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LapTopBD.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminAuth", Roles = "Admin,Seller")]
[Route("admin/policy")]
public class PolicyController : Controller
{
    private readonly IPolicyContentStore _policyStore;

    public PolicyController(IPolicyContentStore policyStore)
    {
        _policyStore = policyStore;
    }

    [HttpGet("edit")]
    public async Task<IActionResult> Edit()
    {
        var content = await _policyStore.GetAsync();
        var vm = new PolicyEditorViewModel
        {
            WarrantyHtml = content.WarrantyHtml,
            ShippingHtml = content.ShippingHtml
        };

        return View(vm);
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PolicyEditorViewModel vm)
    {
        vm.WarrantyHtml ??= string.Empty;
        vm.ShippingHtml ??= string.Empty;

        await _policyStore.SaveAsync(new PolicyContent
        {
            WarrantyHtml = vm.WarrantyHtml,
            ShippingHtml = vm.ShippingHtml
        });

        TempData["Success"] = "Da cap nhat noi dung chinh sach thanh cong.";
        return RedirectToAction(nameof(Edit));
    }
}
