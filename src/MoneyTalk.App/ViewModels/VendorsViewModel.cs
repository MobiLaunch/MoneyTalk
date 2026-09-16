using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class VendorsViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public ObservableCollection<Vendor> Vendors { get; } = new();

    [ObservableProperty] private Vendor? selectedVendor;

    public VendorsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, INavigationService navigationService)
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
            var vendors = await uow.Vendors.FindAsync(v => v.CompanyId == ActiveCompanyId);
            Vendors.Clear();
            foreach (var vendor in vendors.OrderBy(v => v.Name))
                Vendors.Add(vendor);
        });
    }

    public async Task<bool> AddVendorAsync(string name, string email, string phone, int paymentTermsDays, bool is1099)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Vendor name is required.";
            return false;
        }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var vendor = new Vendor
            {
                CompanyId = ActiveCompanyId,
                Name = name.Trim(),
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                PaymentTermsDays = paymentTermsDays,
                Is1099Vendor = is1099
            };
            await uow.Vendors.AddAsync(vendor);
            await uow.SaveChangesAsync();
            Vendors.Add(vendor);
            success = true;
        });
        return success;
    }

    [RelayCommand]
    private void NewBillForSelected()
    {
        if (SelectedVendor == null) return;
        _navigationService.NavigateTo(PageKeys.BillEdit, new BillEditNavigationArgs(null, SelectedVendor.Id));
    }
}
