using LapTopBD.Models.ViewModels.Users;
using LapTopBD.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace LapTopBD.ViewComponents;

public class PolicyTopLinksViewComponent : ViewComponent
{
    private readonly IPolicyContentStore _policyStore;

    public PolicyTopLinksViewComponent(IPolicyContentStore policyStore)
    {
        _policyStore = policyStore;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var policies = await _policyStore.GetAllAsync();
        var links = policies
            .Where(policy => policy.IsPublished)
            .OrderBy(policy => policy.Title)
            .Select(policy => new PolicyLinkViewModel
            {
                Title = policy.Title,
                Slug = policy.Slug
            })
            .ToList();

        return View(links);
    }
}
