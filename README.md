# MoneyTalk — Financial & Business Management

A native Windows 11 (WinUI 3) accounting and business-management application: a full
double-entry ledger, invoicing/billing, bank reconciliation, budgeting, financial reporting,
and an AI financial advisor powered by Google Gemini — with Square and QuickBooks Online
integrations for pulling in real transactions.

MoneyTalk is designed for a small/medium business that wants QuickBooks-style bookkeeping and
a live "how's my business doing" dashboard in one desktop app, without shipping its data to a
SaaS backend: everything is stored locally in SQLite, and every external credential (Square,
QuickBooks, Gemini) is encrypted at rest on the machine it runs on.

## Feature list

**Core accounting (double-entry, always-balanced)**
- Full chart of accounts (Asset/Liability/Equity/Income/COGS/Expense, with sub-types)
- General ledger with manual journal entries, posting, and void-as-reversal (never deletes history)
- Accounts Receivable: customers, invoices with line items and tax, payments with multi-invoice application, AR aging
- Accounts Payable: vendors, bills, payments with multi-bill application, 1099 tracking, AP aging
- Products & services catalog (inventory, non-inventory, service, bundle types)
- Bank/credit-card accounts, CSV statement import, and a full cash-reconciliation workflow (start → clear/auto-match → complete)
- Budgets with budget-vs-actual by account and month
- Recurring transactions (recurring journal entries out of the box; recurring invoices/bills via the same engine)
- Financial statements: Profit & Loss, Balance Sheet, Trial Balance, Cash Flow Statement (direct method), AR/AP aging
- 90-day cash-flow forecasting from open invoices, open bills, and recurring transactions
- Dashboard with live KPIs, a cash-trend chart, and rule-based financial insights

**AI financial advisor (Gemini)**
- Chat interface grounded in a live snapshot of your books (cash, AR/AP, aging, P&L, forecast) — never a black box, and never sends more than that summary to Google
- Suggested prompts for common questions ("how's my cash flow", "who owes me money", "where can I cut costs")

**Integrations**
- **Square**: OAuth connect, pulls payments from the last 30 days into bank transactions ready for reconciliation
- **QuickBooks Online**: OAuth connect, chart-of-accounts/customer/invoice import, and invoice push-back
- **Gemini**: bring your own API key from [aistudio.google.com](https://aistudio.google.com/apikey)

**Everything else a small business needs**
- Multi-user (local) with roles (Owner/Admin/Accountant/Bookkeeper/Read-only)
- Audit-log-ready data model (every posting is traceable back to its source document)
- Attachments model for receipts/statements
- Local, encrypted secrets — no cloud account required to run the app

## Architecture

```
MoneyTalk.sln
├── src/
│   ├── MoneyTalk.Core                    Domain entities, the accounting engine, DTOs — no UI, no EF, no platform deps
│   ├── MoneyTalk.Data                    EF Core + SQLite persistence, repositories, chart-of-accounts seeder
│   ├── MoneyTalk.Integrations.Square     Square Connect API client (OAuth, payments, payouts)
│   ├── MoneyTalk.Integrations.QuickBooks QuickBooks Online Accounting API client (OAuth, query, invoice push)
│   ├── MoneyTalk.Integrations.Gemini     Google Gemini generateContent API client
│   └── MoneyTalk.App                     WinUI 3 app: MVVM (CommunityToolkit.Mvvm), NavigationView shell, DI via Microsoft.Extensions.Hosting
└── tests/
    └── MoneyTalk.Tests                   xUnit tests for the accounting engine, run against a real per-test SQLite database
```

**Why this split:** `MoneyTalk.Core` has zero dependency on EF Core, WinUI, or any specific
integration SDK — it's plain C#/.NET 8, so the double-entry engine (`LedgerService`,
`InvoiceService`, `BillService`, `ReconciliationService`, `ReportingService`, ...) is testable in
isolation and could power a different front end (web, mobile) later without being rewritten.
Every dollar that moves through the app goes through `LedgerService.PostJournalEntryAsync`,
which is the one place "debits must equal credits" is enforced — every other service builds a
balanced set of lines and hands them off rather than touching account balances directly.

**Data storage:** a single SQLite database at `%LOCALAPPDATA%\MoneyTalk\moneytalk.db`. OAuth
tokens and the Gemini API key are never stored in that database — they're encrypted with the
Windows Data Protection API (DPAPI, scoped to the current Windows user) in
`%LOCALAPPDATA%\MoneyTalk\secrets.dat`, so a copy of the .db file alone can't leak live
credentials.

**Why unpackaged (no MSIX):** the app builds and runs as a plain Win32 desktop app
(`WindowsPackageType=None`) — no signing certificate or Store identity needed to build and run
it locally. This is why the secure-token store uses DPAPI rather than the Credential Locker
(`PasswordVault`), which historically assumes a packaged app identity.

## Getting started

### Prerequisites (Windows 11)
- Visual Studio 2022 (17.9+) with the **.NET Desktop Development** and **Windows App SDK C#**
  workloads, or the .NET 8 SDK + [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads) tooling from the CLI
