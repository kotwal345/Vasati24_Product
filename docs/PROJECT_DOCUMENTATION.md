# Vasati24 — Society Management System
### Full Project Documentation & Demo Guide

*(Product name: **Vasati24** — "सभासद व्यवस्थापन प्रणाली" / Member Management System. First client: Swami Samarth Society.)*

---

## 1. What is Vasati24?

Vasati24 is a web application for **housing and credit societies** (सोसायटी / पतसंस्था). It replaces paper registers, Excel sheets and hand-sent WhatsApp reminders with one secure system that handles:

- **Members** — records, mobile numbers, joining/leaving
- **Monthly share collection** — every member pays a fixed monthly share (default ₹500)
- **Loans** — issue loans with guarantors, calculate EMIs and interest automatically
- **Monthly collection sheet** — one screen to record who paid what; exports to Excel and PDF
- **Reminders** — automatic SMS reminders and WhatsApp payment confirmations in Marathi
- **Multi-society (SaaS)** — one installation serves many societies, each fully isolated

**One-line pitch:** *"Your whole society's accounts, loans and reminders — in one place, in Marathi and English."*

---

## 2. Who uses it — three roles

| Role | Who | What they can do |
|---|---|---|
| **SuperAdmin** | Product owner (us) | Create / deactivate societies, view activity across societies. Has no society of their own. |
| **SocietyAdmin** | Society अध्यक्ष / chairman / treasurer | Full control inside their own society: members, loans, collection, reminders, logins. |
| **Member** | Ordinary society member | Read-only view of their own society's data. |

Members can only ever see **their own society's** data — never another society's.

---

## 3. Feature walk-through (what to show clients)

### 3.1 Public landing page
Signed-out visitors see a marketing page: features, why Vasati24, contact. Login button leads to the secure sign-in.

### 3.2 Login & security
- Email + password sign-in
- Admin-created accounts get a **temporary password** and are **forced to change it on first login**
- 5 wrong attempts locks the account for 5 minutes
- Login attempts are rate-limited per IP address

### 3.3 Dashboard
Totals at a glance: total/active members, active loans, outstanding loan principal, share/principal/interest collected, current bank balance, and a month-by-month history chart.

### 3.4 Members
Add and edit members with English + Marathi names, mobile number, address, joining date and monthly share amount. Status: Active / Inactive / Left. Each member gets a code (MEM001, MEM002 …).

### 3.5 Loans
- Create a loan for an active member who has **no other active loan**
- Choose the **required number of guarantors** (default 2) — a member cannot guarantee their own loan
- Loan amount is checked against the society's **maximum loan amount** (default ₹50,000)
- Loan numbers are generated automatically (LN001, LN002 …)
- **Loan Details page** shows the full installment history
- **Export PDF (new):** a bank-style **Loan Statement & EMI Schedule** — loan summary, month-by-month EMI table, total interest, total payable and the **expected closing month**

### 3.6 Monthly collection cycle (the heart of the system)
1. Admin clicks **Open Next Cycle** (one open cycle at a time; opening bank balance carries over from the last closed cycle).
2. The system **automatically creates an installment for every active loan** (principal + interest) and a share line for every member.
3. The **Collection Sheet** shows every member's row: share, opening loan balance, principal due, interest due, penalty, arrears, amount to collect, remaining balance.
4. Admin types the amounts received and clicks **Save** on each row. Totals at the top update (collected share, principal + interest collected, total collection, bank balance).
5. At month-end admin **closes the cycle** with the closing bank balance. A closed cycle can no longer be edited.

**Exports from the sheet**
| Button | Output |
|---|---|
| Export Excel | Full collection sheet workbook |
| Export PDF | Full collection sheet (landscape A4) — matches the society's paper register layout |
| **Export Collection PDF** | Slim portrait PDF: serial no., member name, total amount only |
| Recalculate Penalties & Arrears | Re-checks unpaid rows against last month's payment status |

### 3.6.1 Paying a loan off early
Enter **remaining principal + this month's interest** in the Loan column (share stays in the Share column). Example: balance ₹23,000 + interest ₹460 → enter **₹23,460**. The loan closes automatically and its status becomes **Completed**.

### 3.7 Reminders (SMS) & payment confirmation (WhatsApp)
- **Automatic reminders:** on the **18th, 19th and 20th** of each month, between **7 am and 12 noon**, the system sends each member an SMS in Marathi with the amount due. It sends **once per day per society**, even if the server restarts.
- Members with a loan get principal + interest + share; others get the share amount.
- **Send Now** button for manual sending.
- **WhatsApp confirmation:** when a payment is saved, the member gets a Marathi WhatsApp message with date and amount (requires Meta Business setup — see §9).
- Every message is logged (success/failure, provider response).

### 3.8 Logins management
Admin creates logins for members (temporary password shown once), and can deactivate / reactivate them. All actions are audited.

