using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SwamiSamarthSociety.Data.Entities;
using SwamiSamarthSociety.Services;

namespace SwamiSamarthSociety.Web.Reporting
{
    // Bank-style loan statement: loan summary + full EMI schedule (billed months with their real
    // status, then projected months until the balance reaches zero) + expected closing month.
    public static class LoanSchedulePdfExporter
    {
        private const string FontFamily = "Nirmala UI";
        private const string HeaderBg = "#BDD7EE";
        private const string TitleBg = "#C6E0B4";
        private const string ProjectedBg = "#F2F2F2";
        private const string BorderColor = "#B7BEC9";

        private sealed record ScheduleRow(
            int No, DateTime Month, decimal Opening, decimal Principal, decimal Interest,
            decimal Closing, string Status, bool Projected)
        {
            public decimal Emi => Principal + Interest;
        }

        public static byte[] Export(
            Society society,
            Loan loan,
            IEnumerable<LoanInstallmentRule> rules,
            IInstallmentCalculationService installmentCalc,
            IInterestCalculationService interestCalc)
        {
            var rows = BuildSchedule(loan, rules.ToList(), installmentCalc, interestCalc);
            // The regular monthly principal as billed, not a one-off prepayment that inflates row 1.
            var regularEmi = loan.Installments.OrderBy(i => i.InstallmentNumber).FirstOrDefault()?.PrincipalAmount
                             ?? rows.FirstOrDefault()?.Principal ?? 0m;
            var last = rows.LastOrDefault();
            var closingMonth = last is null ? "-" : last.Month.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
            var totalInterest = rows.Sum(r => r.Interest);
            var totalPrincipal = rows.Sum(r => r.Principal);
            var titleLine = $"|| {society.NameMarathi ?? society.Name} ||";
            var guarantors = string.Join(", ", loan.Guarantors.Select(g => g.GuarantorMember.FullName));

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(9));

                    page.Header().Background(TitleBg).Padding(6).Column(col =>
                    {
                        col.Item().AlignCenter().Text(titleLine).Bold().FontSize(14);
                        if (!string.IsNullOrWhiteSpace(society.Address))
                            col.Item().AlignCenter().Text(society.Address).FontSize(9);
                        col.Item().PaddingTop(2).AlignCenter().Text($"Loan Statement & EMI Schedule - {loan.LoanNumber}").Bold().FontSize(11);
                    });

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        col.Spacing(8);

