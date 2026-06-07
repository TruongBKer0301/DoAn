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

    [HttpGet("")]
    [HttpGet("edit")]
    public async Task<IActionResult> Edit()
    {
        var policies = await _policyStore.GetAllAsync();
        var vm = new PolicyEditorViewModel
        {
            Policies = policies.Select(policy => new PolicyListItemViewModel
            {
                Id = policy.Id,
                Title = policy.Title,
                Slug = policy.Slug,
                IsPublished = policy.IsPublished,
                UpdatedAtUtc = policy.UpdatedAtUtc
            }).ToList()
        };

        return View(vm);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        return View("Form", new PolicyFormViewModel { IsPublished = true });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PolicyFormViewModel vm)
    {
        if (!ValidatePolicyForm(vm))
        {
            return View("Form", vm);
        }

        await _policyStore.UpsertAsync(new PolicyItem
        {
            Title = vm.Title,
            Slug = vm.Slug,
            HtmlContent = vm.HtmlContent,
            IsPublished = vm.IsPublished
        });

        TempData["Success"] = "Đã thêm chính sách thành công.";
        return RedirectToAction(nameof(Edit));
    }

    [HttpGet("edit/{id:guid}")]
    public async Task<IActionResult> Update(Guid id)
    {
        var policy = await _policyStore.GetByIdAsync(id);
        if (policy is null)
        {
            return NotFound();
        }

        return View("Form", new PolicyFormViewModel
        {
            Id = policy.Id,
            Title = policy.Title,
            Slug = policy.Slug,
            HtmlContent = policy.HtmlContent,
            IsPublished = policy.IsPublished
        });
    }

    [HttpPost("edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, PolicyFormViewModel vm)
    {
        if (!ValidatePolicyForm(vm))
        {
            vm.Id = id;
            return View("Form", vm);
        }

        var existing = await _policyStore.GetByIdAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Title = vm.Title;
        existing.Slug = vm.Slug;
        existing.HtmlContent = vm.HtmlContent;
        existing.IsPublished = vm.IsPublished;

        await _policyStore.UpsertAsync(existing);
        TempData["Success"] = "Đã cập nhật chính sách thành công.";
        return RedirectToAction(nameof(Edit));
    }

    [HttpPost("delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _policyStore.DeleteAsync(id);
        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Đã xóa chính sách thành công ."
            : "Không tìm thấy chính sách cần xóa.";

        return RedirectToAction(nameof(Edit));
    }

    [HttpPost("toggle/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublished(Guid id)
    {
        var policy = await _policyStore.GetByIdAsync(id);
        if (policy is null)
        {
            TempData["Error"] = "Không tìm thấy chính sách cần cập nhật.";
            return RedirectToAction(nameof(Edit));
        }

        policy.IsPublished = !policy.IsPublished;
        await _policyStore.UpsertAsync(policy);

        TempData["Success"] = policy.IsPublished
            ? "Đã bật hiển thị chính sách."
            : "Đã ẩn chính sách khỏi website.";

        return RedirectToAction(nameof(Edit));
    }

    private bool ValidatePolicyForm(PolicyFormViewModel vm)
    {
        vm.Title = (vm.Title ?? string.Empty).Trim();
        vm.Slug = (vm.Slug ?? string.Empty).Trim();
        vm.HtmlContent ??= string.Empty;

        if (string.IsNullOrWhiteSpace(vm.Title))
        {
            ModelState.AddModelError(nameof(vm.Title), "Vui lòng nhập tiêu đề chính sách.");
        }

        if (string.IsNullOrWhiteSpace(vm.HtmlContent))
        {
            ModelState.AddModelError(nameof(vm.HtmlContent), "Vui lòng nhập nội dung chính sách.");
        }

        return ModelState.IsValid;
    }
}
