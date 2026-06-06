namespace LapTopBD.Utilities;

public interface IPolicyContentStore
{
    Task<PolicyContent> GetAsync();
    Task SaveAsync(PolicyContent content);
    Task<IReadOnlyList<PolicyItem>> GetAllAsync();
    Task<PolicyItem?> GetByIdAsync(Guid id);
    Task<PolicyItem?> GetBySlugAsync(string slug);
    Task<PolicyItem> UpsertAsync(PolicyItem policy);
    Task<bool> DeleteAsync(Guid id);
}

public sealed class PolicyContent
{
    public string WarrantyHtml { get; set; } = string.Empty;
    public string ShippingHtml { get; set; } = string.Empty;
    public List<PolicyItem> Policies { get; set; } = new();
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PolicyItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
