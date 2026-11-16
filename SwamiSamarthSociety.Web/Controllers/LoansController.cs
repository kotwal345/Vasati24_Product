using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwamiSamarthSociety.Data;
using SwamiSamarthSociety.Data.Entities;
using SwamiSamarthSociety.Services;
using SwamiSamarthSociety.Web.Models;
using SwamiSamarthSociety.Web.Reporting;

namespace SwamiSamarthSociety.Web.Controllers
{
    [Authorize]
    public class LoansController : Controller
    {
        private readonly ILoanService _loanService;
        private readonly ApplicationDbContext _db;
        private readonly ICurrentSocietyContext _tenant;
        private readonly IInstallmentCalculationService _installmentCalc;
        private readonly IInterestCalculationService _interestCalc;

        public LoansController(
            ILoanService loanService,
            ApplicationDbContext db,
            ICurrentSocietyContext tenant,
            IInstallmentCalculationService installmentCalc,
            IInterestCalculationService interestCalc)
        {
            _loanService = loanService;
            _db = db;
            _tenant = tenant;
            _installmentCalc = installmentCalc;
            _interestCalc = interestCalc;
        }

        public async Task<IActionResult> ExportPdf(int id)
        {
            var loan = await _loanService.GetByIdAsync(id);
            if (loan is null) return NotFound();

            var society = await _db.Societies.FirstAsync(s => s.SocietyId == _tenant.SocietyId);
            var rules = await _db.LoanInstallmentRules.Where(r => r.IsActive).ToListAsync();
            var bytes = LoanSchedulePdfExporter.Export(society, loan, rules, _installmentCalc, _interestCalc);
            return File(bytes, "application/pdf", $"{loan.LoanNumber}_EMI_Schedule.pdf");
        }

        public async Task<IActionResult> Index(string? status = null)
        {
            var loans = await _loanService.GetAllAsync();
            if (!string.IsNullOrEmpty(status))
                loans = loans.Where(l => l.Status == status).ToList();
            ViewBag.StatusFilter = status;
            return View(loans);
        }

        public async Task<IActionResult> Details(int id)
        {
            var loan = await _loanService.GetByIdAsync(id);
            if (loan is null) return NotFound();
            return View(loan);
        }

        [Authorize(Roles = AppRoles.SocietyAdmin)]
        public async Task<IActionResult> Create()
        {
            var vm = new LoanCreateViewModel
            {
                BorrowerCandidates = await _loanService.GetBorrowerCandidatesAsync(),
                GuarantorsRequired = await _loanService.GetGuarantorsRequiredAsync()
            };
            vm.GuarantorCandidates = vm.BorrowerCandidates; // refined once a borrower is picked (client-side excludes self)
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.SocietyAdmin)]
        public async Task<IActionResult> Create(LoanCreateViewModel vm)
        {
            var errors = ModelState.IsValid
                ? await _loanService.ValidateNewLoanAsync(vm.MemberId, vm.OriginalLoanAmount, vm.GuarantorMemberIds)
                : new List<string>();

            foreach (var error in errors)
                ModelState.AddModelError(string.Empty, error);

            if (!ModelState.IsValid)
            {
                vm.BorrowerCandidates = await _loanService.GetBorrowerCandidatesAsync();
                vm.GuarantorCandidates = await _loanService.GetGuarantorCandidatesAsync(vm.MemberId);
                vm.GuarantorsRequired = await _loanService.GetGuarantorsRequiredAsync();
                return View(vm);
            }

            var loan = new Loan
            {
                MemberId = vm.MemberId,
                OriginalLoanAmount = vm.OriginalLoanAmount,
                LoanDate = vm.LoanDate,
                InterestRate = vm.InterestRate,
                Remarks = vm.Remarks,
                ApprovedBy = User.Identity?.Name,
                ApprovedDate = DateTime.UtcNow,
                DisbursementDate = vm.LoanDate
            };

            var created = await _loanService.CreateAsync(loan, vm.GuarantorMemberIds);
            TempData["Success"] = $"Loan {created.LoanNumber} of ₹{created.OriginalLoanAmount:N0} created.";
            return RedirectToAction(nameof(Details), new { id = created.LoanId });
        }
    }
}
