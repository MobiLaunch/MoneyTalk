using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class InvoiceEditViewModel : ViewModelBase
{
    private readonly InvoiceService _invoiceService;
    private readonly Services.INavigationService _navigationService;

    private Guid? _invoiceId;

    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<Item> Items { get; } = new();
    public ObservableCollection<BankAccount> DepositAccounts { get; } = new();
    public ObservableCollection<InvoiceLineEditRow> Lines { get; } = new();

    [ObservableProperty] private Customer? selectedCustomer;
    [ObservableProperty] private string invoiceNumber = string.Empty;
    [ObservableProperty] private DateTimeOffset invoiceDate = DateTimeOffset.Now;
    [ObservableProperty] private DateTimeOffset dueDate = DateTimeOffset.Now.AddDays(30);
    [ObservableProperty] private string? memo;
    [ObservableProperty] private InvoiceStatus status = InvoiceStatus.Draft;
    [ObservableProperty] private string subtotalDisplay = "$0.00";
    [ObservableProperty] private string totalDisplay = "$0.00";
    [ObservableProperty] private string amountPaidDisplay = "$0.00";
    [ObservableProperty] private string balanceDisplay = "$0.00";

    public bool IsDraft => Status == InvoiceStatus.Draft;
    public bool CanRecordPayment => !IsDraft && _currentBalance > 0;

    private decimal _currentBalance;

    public InvoiceEditViewModel(
        Func<IUnitOfWork> unitOfWorkFactory, Services.LocalSettingsService settingsService,
        InvoiceService invoiceService, Services.INavigationService navigationService)
        : base(unitOfWorkFactory, settingsService)
    {
        _invoiceService = invoiceService;
        _navigationService = navigationService;
        Lines.CollectionChanged += (_, __) => RecalculateTotals();
    }

    partial void OnStatusChanged(InvoiceStatus value)
    {
        OnPropertyChanged(nameof(IsDraft));
        OnPropertyChanged(nameof(CanRecordPayment));
    }

    public async Task LoadAsync(InvoiceEditNavigationArgs args)
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;

            var customers = await uow.Customers.FindAsync(c => c.CompanyId == companyId && c.IsActive);
            Customers.Clear();
            foreach (var customer in customers.OrderBy(c => c.Name)) Customers.Add(customer);

            var items = await uow.Items.FindAsync(i => i.CompanyId == companyId && i.IsActive);
            Items.Clear();
            foreach (var item in items.OrderBy(i => i.Name)) Items.Add(item);

            var bankAccounts = await uow.BankAccounts.FindAsync(b => b.CompanyId == companyId && b.IsActive);
            DepositAccounts.Clear();
            foreach (var account in bankAccounts) DepositAccounts.Add(account);

            if (args.InvoiceId.HasValue)
            {
                var invoice = await uow.Invoices.GetByIdAsync(args.InvoiceId.Value)
                    ?? throw new InvalidOperationException("This invoice no longer exists.");

                _invoiceId = invoice.Id;
                InvoiceNumber = invoice.InvoiceNumber;
                InvoiceDate = invoice.InvoiceDate;
                DueDate = invoice.DueDate;
                Memo = invoice.Memo;
                Status = invoice.Status;
                _currentBalance = invoice.Balance;
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == invoice.CustomerId);
                AmountPaidDisplay = invoice.AmountPaid.ToString("C2");

                Lines.Clear();
                foreach (var line in invoice.Lines)
                {
                    var row = new InvoiceLineEditRow
                    {
                        ItemId = line.ItemId,
                        IncomeAccountId = line.IncomeAccountId,
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
                var existingCount = (await uow.Invoices.FindAsync(i => i.CompanyId == companyId)).Count;
                InvoiceNumber = $"INV-{1001 + existingCount}";
                SelectedCustomer = args.CustomerId.HasValue ? Customers.FirstOrDefault(c => c.Id == args.CustomerId) : null;
            }

            RecalculateTotals();
        });
    }

    public void AddLine(Item? item, string description, decimal quantity, decimal unitPrice)
    {
        var row = new InvoiceLineEditRow
        {
            ItemId = item?.Id,
            IncomeAccountId = item?.IncomeAccountId,
            Description = string.IsNullOrWhiteSpace(description) ? item?.Name ?? "Item" : description,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
        row.PropertyChanged += (_, __) => RecalculateTotals();
        Lines.Add(row);
    }

    [RelayCommand]
    private void RemoveLine(InvoiceLineEditRow? row)
    {
        if (row != null) Lines.Remove(row);
    }

    private void RecalculateTotals()
    {
        var subtotal = Lines.Sum(l => l.Amount);
        SubtotalDisplay = subtotal.ToString("C2");
        TotalDisplay = subtotal.ToString("C2"); // Tax is computed against configured tax rates at post time.
        if (IsDraft) BalanceDisplay = subtotal.ToString("C2");
    }

    [RelayCommand]
    private async Task SaveDraftAsync()
    {
        if (SelectedCustomer == null) { ErrorMessage = "Choose a customer first."; return; }
        if (Lines.Count == 0) { ErrorMessage = "Add at least one line item."; return; }
        if (Lines.Any(l => l.IncomeAccountId == null))
        {
            ErrorMessage = "Every line needs an item that has an income account assigned (set this on the Products & Services page).";
            return;
        }

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var invoice = _invoiceId.HasValue
                ? await uow.Invoices.GetByIdAsync(_invoiceId.Value) ?? throw new InvalidOperationException("Invoice no longer exists.")
                : new Invoice { CompanyId = ActiveCompanyId };

            invoice.InvoiceNumber = InvoiceNumber;
            invoice.CustomerId = SelectedCustomer!.Id;
            invoice.InvoiceDate = InvoiceDate.DateTime;
            invoice.DueDate = DueDate.DateTime;
            invoice.Memo = Memo;

            invoice.Lines.Clear();
            foreach (var row in Lines)
            {
                invoice.Lines.Add(new InvoiceLine
                {
                    InvoiceId = invoice.Id,
                    ItemId = row.ItemId,
                    IncomeAccountId = row.IncomeAccountId,
                    TaxRateId = row.TaxRateId,
                    Description = row.Description,
                    Quantity = row.Quantity,
                    UnitPrice = row.UnitPrice
                });
            }

            await _invoiceService.SaveDraftAsync(uow, invoice);
            _invoiceId = invoice.Id;
        });
    }

    [RelayCommand]
    private async Task PostAsync()
    {
        await SaveDraftAsync();
        if (!_invoiceId.HasValue || ErrorMessage != null) return;

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            await _invoiceService.PostInvoiceAsync(uow, _invoiceId!.Value);

            var posted = await uow.Invoices.GetByIdAsync(_invoiceId.Value);
            if (posted != null)
            {
                Status = posted.Status;
                _currentBalance = posted.Balance;
                TotalDisplay = posted.Total.ToString("C2");
                BalanceDisplay = posted.Balance.ToString("C2");
            }
        });
    }

    public async Task<bool> RecordPaymentAsync(decimal amount, DateTime paymentDate, PaymentMethod method, Guid depositToAccountId)
    {
        if (!_invoiceId.HasValue) return false;

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var payment = new Payment
            {
                CompanyId = ActiveCompanyId,
                CustomerId = SelectedCustomer!.Id,
                PaymentDate = paymentDate,
                Amount = amount,
                Method = method,
                DepositToAccountId = depositToAccountId
            };
            payment.Applications.Add(new PaymentApplication { PaymentId = payment.Id, InvoiceId = _invoiceId!.Value, AmountApplied = amount });

            await _invoiceService.RecordPaymentAsync(uow, payment);

            var updated = await uow.Invoices.GetByIdAsync(_invoiceId.Value);
            if (updated != null)
            {
                Status = updated.Status;
                _currentBalance = updated.Balance;
                AmountPaidDisplay = updated.AmountPaid.ToString("C2");
                BalanceDisplay = updated.Balance.ToString("C2");
            }
            success = true;
        });
        return success;
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}
