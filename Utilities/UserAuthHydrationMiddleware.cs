using Microsoft.AspNetCore.Authentication;

namespace LapTopBD.Utilities
{
    public class UserAuthHydrationMiddleware
    {
        private readonly RequestDelegate _next;

        public UserAuthHydrationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/Admin"))
            {
                await _next(context);
                return;
            }

            var authResult = await context.AuthenticateAsync("UserAuth");
            if (authResult?.Succeeded == true && authResult.Principal != null)
            {
                context.User = authResult.Principal;
            }

            await _next(context);
        }
    }
}