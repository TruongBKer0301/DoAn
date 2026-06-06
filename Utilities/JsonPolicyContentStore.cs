using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LapTopBD.Utilities;

public sealed class JsonPolicyContentStore : IPolicyContentStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonPolicyContentStore(IWebHostEnvironment env)
    {
        var appDataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, "policies.json");
    }

    public async Task<PolicyContent> GetAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return await LoadOrCreateInternalAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(PolicyContent content)
    {
        await _lock.WaitAsync();
        try
        {
            content.UpdatedAtUtc = DateTime.UtcNow;
            NormalizeContent(content);
            var json = JsonSerializer.Serialize(content, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<PolicyItem>> GetAllAsync()
    {
        var content = await GetAsync();
        return content.Policies
            .OrderBy(policy => policy.Title)
            .ToList();
    }

    public async Task<PolicyItem?> GetByIdAsync(Guid id)
    {
        var content = await GetAsync();
        return content.Policies.FirstOrDefault(policy => policy.Id == id);
    }

    public async Task<PolicyItem?> GetBySlugAsync(string slug)
    {
        var normalizedSlug = NormalizeSlug(slug);
        var content = await GetAsync();
        return content.Policies.FirstOrDefault(policy =>
            policy.IsPublished &&
            string.Equals(policy.Slug, normalizedSlug, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PolicyItem> UpsertAsync(PolicyItem policy)
    {
        await _lock.WaitAsync();
        try
        {
            var content = await LoadOrCreateInternalAsync();
            NormalizePolicy(policy);
            EnsureUniqueSlug(content, policy);

            var existing = content.Policies.FirstOrDefault(item => item.Id == policy.Id);
            if (existing is null)
            {
                policy.Id = policy.Id == Guid.Empty ? Guid.NewGuid() : policy.Id;
                policy.CreatedAtUtc = DateTime.UtcNow;
                policy.UpdatedAtUtc = DateTime.UtcNow;
                content.Policies.Add(policy);
            }
            else
            {
                existing.Title = policy.Title;
                existing.Slug = policy.Slug;
                existing.HtmlContent = policy.HtmlContent;
                existing.IsPublished = policy.IsPublished;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                policy = existing;
            }

            content.UpdatedAtUtc = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(content, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json);

            return policy;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var content = await LoadOrCreateInternalAsync();
            var removed = content.Policies.RemoveAll(policy => policy.Id == id) > 0;
            if (!removed)
            {
                return false;
            }

            content.UpdatedAtUtc = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(content, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<PolicyContent> LoadOrCreateInternalAsync()
    {
        if (!File.Exists(_filePath))
        {
            var seed = CreateDefaultContent();
            var seedJson = JsonSerializer.Serialize(seed, JsonOptions);
            await File.WriteAllTextAsync(_filePath, seedJson);
            return seed;
        }

        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            var seed = CreateDefaultContent();
            var seedJson = JsonSerializer.Serialize(seed, JsonOptions);
            await File.WriteAllTextAsync(_filePath, seedJson);
            return seed;
        }

        var parsed = JsonSerializer.Deserialize<PolicyContent>(json);
        if (parsed is null)
        {
            var seed = CreateDefaultContent();
            var seedJson = JsonSerializer.Serialize(seed, JsonOptions);
            await File.WriteAllTextAsync(_filePath, seedJson);
            return seed;
        }

        NormalizeContent(parsed);
        if (parsed.Policies.Count == 0)
        {
            parsed = CreateDefaultContent(parsed.WarrantyHtml, parsed.ShippingHtml);
            var migratedJson = JsonSerializer.Serialize(parsed, JsonOptions);
            await File.WriteAllTextAsync(_filePath, migratedJson);
        }

        return parsed;
    }

    private static PolicyContent CreateDefaultContent(string? warrantyHtml = null, string? shippingHtml = null)
    {
        var warranty = string.IsNullOrWhiteSpace(warrantyHtml)
            ? "<h2>CHINH SACH BAO HANH</h2><p>Ap dung cho cac san pham duoc mua tai cua hang va co thong tin don hang hop le.</p><h3>1. Thoi han bao hanh</h3><ul><li>Thoi han bao hanh theo tung san pham.</li><li>Tinh tu ngay khach hang nhan hang thanh cong.</li></ul><h3>2. Dieu kien duoc bao hanh</h3><ul><li>Loi ky thuat tu nha san xuat.</li><li>Con thong tin xac nhan don hang hop le.</li></ul><h3>3. Truong hop khong thuoc bao hanh</h3><ul><li>Roi vo, va dap, vao nuoc, su dung sai huong dan.</li><li>Hao mon tu nhien trong qua trinh su dung.</li></ul><h3>4. Quy trinh bao hanh</h3><ol><li>Lien he CSKH.</li><li>Cung cap ma don va mo ta loi.</li><li>Cua hang tiep nhan va phan hoi huong xu ly.</li></ol>"
            : warrantyHtml;
        var shipping = string.IsNullOrWhiteSpace(shippingHtml)
            ? "<h2>CHINH SACH GIAO HANG</h2><p>Chinh sach ap dung cho don hang dat tai cua hang.</p><h3>1. Khu vuc giao hang</h3><ul><li>Giao hang toan quoc.</li><li>Khu vuc dac thu co the phat sinh them chi phi.</li></ul><h3>2. Thoi gian giao hang</h3><ul><li>Noi thanh: 1-2 ngay lam viec.</li><li>Ngoai thanh/tinh khac: 2-5 ngay lam viec.</li></ul><h3>3. Phi giao hang</h3><ul><li>Phi giao hang hien thi khi dat don.</li><li>Mien phi giao hang voi don du dieu kien (neu co).</li></ul><h3>4. Kiem tra khi nhan hang</h3><ul><li>Kiem tra tinh trang goi hang truoc khi ky nhan.</li><li>Neu co van de, lien he CSKH trong 24 gio.</li></ul>"
            : shippingHtml;
        var now = DateTime.UtcNow;

        var content = new PolicyContent
        {
            WarrantyHtml = warranty,
            ShippingHtml = shipping,
            UpdatedAtUtc = now,
            Policies = new List<PolicyItem>
            {
                new()
                {
                    Title = "Chinh sach bao hanh",
                    Slug = "bao-hanh",
                    HtmlContent = warranty,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                },
                new()
                {
                    Title = "Chinh sach giao hang",
                    Slug = "giao-hang",
                    HtmlContent = shipping,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                }
            }
        };

        return content;
    }

    private static void NormalizeContent(PolicyContent content)
    {
        content.Policies ??= new List<PolicyItem>();
        foreach (var policy in content.Policies)
        {
            NormalizePolicy(policy);
        }
    }

    private static void NormalizePolicy(PolicyItem policy)
    {
        policy.Title = (policy.Title ?? string.Empty).Trim();
        policy.HtmlContent ??= string.Empty;
        policy.Slug = NormalizeSlug(string.IsNullOrWhiteSpace(policy.Slug) ? policy.Title : policy.Slug);
        if (policy.CreatedAtUtc == default)
        {
            policy.CreatedAtUtc = DateTime.UtcNow;
        }
        if (policy.UpdatedAtUtc == default)
        {
            policy.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private static void EnsureUniqueSlug(PolicyContent content, PolicyItem policy)
    {
        var baseSlug = string.IsNullOrWhiteSpace(policy.Slug) ? "policy" : policy.Slug;
        var slug = baseSlug;
        var index = 2;

        while (content.Policies.Any(item =>
            item.Id != policy.Id &&
            string.Equals(item.Slug, slug, StringComparison.OrdinalIgnoreCase)))
        {
            slug = $"{baseSlug}-{index}";
            index++;
        }

        policy.Slug = slug;
    }

    private static string NormalizeSlug(string value)
    {
        var slug = (value ?? string.Empty).Trim()
            .Replace("\u0111", "d")
            .Replace("\u0110", "D");
        slug = string.Concat(slug.Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark))
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9]+", "-");
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "policy" : slug;
    }
}
