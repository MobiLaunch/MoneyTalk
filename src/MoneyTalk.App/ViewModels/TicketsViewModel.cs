using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public class TicketListRow
{
    public Guid Id { get; init; }
    public string TicketNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string Device { get; init; } = string.Empty;
    public string Issue { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public TicketPriority Priority { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public decimal Balance { get; init; }

    /// <summary>Days since intake — drives the amber (3+) / red (7+) idle-ticket badges on the
    /// list, matching the source system's ticket-age awareness.</summary>
    public int AgeDays => Math.Max(0, (DateTime.UtcNow.Date - CreatedAtUtc.Date).Days);
}

public partial class TicketsViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private List<RepairTicket> _allTickets = new();
    private Dictionary<Guid, Customer> _customersById = new();

    public ObservableCollection<TicketListRow> Tickets { get; } = new();
    public ObservableCollection<string> StatusFilters { get; } = new();

    [ObservableProperty] private TicketListRow? selectedTicket;
    [ObservableProperty] private string? selectedStatusFilter;
    [ObservableProperty] private string searchText = string.Empty;

    public TicketsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, INavigationService navigationService)
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
            var companyId = ActiveCompanyId;

            var company = await uow.Companies.GetByIdAsync(companyId)
                ?? throw new InvalidOperationException("No active company is selected.");

            StatusFilters.Clear();
            StatusFilters.Add("All");
            foreach (var status in company.TicketStatuses.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                StatusFilters.Add(status);
            SelectedStatusFilter ??= "All";

            _allTickets = await uow.RepairTickets.FindAsync(t => t.CompanyId == companyId);
            _customersById = (await uow.Customers.FindAsync(c => c.CompanyId == companyId)).ToDictionary(c => c.Id);

            ApplyFilter();
        });
    }

    partial void OnSelectedStatusFilterChanged(string? value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<RepairTicket> filtered = _allTickets;
        if (!string.IsNullOrEmpty(SelectedStatusFilter) && SelectedStatusFilter != "All")
            filtered = filtered.Where(t => t.Status == SelectedStatusFilter);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            filtered = filtered.Where(t =>
                t.TicketNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.Device.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.Issue.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (t.SerialNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Tickets.Clear();
        foreach (var ticket in filtered.OrderByDescending(t => t.CreatedAtUtc))
        {
            Tickets.Add(new TicketListRow
            {
                Id = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                CustomerName = ticket.CustomerId.HasValue && _customersById.TryGetValue(ticket.CustomerId.Value, out var c) ? c.Name : "Walk-in",
                Device = string.IsNullOrWhiteSpace(ticket.DeviceModel) ? ticket.Device : $"{ticket.Device} {ticket.DeviceModel}",
                Issue = ticket.Issue,
                Status = ticket.Status,
                Priority = ticket.Priority,
                CreatedAtUtc = ticket.CreatedAtUtc,
                Balance = ticket.Balance
            });
        }
    }

    [RelayCommand]
    private void NewTicket() => _navigationService.NavigateTo(PageKeys.TicketEdit, new TicketEditNavigationArgs(null));

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedTicket == null) return;
        _navigationService.NavigateTo(PageKeys.TicketEdit, new TicketEditNavigationArgs(SelectedTicket.Id));
    }
}