### 3.9 SuperAdmin console
Create a new society (name in English + Marathi, code, contact, address) — the system also creates that society's admin login with a one-time temporary password. Deactivate/reactivate societies; view an activity log.

---

## 4. Business rules (the "money logic")

These are configurable per society and drive every calculation.

### 4.1 Monthly share
Default **₹500 per member per month**; accumulated as the member's total share.

### 4.2 Interest
`Interest = Outstanding principal × rate ÷ 100` — default **2% per month on the reducing balance**, rounded to the nearest rupee.

### 4.3 Principal installment (EMI) — by loan-size band
Stored in the **LoanInstallmentRule** table (`MinimumAmount`, `MaximumAmount`, and either a percentage or a number of installments):

- **Percentage band:** installment = original loan × percentage. The last installment is capped so the loan closes exactly.
- **Fixed-count band** (e.g. small loans ≤ ₹5,000): installment = loan ÷ number of installments.
- A loan's own custom installment amount, if set, overrides the band.

### 4.4 Late payment — penalty & arrears (Rule "b")
Marathi rule: *"हफ्ता न भरल्यास शिल्लक कर्ज रकमेवर ५% दंड आकारला जाईल."*
If last month's installment was **not fully paid**, the next installment carries:
- a **penalty** = 5% (configurable, `InstallmentPenaltyPercent`) of the **unpaid shortfall** only, and
- **arrears** = that same shortfall, demanded back on top of the regular principal.

### 4.5 Settings (AppSetting table, per society)
`MaxLoanAmount` (default 50,000) · `GuarantorsRequired` (default 2) · `InstallmentPenaltyPercent` (default 5) · `LastPaymentReminderSentDate` (system-maintained).

---

## 5. Worked example (use this in the video)

**Member: Vivek Batwal — loan ₹23,000, November 2026**

| Item | Amount |
|---|---|
| Monthly share | ₹500 |
| Principal installment | ₹4,600 |
| Interest (2% × 23,000) | ₹460 |
| **Total due this month** | **₹5,560** |
| Balance after paying | ₹18,400 |

**Early payoff the same month:** loan column **₹23,460** (principal 23,000 + interest 460) + share ₹500 = ₹23,960 collected. Loan → **Completed**, balance ₹0.

---

## 6. Technology & concepts used

