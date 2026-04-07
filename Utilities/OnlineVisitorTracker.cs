using LapTopBD.Data;
using LapTopBD.Models;
using Microsoft.EntityFrameworkCore;

namespace LapTopBD.Utilities
{
    public class OnlineVisitorTracker : IOnlineVisitorTracker
    {
        private readonly ApplicationDbContext _dbContext;

        public OnlineVisitorTracker(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task TrackVisitAsync(string visitorId, string userAgent, string ipAddress, string path)
        {
            if (string.IsNullOrWhiteSpace(visitorId))
            {
                return;
            }

            var safeUserAgent = userAgent ?? string.Empty;

            _dbContext.VisitLogs.Add(new VisitLog
            {
                VisitorId = visitorId,
                VisitedAtUtc = DateTime.UtcNow,
                Path = path,
                UserAgent = safeUserAgent.Length > 512
                    ? safeUserAgent[..512]
                    : safeUserAgent,
                Browser = DetectBrowser(safeUserAgent),
                Device = DetectDevice(safeUserAgent),
                IpAddress = NormalizeIp(ipAddress)
            });

            await _dbContext.SaveChangesAsync();
        }

        public async Task<int> GetOnlineCountAsync(TimeSpan activeWindow)
        {
            var threshold = DateTime.UtcNow.Subtract(activeWindow);

            return await _dbContext.VisitLogs
                .AsNoTracking()
                .Where(v => v.VisitedAtUtc >= threshold)
                .Select(v => v.VisitorId)
                .Distinct()
                .CountAsync();
        }

        public async Task<int> GetTotalVisitCountAsync()
        {
            return await _dbContext.VisitLogs.AsNoTracking().CountAsync();
        }

        public async Task<IReadOnlyList<int>> GetDailyVisitCountsAsync(int year, int month)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var daily = new List<int>(daysInMonth);

            var monthStartUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEndUtc = monthStartUtc.AddMonths(1);

            var grouped = await _dbContext.VisitLogs
                .AsNoTracking()
                .Where(v => v.VisitedAtUtc >= monthStartUtc && v.VisitedAtUtc < monthEndUtc)
                .GroupBy(v => v.VisitedAtUtc.Day)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .ToListAsync();

            var lookup = grouped.ToDictionary(x => x.Day, x => x.Count);

            for (var day = 1; day <= daysInMonth; day++)
            {
                daily.Add(lookup.TryGetValue(day, out var count) ? count : 0);
            }

            return daily;
        }

        public async Task<IReadOnlyList<KeyValuePair<string, int>>> GetTopBrowsersAsync(int topCount)
        {
            var grouped = await _dbContext.VisitLogs
                .AsNoTracking()
                .GroupBy(v => v.Browser)
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(topCount)
                .ToListAsync();

            return grouped
                .Select(x => new KeyValuePair<string, int>(string.IsNullOrWhiteSpace(x.Key) ? "Khac" : x.Key, x.Count))
                .ToList();
        }

        public async Task<IReadOnlyList<KeyValuePair<string, int>>> GetDeviceBreakdownAsync()
        {
            var grouped = await _dbContext.VisitLogs
                .AsNoTracking()
                .GroupBy(v => v.Device)
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return grouped
                .Select(x => new KeyValuePair<string, int>(string.IsNullOrWhiteSpace(x.Key) ? "Khac" : x.Key, x.Count))
                .ToList();
        }

        public async Task<IReadOnlyList<KeyValuePair<string, int>>> GetTopIpsAsync(int topCount)
        {
            var grouped = await _dbContext.VisitLogs
                .AsNoTracking()
                .GroupBy(v => v.IpAddress)
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(topCount)
                .ToListAsync();

            return grouped
                .Select(x => new KeyValuePair<string, int>(string.IsNullOrWhiteSpace(x.Key) ? "Unknown" : x.Key, x.Count))
                .ToList();
        }

        private static string NormalizeIp(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return "Unknown";
            }

            var first = ipAddress.Split(',').FirstOrDefault();
            return string.IsNullOrWhiteSpace(first) ? "Unknown" : first.Trim();
        }

        private static string DetectBrowser(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return "Khac";
            }

            var ua = userAgent.ToLowerInvariant();
            if (ua.Contains("edg")) return "Microsoft Edge";
            if (ua.Contains("opr") || ua.Contains("opera")) return "Opera";
            if (ua.Contains("chrome") && !ua.Contains("edg")) return "Chrome";
            if (ua.Contains("safari") && !ua.Contains("chrome")) return "Safari";
            if (ua.Contains("firefox")) return "Mozilla Firefox";
            if (ua.Contains("trident") || ua.Contains("msie")) return "Internet Explorer";

            return "Khac";
        }

        private static string DetectDevice(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return "Desktop";
            }

            var ua = userAgent.ToLowerInvariant();
            if (ua.Contains("tablet") || ua.Contains("ipad")) return "Tablet";
            if (ua.Contains("mobi") || ua.Contains("android") || ua.Contains("iphone")) return "Mobile";

            return "Desktop";
        }
    }
}
