namespace LapTopBD.Utilities
{
    public class VisitorTrackingMiddleware
    {
        private const string VisitorCookieName = "laptopbd_visitor";

        private readonly RequestDelegate _next;

        public VisitorTrackingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IOnlineVisitorTracker tracker)
        {
            if (!context.Request.Path.StartsWithSegments("/css") &&
                !context.Request.Path.StartsWithSegments("/js") &&
                !context.Request.Path.StartsWithSegments("/images") &&
                !context.Request.Path.StartsWithSegments("/lib") &&
                !context.Request.Path.StartsWithSegments("/uploads") &&
                !context.Request.Path.StartsWithSegments("/avatar"))
            {
                if (!context.Request.Cookies.TryGetValue(VisitorCookieName, out var visitorId) ||
                    string.IsNullOrWhiteSpace(visitorId))
                {
                    visitorId = Guid.NewGuid().ToString("N");
                    context.Response.Cookies.Append(VisitorCookieName, visitorId, new CookieOptions
                    {
                        HttpOnly = true,
                        IsEssential = true,
                        Expires = DateTimeOffset.UtcNow.AddDays(30),
                        SameSite = SameSiteMode.Lax,
                        Secure = context.Request.IsHttps
                    });
                }

                var userAgent = context.Request.Headers.UserAgent.ToString();
                var forwardedIp = context.Request.Headers["X-Forwarded-For"].ToString();
                var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var ipAddress = string.IsNullOrWhiteSpace(forwardedIp) ? remoteIp : forwardedIp;

                await tracker.TrackVisitAsync(visitorId, userAgent, ipAddress, context.Request.Path.Value ?? string.Empty);
            }

            await _next(context);
        }
    }
}
