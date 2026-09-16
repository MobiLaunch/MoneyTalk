using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class BillEditViewModel : ViewModelBase
{
    private readonly BillService _billService;
    private readonly Services.INavigationService _navigationService;

    private Guid? _billId;

    public ObservableCollection<Vendor> Vendors { get; } = new();
    public ObservableCollection<Account> ExpenseAccounts { get; } = new();
    public ObservableCollection<BankAccount> PaymentAccounts { get; } = new();
    public ObservableCollection<BillLineEditRow> Lines { get; } = new();

    [ObservableProperty] private Vendor? selectedVendor;
    [ObservableProperty] private string billNumber = string.Empty;
    [ObservableProperty] private DateTimeOffset billDate = DateTimeOffset.Now;
    [ObservableProperty] private DateTimeOffset dueDate = DateTimeOffset.Now.AddDays(30);
    [ObservableProperty] private string? memo;
    [ObservableProperty] private BillStatus status = BillStatus.Open;
    [ObservableProperty] private string totalDisplay = "$0.00";
    [ObservableProperty] private string amountPaidDisplay = "$0.00";
    [ObservableProperty] private string balanceDisplay = "$0.00";

    public bool IsUnposted => _billId == null;
    public bool CanRecordPayment => _billId != null && _currentBalance > 0;

    private decimal _currentBalance;

    public BillEditViewModel(
        Func<IUnitOfWork> unitOfWorkFactory, Services.LocalSettingsService settingsService,
        BillService billService, Services.INavigationService navigationService)
        : base(unitOfWorkFactory, settingsService)
    {
        _billService = billService;
        _navigationService = navigationService;
        Lines.CollectionChanged += (_, __) => RecalculateTotals();
    }

    public async Task LoadAsync(BillEditNavigationArgs args)
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;

            var vendors = await uow.Vendors.FindAsync(v => v.CompanyId == companyId && v.IsActive);
            Vendors.Clear();
            foreach (var vendor in vendors.OrderBy(v => v.Name)) Vendors.Add(vendor);

            var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == companyId &&
                (a.Type == AccountType.Expense || a.Type == AccountType.CostOfGoodsSold) && a.IsActive);
            ExpenseAccounts.Clear();
            foreach (var account in accounts.OrderBy(a => a.Code)) ExpenseAccounts.Add(account);

            var bankAccounts = await uow.BankAccounts.FindAsync(b => b.CompanyId == companyId && b.IsActive);
            PaymentAccounts.Clear();
            foreach (var account in bankAccounts) PaymentAccounts.Add(account);

            if (args.BillId.HasValue)
            {
                var bill = await uow.Bills.GetByIdAsync(args.BillId.Value)
                    ?? throw new InvalidOperationException("This bill no longer exists.");

                _billId = bill.Id;
                BillNumber = bill.BillNumber;
                BillDate = bill.BillDate;
                DueDate = bill.DueDate;
                Memo = bill.Memo;
                Status = bill.Status;
                _currentBalance = bill.Balance;
                SelectedVendor = Vendors.FirstOrDefault(v => v.Id == bill.VendorId);
                AmountPaidDisplay = bill.AmountPaid.ToString("C2");
                TotalDisplay = bill.Total.ToString("C2");
                BalanceDisplay = bill.Balance.ToString("C2");

                Lines.Clear();
                foreach (var line in bill.Lines)
                {
                    var row = new BillLineEditRow
                    {
                        ItemId = line.ItemId,
                        ExpenseAccountId = line.ExpenseAccountId,
                        TaxRateId = line.TaxRateId,
                        Description = line.Description,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice
                    };
                    row.PropertyChanged += (_, __) => RecalculateTotals();
                    Lines.Add(row);
                }
            }
            else
            {
                var existingCount = (await uow.Bills.FindAsync(b => b.CompanyId == companyId)).Count;
                BillNumber = $"BILL-{1001 + existingCount}";
                SelectedVendor = args.VendorId.HasValue ? Vendors.FirstOrDefault(v => v.Id == args.VendorId) : null;
            }

            RecalculateTotals();
            OnPropertyChanged(nameof(IsUnposted));
            OnPropertyChanged(nameof(CanRecordPayment));
        });
    }

    public void AddLine(Account expenseAccount, string description, decimal quantity, decimal unitPrice)
    {
        var row = new BillLineEditRow
        {
            ExpenseAccountId = expenseAccount.Id,
            Description = string.IsNullOrWhiteSpace(description) ? expenseAccount.Name : description,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
        row.PropertyChanged += (_, __) => RecalculateTotals();
        Lines.Add(row);
    }

    [RelayCommand]
    private void RemoveLine(BillLineEditRow? row)
    {
        if (row != null) Lines.Remove(row);
    }

    private void RecalculateTotals()
    {
        if (_billId == null) TotalDisplay = Lines.Sum(l => l.Amount).ToString("C2");
    }

    [RelayCommand]
    private async Task SaveAndPostAsync()
    {
        if (SelectedVendor == null) { ErrorMessage = "Choose a vendor first."; return; }
        if (Lines.Count == 0) { ErrorMessage = "Add at least one line item."; return; }
        if (_billId != null) { ErrorMessage = "This bill has already been posted."; return; }

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var bill = new Bill
            {
                CompanyId = ActiveCompanyId,
                BillNumber = BillNumber,
                VendorId = SelectedVendor!.Id,
                BillDate = BillDate.DateTime,
                DueDate = DueDate.DateTime,
                Memo = Memo
            };
            foreach (var row in Lines)
            {
                bill.Lines.Add(new BillLine
                {
                    BillId = bill.Id,
                    ItemId = row.ItemId,
                    ExpenseAccountId = row.ExpenseAccountId,
                    TaxRateId = row.TaxRateId,
                    Description = row.Description,
                    Quantity = row.Quantity,
                    UnitPrice = row.UnitPrice
                });
            }

            await _billService.SaveDraftAsync(uow, bill);
            await _billService.PostBillAsync(uow, bill.Id);

            var posted = await uow.Bills.GetByIdAsync(bill.Id);
            if (posted != null)
            {
                _billId = posted.Id;
                Status = posted.Status;
                _currentBalance = posted.Balance;
                TotalDisplay = posted.Total.ToString("C2");
                BalanceDisplay = posted.Balance.ToString("C2");
            }
            OnPropertyChanged(nameof(IsUnposted));
            OnPropertyChanged(nameof(CanRecordPayment));
        });
    }

    public async Task<bool> RecordPaymentAsync(decimal amount, DateTime paymentDate, PaymentMethod method, Guid paidFromAccountId)
    {
        if (!_billId.HasValue) return false;

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var payment = new BillPayment
            {
                CompanyId = ActiveCompanyId,
                VendorId = SelectedVendor!.Id,
                PaymentDate = paymentDate,
                Amount = amount,
                Method = method,
                PaidFromAccountId = paidFromAccountId
            };
            payment.Applications.Add(new BillPaymentApplication { BillPaymentId = payment.Id, BillId = _billId!.Value, AmountApplied = amount });

            await _billService.RecordPaymentAsync(uow, payment);

            var updated = await uow.Bills.GetByIdAsync(_billId.Value);
            if (updated != null)
            {
                Status = updated.Status;
                _currentBalance = updated.Balance;
                AmountPaidDisplay = updated.AmountPaid.ToString("C2");
                BalanceDisplay = updated.Balance.ToString("C2");
            }
            OnPropertyChanged(nameof(CanRecordPayment));
            success = true;
        });
        return success;
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}