| Area | Technology / concept |
|---|---|
| Framework | **ASP.NET Core 8 MVC** (C#), Razor views |
| Database | **SQL Server** via **Entity Framework Core 8** (code-first migrations) |
| Authentication | **ASP.NET Core Identity** with roles, lockout, forced password change |
| Architecture | **3-layer solution:** `Data` (entities, DbContext) → `Services` (business logic) → `Web` (controllers, views) |
| Multi-tenancy | Shared database, **`SocietyId` on every table**; EF Core **global query filters** auto-restrict every query to the current society |
| Dependency injection | Services registered as interfaces (`ILoanService`, `IMonthlyCycleService`, …) |
| Background jobs | `BackgroundService` (hosted service) wakes every 30 min for reminders |
| PDF | **QuestPDF** (Community licence), Marathi font "Nirmala UI" |
| Excel | **ClosedXML** |
| SMS | **MSG91** HTTP API |
| WhatsApp | **Meta WhatsApp Cloud API** (approved Marathi template `payment_confirmation`) |
| Auditing | `AuditLog` table records sensitive actions (who, what, when) |
| Security | Secure/HttpOnly cookies, rate limiting, CSP + security headers (X-Frame-Options, nosniff, …), per-tenant isolation |
| Deployment | **Docker** (multi-stage .NET 8 image, port 10000) |
| Localisation | Marathi names, headers and message text alongside English |

### How multi-tenancy works (simple explanation for clients)
Every record belongs to one society. When someone signs in, the system knows their society and **automatically hides everything else** — a society admin can never see another society's members or money, even by accident. Only the SuperAdmin sees across societies.

### Request flow
`Browser → Controller (checks role) → Service (business rules) → DbContext (filtered to the society) → SQL Server`

---

## 7. Data model (main tables)

| Table | Purpose |
|---|---|
| Society | The tenant (a client society) |
| AspNetUsers / Roles | Logins and roles; users link to a society |
| Member | Member profile, share amount, status |
| MonthlyCycle | One row per month: expected/collected share, principal & interest collected, opening/closing bank balance, Open/Closed |
| MemberSharePayment | A member's share payment for a cycle |
| Loan | Amount, rate, EMI, outstanding balance, status (Active/Completed) |
| LoanGuarantor | Guarantors of a loan |
| LoanInstallment | One row per loan per cycle: principal, interest, penalty, arrears, paid, closing balance |
| LoanPayment | Actual payments received |
| LoanInstallmentRule | EMI bands by loan size |
| AppSetting | Per-society settings |
| SmsLog | Every SMS/WhatsApp sent and its result |
| AuditLog | Audit trail |
| BankAccount, BankTransaction, FinancialTransaction, MemberExitSettlement, ImportLog | Tables present in the schema for ledger / member-exit / import features; **not yet wired into screens** |

---

## 8. Suggested demo video script (≈ 12–15 min)

| # | Scene | What to show | Say |
|---|---|---|---|
| 1 | Landing page (0:45) | Scroll features | "One system for members, loans, collection and reminders — in Marathi and English." |
| 2 | SuperAdmin (1:30) | Create a new society → show one-time temp password | "Onboarding a new society takes a minute. Each society's data is completely separate." |
| 3 | First login (0:45) | Forced password change | "Security first — no shared passwords." |
| 4 | Dashboard (1:00) | Totals, history chart | "The chairman sees the whole picture instantly." |
| 5 | Members (1:00) | Add a member with Marathi name | "Marathi names flow into reports and messages." |
| 6 | Create loan (2:00) | Pick member, 2 guarantors, amount | "The system blocks a second loan, self-guarantee and over-limit loans automatically." |
| 7 | Open cycle & collection sheet (3:00) | Open next cycle; show rows; enter payments; Save | "Interest, EMI and totals are calculated for you — no calculator." |
| 8 | Exports (1:30) | Excel, full PDF, **Collection PDF** | "Same layout as your paper register — print and hand out." |
| 9 | Late payment (1:00) | Unpaid month → penalty & arrears | "The 5% rule is applied fairly, only on the shortfall." |
| 10 | Early payoff (1:00) | Vivek: ₹23,460 → loan Completed | "Closing a loan early is a single entry." |
| 11 | Loan EMI PDF (1:00) | Loan Details → Export PDF | "Like a bank — full EMI schedule and the month the loan will close." |
| 12 | Reminders (1:00) | Reminders page, SMS log; WhatsApp confirmation | "Members get reminders on the 18th–20th automatically." |
| 13 | Wrap-up (0:30) | Privacy, roles | "Your data stays yours." |

**Tips:** use sample data (not real members' names/numbers); keep the worked example from §5 on screen; show the Marathi PDF at full size.

---

## 9. Setup & deployment notes (for the technical team)

**Run locally:** .NET 8 SDK + SQL Server → set `ConnectionStrings:DefaultConnection` → `dotnet run --project SwamiSamarthSociety.Web`. On startup the app creates the three roles and seeds the SuperAdmin from config.

**Required configuration (use User Secrets / environment variables — never commit secrets):**
```
ConnectionStrings:DefaultConnection
SuperAdmin:Email, SuperAdmin:Password
Msg91:AuthKey, Msg91:TemplateId, Msg91:SenderId
WhatsApp:AccessToken, WhatsApp:PhoneNumberId, WhatsApp:TemplateName, WhatsApp:TemplateLanguage
```

**Docker:** `docker build -t vasati24 .` — image listens on port 10000 and installs `libfontconfig1` (needed by PDF generation).

**Password recovery:** `dotnet run -- --reset-password <email> <newPassword>` (forces change at next login).

**Before onboarding a new society, set up its loan rules.** The code does not auto-create EMI bands (`LoanInstallmentRule` rows) when a society is created, so a new society must have its bands inserted (e.g. by SQL) before the first loan can be issued — otherwise loan creation reports "No installment rule is configured". *(Recommended improvement: seed default bands when a society is created.)*

**Fonts:** PDFs use "Nirmala UI" for Marathi (available on Windows). On Linux/Docker, install a Devanagari-capable font (or switch the font) or Marathi text may not render.

---

## 10. Current status & known limits

**Working:** members, loans, collection cycles, penalties/arrears, Excel/PDF exports, loan EMI PDF, early payoff, SMS reminders, per-society isolation, roles, audit log.

**Needs configuration to go live:** SMS (MSG91 keys) and WhatsApp (Meta Business account + approved template). Until set, those messages are not delivered.

**Not yet built (tables exist):** bank ledger, member exit settlement, data import.

**Good to know**
- Loan EMI PDF future rows are projections assuming on-time payment and exclude penalties and shares.
- The flat "share-late" penalty setting exists but is not applied; the loan late-payment penalty (rule b) is.
- Only one active loan per member at a time.

---

## 11. Frequently asked client questions

- **Is our data safe from other societies?** Yes — every query is automatically restricted to your society.
- **Can members log in?** Yes, read-only, with logins created by the admin.
- **Does it work in Marathi?** Names, PDF headers and SMS/WhatsApp messages are Marathi-ready.
- **Can we keep our paper-register format?** Yes — Excel and PDF exports mirror it.
- **Can the rules change (interest %, penalty %, guarantors, max loan)?** Yes, they are per-society settings.
- **What if a member pays a loan off early?** Enter the payoff amount; the loan closes automatically.
