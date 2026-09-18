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

    public async Task<bool> AddCustomerAsync(
        string name, string email, string phone, int paymentTermsDays, bool taxExempt,
        string? driversLicense = null, string? tags = null)
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
                TaxExempt = taxExempt,
                DriversLicense = string.IsNullOrWhiteSpace(driversLicense) ? null : driversLicense.Trim(),
                Tags = string.IsNullOrWhiteSpace(tags) ? null : tags.Trim()
            };
            await uow.Customers.AddAsync(customer);
            await uow.SaveChangesAsync();
            Customers.Add(customer);
            success = true;
        });
        return success;
    }

    /// <summary>Summary rows for the read-only "View Tickets" dialog on the Customers page.</summary>
    public async Task<List<TicketListRow>> GetCustomerTicketsAsync(Guid customerId)
    {
        using var uow = NewUnitOfWork();
        var tickets = await uow.RepairTickets.FindAsync(t => t.CustomerId == customerId);
        return tickets.OrderByDescending(t => t.CreatedAtUtc).Select(t => new TicketListRow
        {
            Id = t.Id,
            TicketNumber = t.TicketNumber,
            Device = string.IsNullOrWhiteSpace(t.DeviceModel) ? t.Device : $"{t.Device} {t.DeviceModel}",
            Issue = t.Issue,
            Status = t.Status,
            Priority = t.Priority,
            CreatedAtUtc = t.CreatedAtUtc,
            Balance = t.Balance
        }).ToList();
    }

    [RelayCommand]
    private void NewInvoiceForSelected()
    {
        if (SelectedCustomer == null) return;
        _navigationService.NavigateTo(PageKeys.InvoiceEdit, new InvoiceEditNavigationArgs(null, SelectedCustomer.Id));
    }

    public async Task<bool> EditCustomerAsync(
        Guid customerId, string name, string email, string phone, int paymentTermsDays, bool taxExempt, bool isActive,
        string? driversLicense = null, string? tags = null)
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
            var customer = await uow.Customers.GetByIdAsync(customerId)
                ?? throw new InvalidOperationException("Customer no longer exists.");
            customer.Name = name.Trim();
            customer.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            customer.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            customer.PaymentTermsDays = paymentTermsDays;
            customer.TaxExempt = taxExempt;
            customer.IsActive = isActive;
            customer.DriversLicense = string.IsNullOrWhiteSpace(driversLicense) ? null : driversLicense.Trim();
            customer.Tags = string.IsNullOrWhiteSpace(tags) ? null : tags.Trim();
            uow.Customers.Update(customer);
            await uow.SaveChangesAsync();
            success = true;
        });

        if (success) await LoadAsync();
        return success;
    }

    /// <summary>Imports customers from parsed CSV rows (see <see cref="CsvImportService"/>).
    /// Matches an existing customer by email (case-insensitive) and updates it in place; otherwise
    /// creates a new one. A row with no name is skipped rather than failing the whole import.</summary>
    public async Task<(int Imported, int Updated, int Skipped)> ImportCustomersAsync(List<Dictionary<string, string>> rows)
    {
        var imported = 0;
        var updated = 0;
        var skipped = 0;

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;
            var existingByEmail = (await uow.Customers.FindAsync(c => c.CompanyId == companyId))
                .Where(c => !string.IsNullOrWhiteSpace(c.Email))
                .ToDictionary(c => c.Email!.Trim(), c => c, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var name = CsvImportService.Get(row, "Name", "Customer Name", "Customer");
                if (string.IsNullOrWhiteSpace(name)) { skipped++; continue; }

                var email = CsvImportService.Get(row, "Email");
                var phone = CsvImportService.Get(row, "Phone");
                var tags = CsvImportService.Get(row, "Tags");
                var termsText = CsvImportService.Get(row, "Payment Terms (days)", "Payment Terms", "Terms");
                var terms = int.TryParse(termsText, out var parsedTerms) ? parsedTerms : 30;

                if (email != null && existingByEmail.TryGetValue(email, out var existing))
                {
                    existing.Name = name;
                    existing.Phone = phone;
                    existing.Tags = tags;
                    existing.PaymentTermsDays = terms;
                    uow.Customers.Update(existing);
                    updated++;
                }
                else
                {
                    var customer = new Customer
                    {
                        CompanyId = companyId,
                        Name = name,
                        Email = email,
                        Phone = phone,
                        Tags = tags,
                        PaymentTermsDays = terms
                    };
                    await uow.Customers.AddAsync(customer);
                    imported++;
                }
            }

            await uow.SaveChangesAsync();
        });

        await LoadAsync();
        return (imported, updated, skipped);
    }
}
