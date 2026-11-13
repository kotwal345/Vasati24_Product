using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwamiSamarthSociety.Data;
using SwamiSamarthSociety.Data.Entities;
using SwamiSamarthSociety.Services;
using SwamiSamarthSociety.Web.Identity;
using SwamiSamarthSociety.Web.Models;

namespace SwamiSamarthSociety.Web.Controllers
{
    // Lets a SocietyAdmin provision Member logins within their own society. ApplicationUser has
    // no ambient tenant query filter (see ApplicationDbContext.OnModelCreating), so Index()
    // filters _db.Users by the caller's own SocietyId explicitly -- this controller never needs
    // to (and never should) accept a SocietyId from the caller.
    [Authorize(Roles = AppRoles.SocietyAdmin)]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICurrentSocietyContext _tenant;
        private readonly IAuditLogService _auditLog;

        public UsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ICurrentSocietyContext tenant, IAuditLogService auditLog)
        {
            _db = db;
            _userManager = userManager;
            _tenant = tenant;
            _auditLog = auditLog;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _db.Users.Where(u => u.SocietyId == _tenant.SocietyId).OrderBy(u => u.Email).ToListAsync();
            var vms = new List<UserListItemViewModel>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                vms.Add(new UserListItemViewModel
                {
                    Id = u.Id,
                    Email = u.Email ?? "",
                    Role = roles.FirstOrDefault() ?? "(none)",
                    MustChangePassword = u.MustChangePassword,
                    IsLockedOut = await _userManager.IsLockedOutAsync(u)
                });
            }
            return View(vms);
        }

        public IActionResult Create() => View(new CreateMemberLoginViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateMemberLoginViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var normalizedEmail = vm.Email.Trim().ToUpperInvariant();
            if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedEmail == normalizedEmail))
            {
                ModelState.AddModelError(nameof(vm.Email), "An account with this email already exists.");
                return View(vm);
            }

            var tempPassword = TempPasswordGenerator.Generate();
            var member = new ApplicationUser
            {
                UserName = vm.Email.Trim(),
                Email = vm.Email.Trim(),
                EmailConfirmed = true,
                SocietyId = _tenant.SocietyId,
                MustChangePassword = true
            };
            var result = await _userManager.CreateAsync(member, tempPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(vm);
            }
            await _userManager.AddToRoleAsync(member, AppRoles.Member);

            await _auditLog.LogAsync(_tenant.SocietyId!.Value, "AspNetUsers", 0, "MemberLoginCreated", User.Identity?.Name, newValue: member.Email);

            TempData["CreatedEmail"] = member.Email;
            TempData["CreatedTempPassword"] = tempPassword;
            return RedirectToAction(nameof(Created));
        }

        public new IActionResult Created()
        {
            if (TempData["CreatedEmail"] is not string email) return RedirectToAction(nameof(Index));

            var vm = new MemberLoginCreatedViewModel
            {
                Email = email,
                TempPassword = TempData["CreatedTempPassword"] as string ?? ""
            };
            return View(vm);
        }

        // Only ever targets Member-role logins within the caller's own society -- a SocietyAdmin
        // can't deactivate themselves or another SocietyAdmin this way (that's a SuperAdmin-only
        // concern, avoids a self-lockout footgun), and the SocietyId filter on the lookup means
        // an id from another society 404s rather than silently no-op'ing.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(string id)
        {
            var user = await FindOwnMemberAsync(id);
            if (user is null) return NotFound();

            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            await _auditLog.LogAsync(_tenant.SocietyId!.Value, "AspNetUsers", 0, "MemberLoginDeactivated", User.Identity?.Name, newValue: user.Email);
            TempData["Success"] = $"{user.Email} can no longer sign in.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(string id)
        {
            var user = await FindOwnMemberAsync(id);
            if (user is null) return NotFound();

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _auditLog.LogAsync(_tenant.SocietyId!.Value, "AspNetUsers", 0, "MemberLoginReactivated", User.Identity?.Name, newValue: user.Email);
            TempData["Success"] = $"{user.Email} can sign in again.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<ApplicationUser?> FindOwnMemberAsync(string id)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.SocietyId == _tenant.SocietyId);
            if (user is null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains(AppRoles.Member) ? user : null;
        }
    }
}
