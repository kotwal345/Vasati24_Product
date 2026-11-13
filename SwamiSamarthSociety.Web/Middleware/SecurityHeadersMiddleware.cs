namespace SwamiSamarthSociety.Web.Middleware
{
    // Baseline security response headers applied to every request. The CSP allows
    // 'unsafe-inline' for scripts/styles because several existing views (chart tooltip JS,
    // dashboard search JS, a handful of inline style="" attributes) aren't nonce-based yet --
    // still meaningfully tighter than no policy, since it blocks any *external* script/resource
    // origin from ever being loaded. Tightening to nonces is a follow-up, not a blocker.
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self'; " +
                "frame-ancestors 'none'";

            await _next(context);
        }
    }
}
