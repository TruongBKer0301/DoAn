namespace LapTopBD.Utilities;

public interface IPolicyContentStore
{
    Task<PolicyContent> GetAsync();
    Task SaveAsync(PolicyContent content);
}

public sealed class PolicyContent
{
    public string WarrantyHtml { get; set; } = string.Empty;
    public string ShippingHtml { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
