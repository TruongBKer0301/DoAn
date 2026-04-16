using System.Text.Json;

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
            var json = JsonSerializer.Serialize(content, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
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

        return parsed;
    }

    private static PolicyContent CreateDefaultContent()
    {
        return new PolicyContent
        {
            WarrantyHtml = "<h2>CHINH SACH BAO HANH</h2><p>Ap dung cho cac san pham duoc mua tai cua hang va co thong tin don hang hop le.</p><h3>1. Thoi han bao hanh</h3><ul><li>Thoi han bao hanh theo tung san pham.</li><li>Tinh tu ngay khach hang nhan hang thanh cong.</li></ul><h3>2. Dieu kien duoc bao hanh</h3><ul><li>Loi ky thuat tu nha san xuat.</li><li>Con thong tin xac nhan don hang hop le.</li></ul><h3>3. Truong hop khong thuoc bao hanh</h3><ul><li>Roi vo, va dap, vao nuoc, su dung sai huong dan.</li><li>Hao mon tu nhien trong qua trinh su dung.</li></ul><h3>4. Quy trinh bao hanh</h3><ol><li>Lien he CSKH.</li><li>Cung cap ma don va mo ta loi.</li><li>Cua hang tiep nhan va phan hoi huong xu ly.</li></ol>",
            ShippingHtml = "<h2>CHINH SACH GIAO HANG</h2><p>Chinh sach ap dung cho don hang dat tai cua hang.</p><h3>1. Khu vuc giao hang</h3><ul><li>Giao hang toan quoc.</li><li>Khu vuc dac thu co the phat sinh them chi phi.</li></ul><h3>2. Thoi gian giao hang</h3><ul><li>Noi thanh: 1-2 ngay lam viec.</li><li>Ngoai thanh/tinh khac: 2-5 ngay lam viec.</li></ul><h3>3. Phi giao hang</h3><ul><li>Phi giao hang hien thi khi dat don.</li><li>Mien phi giao hang voi don du dieu kien (neu co).</li></ul><h3>4. Kiem tra khi nhan hang</h3><ul><li>Kiem tra tinh trang goi hang truoc khi ky nhan.</li><li>Neu co van de, lien he CSKH trong 24 gio.</li></ul>",
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
