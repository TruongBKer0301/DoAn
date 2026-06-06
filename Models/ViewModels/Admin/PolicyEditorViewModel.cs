namespace LapTopBD.Models.ViewModels.Admin;

public class PolicyEditorViewModel
{
    public List<PolicyListItemViewModel> Policies { get; set; } = new();
}

public class PolicyListItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class PolicyFormViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = true;
}
