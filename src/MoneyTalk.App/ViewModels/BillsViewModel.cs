using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public class BillListRow
{
    public Guid Id { get; init; }
    public string BillNumber { get; init; } = string.Empty;
    public string VendorName { get; init; } = string.Empty;
    public DateTime BillDate { get; init; }
    public DateTime DueDate { get; init; }
    public BillStatus Status { get; init; }
    public decimal Total { get; init; }
    public decimal Balance { get; init; }
}

public partial class BillsViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public ObservableCollection<BillListRow> Bills { get; } = new();

    [ObservableProperty] private BillListRow? selectedBill;

    public BillsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, INavigationService navigationService)
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
            var bills = await uow.Bills.FindAsync(b => b.CompanyId == ActiveCompanyId);
            var vendors = (await uow.Vendors.FindAsync(v => v.CompanyId == ActiveCompanyId)).ToDictionary(v => v.Id);

            Bills.Clear();
            foreach (var bill in bills.OrderByDescending(b => b.BillDate))
            {
                Bills.Add(new BillListRow
                {
                    Id = bill.Id,
                    BillNumber = bill.BillNumber,
                    VendorName = vendors.TryGetValue(bill.VendorId, out var v) ? v.Name : "Unknown",
                    BillDate = bill.BillDate,
                    DueDate = bill.DueDate,
                    Status = bill.Status,
                    Total = bill.Total,
                    Balance = bill.Balance
                });
            }
        });
    }

    [RelayCommand]
    private void NewBill() => _navigationService.NavigateTo(PageKeys.BillEdit, new BillEditNavigationArgs(null));

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedBill == null) return;
        _navigationService.NavigateTo(PageKeys.BillEdit, new BillEditNavigationArgs(SelectedBill.Id));
    }
}
