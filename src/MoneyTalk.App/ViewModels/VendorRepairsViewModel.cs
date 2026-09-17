using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public class VendorRepairRow
{
    public Guid Id { get; init; }
    public string Device { get; init; } = string.Empty;
    public string Issue { get; init; } = string.Empty;
    public string VendorName { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? SentDate { get; init; }
    public DateTime? EstimatedReturnDate { get; init; }
    public bool IsOverdue { get; init; }
}

public partial class VendorRepairsViewModel : ViewModelBase
{
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<VendorRepairRow> VendorRepairs { get; } = new();

    [ObservableProperty] private VendorRepairRow? selectedVendorRepair;

    public VendorRepairsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService)
        : base(unitOfWorkFactory, settingsService)
    {
    }

    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;

            var customers = await uow.Customers.FindAsync(c => c.CompanyId == companyId && c.IsActive);
            Customers.Clear();
            foreach (var customer in customers.OrderBy(c => c.Name)) Customers.Add(customer);
            var customersById = customers.ToDictionary(c => c.Id);

            var repairs = await uow.VendorRepairs.FindAsync(v => v.CompanyId == companyId);
            var now = DateTime.UtcNow;

            VendorRepairs.Clear();
            foreach (var repair in repairs.OrderByDescending(r => r.CreatedAtUtc))
            {
                VendorRepairs.Add(new VendorRepairRow
                {
                    Id = repair.Id,
                    Device = repair.Device ?? string.Empty,
                    Issue = repair.Issue ?? string.Empty,
                    VendorName = repair.VendorName ?? string.Empty,
                    CustomerName = repair.CustomerId.HasValue && customersById.TryGetValue(repair.CustomerId.Value, out var c) ? c.Name : string.Empty,
                    TrackingNumber = repair.TrackingNumber ?? string.Empty,
                    Status = repair.Status,
                    SentDate = repair.SentDate,
                    EstimatedReturnDate = repair.EstimatedReturnDate,
                    IsOverdue = repair.IsOverdue(now)
                });
            }
        });
    }

    public async Task<bool> AddVendorRepairAsync(
        string device, string issue, string vendorName, Guid? customerId, DateTime? sentDate, DateTime? estimatedReturnDate, string? notes)
    {
        if (string.IsNullOrWhiteSpace(device)) { ErrorMessage = "Enter the device first."; return false; }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            await uow.VendorRepairs.AddAsync(new VendorRepair
            {
                CompanyId = ActiveCompanyId,
                Device = device.Trim(),
                Issue = issue,
                VendorName = vendorName,
                CustomerId = customerId,
                SentDate = sentDate,
                EstimatedReturnDate = estimatedReturnDate,
                Notes = notes
            });
            await uow.SaveChangesAsync();
            success = true;
        });

        if (success) await LoadAsync();
        return success;
    }

    public async Task<string?> GetVendorRepairNotesAsync(Guid id)
    {
        using var uow = NewUnitOfWork();
        var repair = await uow.VendorRepairs.GetByIdAsync(id);
        return repair?.Notes;
    }

    public async Task<bool> EditVendorRepairAsync(
        Guid id, string device, string issue, string vendorName, string trackingNumber, string status,
        DateTime? sentDate, DateTime? estimatedReturnDate, string? notes)
    {
        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var repair = await uow.VendorRepairs.GetByIdAsync(id) ?? throw new InvalidOperationException("This vendor repair no longer exists.");
            repair.Device = device.Trim();
            repair.Issue = issue;
            repair.VendorName = vendorName;
            repair.TrackingNumber = trackingNumber;
            repair.Status = status;
            repair.SentDate = sentDate;
            repair.EstimatedReturnDate = estimatedReturnDate;
            repair.Notes = notes;
            uow.VendorRepairs.Update(repair);
            await uow.SaveChangesAsync();
            success = true;
        });

        if (success) await LoadAsync();
        return success;
    }
}