                        col.Item().Border(0.5f).BorderColor(BorderColor).Padding(6).Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                            void Pair(string label, string value)
                            {
                                t.Cell().PaddingVertical(2).Text(label).FontColor("#555555");
                                t.Cell().PaddingVertical(2).Text(value).Bold();
                            }
                            Pair("Member", loan.Member.FullName);
                            Pair("Loan No.", loan.LoanNumber);
                            Pair("Loan Amount", $"Rs. {loan.OriginalLoanAmount:N0}");
                            Pair("Interest Rate", $"{loan.InterestRate}% per month (on balance)");
                            Pair("Loan Date", loan.LoanDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture));
                            Pair("Status", loan.Status);
                            Pair("Monthly Principal (EMI)", regularEmi > 0 ? $"Rs. {regularEmi:N0}" : "-");
                            Pair("No. of Installments", rows.Count.ToString());
                            Pair("Total Interest", $"Rs. {totalInterest:N0}");
                            Pair("Total Payable", $"Rs. {totalPrincipal + totalInterest:N0}");
                            Pair("Expected Closing Month", closingMonth);
                            Pair("Outstanding Principal", $"Rs. {loan.OutstandingPrincipal:N0}");
                            if (!string.IsNullOrWhiteSpace(guarantors))
                            {
                                Pair("Guarantors", guarantors);
                                t.Cell(); t.Cell();
                            }
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(28);
                                c.RelativeColumn(1.4f);
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn(1.1f);
                            });

                            table.Header(h =>
                            {
                                foreach (var title in new[] { "No.", "Month", "Opening Balance", "Principal", "Interest", "Total EMI", "Closing Balance", "Status" })
                                    h.Cell().Background(HeaderBg).Border(0.5f).BorderColor(BorderColor)
                                        .Padding(3).AlignCenter().Text(title).Bold().FontSize(8);
                            });

                            foreach (var r in rows)
                            {
                                var bg = r.Projected ? ProjectedBg : "#FFFFFF";
                                IContainer Cell() => table.Cell().Background(bg).Border(0.5f).BorderColor(BorderColor).Padding(3);
                                Cell().AlignCenter().Text(r.No.ToString());
                                Cell().Text(r.Month.ToString("MMM yyyy", CultureInfo.InvariantCulture));
                                Cell().AlignRight().Text($"{r.Opening:N0}");
                                Cell().AlignRight().Text($"{r.Principal:N0}");
                                Cell().AlignRight().Text($"{r.Interest:N0}");
                                Cell().AlignRight().Text($"{r.Emi:N0}").Bold();
                                Cell().AlignRight().Text($"{r.Closing:N0}");
                                Cell().AlignCenter().Text(r.Status);
                            }

                            IContainer Total() => table.Cell().Background(TitleBg).Border(0.5f).BorderColor(BorderColor).Padding(3);
                            Total().Text("");
                            Total().Text("Total").Bold();
                            Total().Text("");
                            Total().AlignRight().Text($"{totalPrincipal:N0}").Bold();
                            Total().AlignRight().Text($"{totalInterest:N0}").Bold();
                            Total().AlignRight().Text($"{totalPrincipal + totalInterest:N0}").Bold();
                            Total().Text("");
                            Total().Text("");
                        });

                        col.Item().Text("Rows marked 'Scheduled' are projected assuming every installment is paid on time. " +
                                        "Penalties, arrears and share contributions are not included. Actual amounts may differ.")
                            .FontSize(7).FontColor("#666666");
                    });

                    page.Footer().Row(r =>
                    {
                        r.RelativeItem().Text($"Generated {DateTime.Now:dd-MMM-yyyy}").FontSize(7).FontColor("#666666");
                        r.RelativeItem().AlignRight().Text(x =>
                        {
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                    });
                });
            }).GeneratePdf();
        }

        private static List<ScheduleRow> BuildSchedule(
            Loan loan,
            List<LoanInstallmentRule> rules,
            IInstallmentCalculationService installmentCalc,
            IInterestCalculationService interestCalc)
        {
            var rows = new List<ScheduleRow>();
            var billed = loan.Installments.OrderBy(i => i.InstallmentNumber).ToList();

            foreach (var i in billed)
            {
                var month = new DateTime(i.MonthlyCycle.Year, i.MonthlyCycle.Month, 1);

                // Once money has been collected, show what was actually paid rather than what was due:
                // a prepayment (e.g. whole loan closed in one go) can far exceed the regular EMI.
                // ClosingPrincipal already reflects the principal actually paid.
                var principal = i.PrincipalAmount;
                var interest = i.InterestAmount;
                if (i.PaidAmount > 0)
                {
                    principal = Math.Max(0, i.OpeningPrincipal - i.ClosingPrincipal);
                    interest = Math.Min(i.PaidAmount, i.InterestAmount);
                }

                rows.Add(new ScheduleRow(i.InstallmentNumber, month, i.OpeningPrincipal, principal,
                    interest, i.ClosingPrincipal, i.Status, false));
            }

            // Outstanding principal only drops once an installment is paid, so a still-unpaid latest
            // installment's principal must come off before projecting the months after it.
            var remaining = loan.OutstandingPrincipal;
            var lastBilled = billed.LastOrDefault();
            if (lastBilled is not null && lastBilled.Status != "Paid")
                remaining = Math.Max(0, remaining - lastBilled.PrincipalAmount);

            var next = lastBilled is not null
                ? new DateTime(lastBilled.MonthlyCycle.Year, lastBilled.MonthlyCycle.Month, 1).AddMonths(1)
                : new DateTime(loan.LoanDate.Year, loan.LoanDate.Month, 1).AddMonths(1);
            var no = (lastBilled?.InstallmentNumber ?? 0) + 1;

            // Cap protects against a pathological custom installment (e.g. tiny EMI) looping forever.
            for (var guard = 0; remaining > 0 && guard < 600; guard++)
            {
                var principal = installmentCalc.CalculateMonthlyPrincipalInstallment(
                    loan.OriginalLoanAmount, remaining, loan.InstallmentAmount, rules);
                if (principal <= 0) break;
                var interest = interestCalc.CalculateMonthlyInterest(remaining, loan.InterestRate);
                var closing = remaining - principal;
                rows.Add(new ScheduleRow(no++, next, remaining, principal, interest, closing, "Scheduled", true));
                remaining = closing;
                next = next.AddMonths(1);
            }

            return rows;
        }
    }
}
