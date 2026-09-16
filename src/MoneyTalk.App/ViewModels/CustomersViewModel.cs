using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class CustomersViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public ObservableCollection<Customer> Customers { get; } = new();

    [ObservableProperty] private Customer? selectedCustomer;

    public CustomersViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, INavigationService navigationService)
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
            var customers = await uow.Customers.FindAsync(c => c.CompanyId == ActiveCompanyId);
            Customers.Clear();
            foreach (var customer in customers.OrderBy(c => c.Name))
                Customers.Add(customer);
        });
    }

    public async Task<bool> AddCustomerAsync(string name, string email, string phone, int paymentTermsDays, bool taxExempt)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Customer name is required.";
            return false;
        }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var customer = new Customer
            {
                CompanyId = ActiveCompanyId,
                Name = name.Trim(),
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                PaymentTermsDays = paymentTermsDays,
                TaxExempt = taxExempt
            };
            await uow.Customers.AddAsync(customer);
            await uow.SaveChangesAsync();
            Customers.Add(customer);
            success = true;
        });
        return success;
    }

    [RelayCommand]
    private void NewInvoiceForSelected()
    {
        if (SelectedCustomer == null) return;
        _navigationService.NavigateTo(PageKeys.InvoiceEdit, new InvoiceEditNavigationArgs(null, SelectedCustomer.Id));
    }
}
