using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Core.Accounting;

/// <summary>Vendor-facing (accounts-payable) side of the ledger: drafting bills, posting them,
/// and recording payments made to vendors.</summary>
public class BillService
{
    private readonly LedgerService _ledger;

    public BillService(LedgerService ledger)
    {
        _ledger = ledger;
    }

    public static void RecalculateTotals(Bill bill, IReadOnlyDictionary<Guid, TaxRate>? taxRatesById = null)
    {
        bill.Subtotal = bill.Lines.Sum(l => l.Amount);
        decimal taxTotal = 0m;
        if (taxRatesById != null)
        {
            foreach (var line in bill.Lines)
            {
                if (line.TaxRateId.HasValue && taxRatesById.TryGetValue(line.TaxRateId.Value, out var rate))
                    taxTotal += Math.Round(line.Amount * rate.RatePercent / 100m, 2);
            }
        }
        bill.TaxTotal = taxTotal;
        bill.Total = bill.Subtotal + bill.TaxTotal;
        bill.Balance = bill.Total - bill.AmountPaid;
    }

    public async Task<Bill> SaveDraftAsync(IUnitOfWork uow, Bill bill, CancellationToken ct = default)
    {
        RecalculateTotals(bill);
        var existing = await uow.Bills.GetByIdAsync(bill.Id, ct);
        if (existing == null)
            await uow.Bills.AddAsync(bill, ct);
        else
            uow.Bills.Update(bill);

        await uow.SaveChangesAsync(ct);
        return bill;
    }

    /// <summary>Posts a bill to the general ledger: Debit each line's expense/COGS account,
    /// Credit Accounts Payable for the total.</summary>
    public async Task PostBillAsync(IUnitOfWork uow, Guid billId, CancellationToken ct = default)
    {
        var bill = await uow.Bills.GetByIdAsync(billId, ct)
            ?? throw new InvalidOperationException($"Bill {billId} was not found.");
        if (bill.Status != BillStatus.Open || bill.JournalEntryId != null)
            throw new InvalidOperationException("This bill has already been posted.");
        if (bill.Lines.Count == 0)
            throw new InvalidOperationException("Cannot post a bill with no line items.");

        var company = await uow.Companies.GetByIdAsync(bill.CompanyId, ct)
            ?? throw new InvalidOperationException("Company profile not found.");
        var apAccountId = company.DefaultApAccountId
            ?? throw new InvalidOperationException("No default Accounts Payable account is configured for this company.");

        var lines = new List<JournalLineInput>();
        foreach (var group in bill.Lines.GroupBy(l => l.ExpenseAccountId))
            lines.Add(new JournalLineInput(group.Key, group.Sum(l => l.Amount), 0m, $"Bill {bill.BillNumber}", null, bill.VendorId));

        lines.Add(new JournalLineInput(apAccountId, 0m, bill.Total, $"Bill {bill.BillNumber}", null, bill.VendorId));

        var journalEntry = await _ledger.PostJournalEntryAsync(
            uow, bill.CompanyId, bill.BillDate, $"Bill {bill.BillNumber}",
            Entities.JournalSourceType.Bill, bill.Id, lines, ct: ct);

        bill.JournalEntryId = journalEntry.Id;
        bill.ModifiedAtUtc = DateTime.UtcNow;
        uow.Bills.Update(bill);

        var vendor = await uow.Vendors.GetByIdAsync(bill.VendorId, ct);
        if (vendor != null)
        {
            vendor.Balance += bill.Total;
            uow.Vendors.Update(vendor);
        }

        await uow.SaveChangesAsync(ct);
    }

    /// <summary>Records a payment made to a vendor, applies it across the requested bills, and
    /// posts Debit Accounts Payable / Credit [the paying bank account].</summary>
    public async Task<BillPayment> RecordPaymentAsync(IUnitOfWork uow, BillPayment payment, CancellationToken ct = default)
    {
        if (payment.Applications.Sum(a => a.AmountApplied) > payment.Amount)
            throw new InvalidOperationException("Cannot apply more than the total payment amount.");

        var company = await uow.Companies.GetByIdAsync(payment.CompanyId, ct)
            ?? throw new InvalidOperationException("Company profile not found.");
        var apAccountId = company.DefaultApAccountId
            ?? throw new InvalidOperationException("No default Accounts Payable account is configured for this company.");

        var lines = new List<JournalLineInput>
        {
            new(apAccountId, payment.Amount, 0m, "Vendor payment", null, payment.VendorId),
            new(payment.PaidFromAccountId, 0m, payment.Amount, "Vendor payment", null, payment.VendorId)
        };

        var journalEntry = await _ledger.PostJournalEntryAsync(
            uow, payment.CompanyId, payment.PaymentDate, "Vendor payment sent",
            Entities.JournalSourceType.VendorPayment, payment.Id, lines, ct: ct);
        payment.JournalEntryId = journalEntry.Id;

        await uow.BillPayments.AddAsync(payment, ct);

        foreach (var application in payment.Applications)
        {
            var bill = await uow.Bills.GetByIdAsync(application.BillId, ct)
                ?? throw new InvalidOperationException($"Bill {application.BillId} was not found.");
            bill.AmountPaid += application.AmountApplied;
            bill.Balance = bill.Total - bill.AmountPaid;
            bill.Status = bill.Balance <= 0 ? BillStatus.Paid : BillStatus.PartiallyPaid;
            bill.ModifiedAtUtc = DateTime.UtcNow;
            uow.Bills.Update(bill);
        }

        var vendor = await uow.Vendors.GetByIdAsync(payment.VendorId, ct);
        if (vendor != null)
        {
            vendor.Balance -= payment.Applications.Sum(a => a.AmountApplied);
            if (payment.PaymentDate.Year == DateTime.UtcNow.Year)
                vendor.YtdPayments += payment.Applications.Sum(a => a.AmountApplied);
            uow.Vendors.Update(vendor);
        }

        await uow.SaveChangesAsync(ct);
        return payment;
    }

    /// <summary>Voids a posted bill that has no payments applied yet: reverses its journal entry
    /// (equal-and-opposite, never deletes history) and rolls back the vendor's balance. Bills
    /// with any payment already applied must have that payment reversed/unapplied first.</summary>
    public async Task VoidBillAsync(IUnitOfWork uow, Guid billId, string? reason = null, CancellationToken ct = default)
    {
        var bill = await uow.Bills.GetByIdAsync(billId, ct)
            ?? throw new InvalidOperationException($"Bill {billId} was not found.");
        if (bill.Status == BillStatus.Void)
            throw new InvalidOperationException("This bill is already void.");
        if (bill.JournalEntryId == null)
            throw new InvalidOperationException("This bill has not been posted.");
        if (bill.AmountPaid > 0)
            throw new InvalidOperationException("Cannot void a bill with payments applied. Unapply the payment first.");

        await _ledger.VoidJournalEntryAsync(uow, bill.JournalEntryId.Value, reason ?? $"Void bill {bill.BillNumber}", ct: ct);

        var vendor = await uow.Vendors.GetByIdAsync(bill.VendorId, ct);
        if (vendor != null)
        {
            vendor.Balance -= bill.Balance;
            uow.Vendors.Update(vendor);
        }

        bill.Status = BillStatus.Void;
        bill.Balance = 0;
        bill.ModifiedAtUtc = DateTime.UtcNow;
        uow.Bills.Update(bill);

        await uow.SaveChangesAsync(ct);
    }
}
