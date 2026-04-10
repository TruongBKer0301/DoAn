namespace LapTopBD.Utilities
{
    public interface IOnlineVisitorTracker
    {
        Task TrackVisitAsync(string visitorId, string userAgent, string ipAddress, string path);
        Task<int> GetOnlineCountAsync(TimeSpan activeWindow);
        Task<int> GetTotalVisitCountAsync();
        Task<IReadOnlyList<int>> GetDailyVisitCountsAsync(int year, int month);
        Task<IReadOnlyList<KeyValuePair<string, int>>> GetTopBrowsersAsync(int topCount);
        Task<IReadOnlyList<KeyValuePair<string, int>>> GetDeviceBreakdownAsync();
        Task<IReadOnlyList<KeyValuePair<string, int>>> GetTopIpsAsync(int topCount);
    }
}
