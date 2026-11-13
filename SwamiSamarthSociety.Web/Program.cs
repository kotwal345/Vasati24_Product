using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SwamiSamarthSociety.Data;
using SwamiSamarthSociety.Data.Entities;
using SwamiSamarthSociety.Services;
using SwamiSamarthSociety.Web.BackgroundServices;
using SwamiSamarthSociety.Web.Identity;
using SwamiSamarthSociety.Web.Middleware;

namespace SwamiSamarthSociety.Web;

public class Program
{
    public static void Main(string[] args)
    {
        // Free for organizations under $1M annual revenue -- see https://questpdf.com/license/
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var builder = WebApplication.CreateBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentSocietyContext, CurrentSocietyContext>();

        builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;

                // Up from the framework default of 6 -- every account here is either
                // admin-provisioned (TempPasswordGenerator already produces 12-char passwords
                // meeting this) or self-chosen at the forced first-login change.
                options.Password.RequiredLength = 8;

                // Framework defaults, made explicit rather than implicitly relied on: 5 failed
                // attempts locks the account out for 5 minutes. Per-account, not per-IP -- see
                // the "auth" rate limiter below for the per-IP layer this doesn't cover.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        // Per-IP throttle on auth endpoints -- Identity's account lockout above is per-account,
        // so on its own it doesn't slow an attacker probing many different email addresses from
        // one IP. Partitioned by client IP (not a single shared bucket) so one busy legitimate
        // client can never rate-limit an unrelated one. Applied via [EnableRateLimiting("auth")].
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });

        builder.Services.AddScoped<IAuditLogService, AuditLogService>();
        builder.Services.AddScoped<IMemberService, MemberService>();
        builder.Services.AddScoped<IInterestCalculationService, InterestCalculationService>();
        builder.Services.AddScoped<IInstallmentCalculationService, InstallmentCalculationService>();
        builder.Services.AddScoped<ILoanService, LoanService>();
        builder.Services.AddScoped<IMonthlyCycleService, MonthlyCycleService>();
        builder.Services.AddScoped<IPaymentCollectionService, PaymentCollectionService>();

        builder.Services.AddHttpClient<ISmsSender, Msg91SmsSender>();
        builder.Services.AddScoped<IPaymentReminderService, PaymentReminderService>();
        builder.Services.AddHostedService<MonthlyReminderBackgroundService>();

        builder.Services.AddHttpClient<IWhatsAppSender, WhatsAppCloudApiSender>();
        builder.Services.AddScoped<IPaymentConfirmationService, PaymentConfirmationService>();

        builder.Services.AddControllersWithViews();

        var app = builder.Build();

        // One-off maintenance hook: `dotnet run -- --reset-password <email> <newPassword>` force-
        // resets a login's password and flags it for a mandatory change on next sign-in, for
        // accounts whose password the owner has lost (e.g. a pre-existing account that predates
        // admin-provisioned logins). Never runs during normal startup.
        if (args.Length == 3 && args[0] == "--reset-password")
        {
            using var scope = app.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var email = args[1];
            var newPassword = args[2];

            var user = db.Users.IgnoreQueryFilters().FirstOrDefault(u => u.NormalizedEmail == email.ToUpperInvariant());
            if (user is null)
            {
                Console.WriteLine($"No account found for {email}.");
                return;
            }

            var token = userManager.GeneratePasswordResetTokenAsync(user).GetAwaiter().GetResult();
            var result = userManager.ResetPasswordAsync(user, token, newPassword).GetAwaiter().GetResult();
            if (!result.Succeeded)
            {
                Console.WriteLine("Failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }

            user.MustChangePassword = true;
            userManager.UpdateAsync(user).GetAwaiter().GetResult();

            // Only society-scoped accounts get an audit entry -- AuditLog is per-society, and
            // the SuperAdmin's own account (SocietyId == null) has no society to attribute it to.
            if (user.SocietyId is { } societyId)
            {
                var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
                auditLogService.LogAsync(societyId, "AspNetUsers", 0, "PasswordResetByAdmin", "CLI: --reset-password", newValue: email)
                    .GetAwaiter().GetResult();
            }

            Console.WriteLine($"Password reset for {email}. They'll be prompted to set their own on next login.");
            return;
        }

        using (var scope = app.Services.CreateScope())
        {
            StartupSeeding.SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult();
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<RequirePasswordChangeMiddleware>();

        app.MapControllerRoute(
            name: "areas",
            pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();

        app.Run();
    }
}
