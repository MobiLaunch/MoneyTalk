using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public class InvoiceListRow
{
    public Guid Id { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public DateTime InvoiceDate { get; init; }
    public DateTime DueDate { get; init; }
    public InvoiceStatus Status { get; init; }
    public decimal Total { get; init; }
    public decimal Balance { get; init; }
}

public partial class InvoicesViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public ObservableCollection<InvoiceListRow> Invoices { get; } = new();

    [ObservableProperty] private InvoiceListRow? selectedInvoice;

    public InvoicesViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, INavigationService navigationService)
        : base(unitOfWorkFactory, settingsService)
    {
        _navigationService = navigationService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var invoices = await uow.Invoices.FindAsync(i => i.CompanyId == ActiveCompanyId);
            var customers = (await uow.Customers.FindAsync(c => c.CompanyId == ActiveCompanyId)).ToDictionary(c => c.Id);

            Invoices.Clear();
            foreach (var invoice in invoices.OrderByDescending(i => i.InvoiceDate))
            {
                Invoices.Add(new InvoiceListRow
                {
                    Id = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    CustomerName = customers.TryGetValue(invoice.CustomerId, out var c) ? c.Name : "Unknown",
                    InvoiceDate = invoice.InvoiceDate,
                    DueDate = invoice.DueDate,
                    Status = invoice.Status,
                    Total = invoice.Total,
                    Balance = invoice.Balance
                });
            }
        });
    }

    [RelayCommand]
    private void NewInvoice() => _navigationService.NavigateTo(PageKeys.InvoiceEdit, new InvoiceEditNavigationArgs(null));

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedInvoice == null) return;
        _navigationService.NavigateTo(PageKeys.InvoiceEdit, new InvoiceEditNavigationArgs(SelectedInvoice.Id));
    }
}