- Windows 10 SDK 10.0.19041.0 or later (installed with the workload above)

### Build & run
```powershell
git clone <this repo>
cd MoneyTalk
dotnet restore MoneyTalk.sln
dotnet build MoneyTalk.sln -c Debug
# Run the app (or just F5 it from Visual Studio with MoneyTalk.App as the startup project):
dotnet run --project src\MoneyTalk.App\MoneyTalk.App.csproj
```

On first launch, MoneyTalk walks you through creating a company, which seeds a standard chart
of accounts, a default bank account, and a default (0%) tax rate — all editable afterward.

### Run the tests
```powershell
dotnet test tests\MoneyTalk.Tests\MoneyTalk.Tests.csproj
```
The test project only needs the plain .NET 8 SDK (no Windows App SDK), so it also runs on
Linux/macOS CI if you want a build check that doesn't require a Windows agent.

> **Note on this repository's origin:** this solution was authored in a Linux sandbox without
> access to the .NET SDK or Windows App SDK tooling, so the WinUI 3 project (`MoneyTalk.App`)
> has not been compiled here — only carefully hand-verified, then patched against real compiler
> output from a Windows build once one became available. `MoneyTalk.Core`, `MoneyTalk.Data`,
> and the integration projects target plain `net8.0` and should build/test cleanly on any
> platform. If you hit anything else, `NumberBox.Value` (a `double`) bound directly to `decimal`
> view-model properties is the most likely remaining rough edge.

#### If `dotnet build` fails with MSB4062 ("ExpandPriContent task could not be loaded")

This is a known Windows App SDK quirk specific to building via the `dotnet` CLI rather than
full Visual Studio (tracked in [microsoft/WindowsAppSDK#3939](https://github.com/microsoft/WindowsAppSDK/issues/3939)):
the PRI (resource index) generation tooling only resolves correctly when
`<EnableMsixTooling>true</EnableMsixTooling>` is set in `MoneyTalk.App.csproj` — already the
case in this repo. This does **not** force MSIX packaging; that's controlled separately by
`<WindowsPackageType>None</WindowsPackageType>`, which stays `None`. If you still hit this
error, confirm you have the .NET 8 SDK and the Windows App SDK C# workload/component installed
(via the Visual Studio Installer, even if you build from the CLI) rather than a bare `dotnet-sdk`
install.

### Database schema changes
The app calls `Database.EnsureCreated()` on first run rather than shipping EF Core migrations
(since no `dotnet ef` tooling was available while authoring this repo). Once you're building on
Windows, switch to migrations before changing the schema:
```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate -p src\MoneyTalk.Data -s src\MoneyTalk.Data
```
`MoneyTalkDbContextFactory` is already in place for the design-time tooling to find the context.

## Configuring the integrations

None of these are required to use the core accounting features — the app is fully useful with
zero integrations connected.

### Gemini (AI Advisor)
1. Get a free API key at https://aistudio.google.com/apikey
2. Integrations page → paste it under "Gemini AI Advisor" → Save Key

### Square
1. Create an app at https://developer.squareup.com/apps and note its Client ID/Secret
2. Integrations page → paste the Client ID/Secret under "Square" → Save App Credentials (the
   secret goes straight into the DPAPI-encrypted store, never to `settings.json`)
3. "Open Square Authorization" → approve in the browser → copy the `code` query-string value
   from the redirect URL → paste it back → Connect
4. Pick a deposit bank account and hit "Sync Last 30 Days of Payments" to pull Square sales into
   bank transactions ready for reconciliation

### QuickBooks Online
Same shape as Square: register an app at https://developer.intuit.com, paste its Client
ID/Secret and save, then connect via the paste-the-authorization-code flow (QuickBooks also
requires the `realmId` from the redirect URL). `IQuickBooksClient` (already wired into DI) can
then import accounts/customers/invoices or push MoneyTalk invoices out.

Both OAuth flows use "open in system browser, paste the code back" rather than an embedded web
view or a registered custom URI scheme — it's a couple of extra clicks, but it needs no
packaging identity, no Windows registry changes, and no background HTTP listener running on the
desktop.

## Security notes
- No telemetry, no cloud sync, no backend server — this is a local-first app
- Square/QuickBooks OAuth tokens and the Gemini API key live in a DPAPI-encrypted file scoped to
  the current Windows user account; they are never written to the SQLite database or to
  `settings.json`
- The Gemini financial-advisor prompt includes only a numeric summary of your books (cash
  position, AR/AP totals and aging, P&L, forecast) — never raw customer/vendor lists, account
  numbers, or integration credentials
- Every posted transaction is immutable; corrections are made by voiding (which posts an
  equal-and-opposite reversing entry) rather than editing or deleting history

## What's intentionally out of scope (for now)
- Payroll processing (payroll expense/liability accounts exist in the seeded chart of accounts
  so you can book payroll journal entries from an external payroll provider, but there's no
  payroll engine)
- Multi-currency conversion (the data model has a `BaseCurrency` field per company, but there's
  no FX rate engine)
- Multi-company switching in the UI (the data model is already company-scoped throughout, so
  this is a UI-only addition, not a schema change)
