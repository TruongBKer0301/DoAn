using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace LapTopBD.Utilities
{
    public class PendingCheckoutData
    {
        public int UserId { get; set; }
        public string? Name { get; set; }
        public string? ContactNo { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
        public string? Address { get; set; }
        public long TotalPrice { get; set; }
        public string TransactionRef { get; set; } = string.Empty;
        public bool IsProcessed { get; set; }
        public List<PendingCheckoutItem> Items { get; set; } = new();
    }

    public class PendingCheckoutItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public interface IPendingCheckoutStore
    {
        Task SaveAsync(PendingCheckoutData data);
        Task<PendingCheckoutData?> GetAsync(string transactionRef);
        Task MarkProcessedAsync(string transactionRef);
        Task RemoveAsync(string transactionRef);
    }

    public class PendingCheckoutStore : IPendingCheckoutStore
    {
        private const string KeyPrefix = "pending_checkout_";
        private readonly IDistributedCache _cache;

        public PendingCheckoutStore(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task SaveAsync(PendingCheckoutData data)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
            };

            var cacheKey = BuildKey(data.TransactionRef);
            var payload = JsonSerializer.Serialize(data);
            await _cache.SetStringAsync(cacheKey, payload, options);
        }

        public async Task<PendingCheckoutData?> GetAsync(string transactionRef)
        {
            var cacheKey = BuildKey(transactionRef);
            var payload = await _cache.GetStringAsync(cacheKey);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            return JsonSerializer.Deserialize<PendingCheckoutData>(payload);
        }

        public async Task MarkProcessedAsync(string transactionRef)
        {
            var existing = await GetAsync(transactionRef);
            if (existing == null)
            {
                return;
            }

            existing.IsProcessed = true;
            await SaveAsync(existing);
        }

        public Task RemoveAsync(string transactionRef)
        {
            return _cache.RemoveAsync(BuildKey(transactionRef));
        }

        private static string BuildKey(string transactionRef)
        {
            return $"{KeyPrefix}{transactionRef}";
        }
    }
}
