using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public enum CalendarEventType { Appointment, HouseCall }

public class CalendarEventRow
{
    public Guid Id { get; init; }
    public CalendarEventType EventType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public DateTime? Date { get; init; }
    public string? Time { get; init; }
    public string Status { get; init; } = string.Empty;
}

public partial class CalendarViewModel : ViewModelBase
{
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<CalendarEventRow> Events { get; } = new();

    [ObservableProperty] private CalendarEventRow? selectedEvent;

    public static IReadOnlyList<string> AppointmentStatusOptions { get; } = Enum.GetNames<AppointmentStatus>();
    public static IReadOnlyList<string> HouseCallStatusOptions { get; } = Enum.GetNames<HouseCallStatus>();

    public CalendarViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService)
        : base(unitOfWorkFactory, settingsService)
    {
    }

    [RelayCommand]
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

            var appointments = await uow.Appointments.FindAsync(a => a.CompanyId == companyId);
            var houseCalls = await uow.HouseCalls.FindAsync(h => h.CompanyId == companyId);

            var rows = appointments.Select(a => new CalendarEventRow
            {
                Id = a.Id,
                EventType = CalendarEventType.Appointment,
                Title = a.Title,
                CustomerName = a.CustomerId.HasValue && customersById.TryGetValue(a.CustomerId.Value, out var c1) ? c1.Name : string.Empty,
                Date = a.ScheduledDate,
                Time = a.ScheduledTime,
                Status = a.Status.ToString()
            }).Concat(houseCalls.Select(h => new CalendarEventRow
            {
                Id = h.Id,
                EventType = CalendarEventType.HouseCall,
                Title = h.Description ?? "House Call",
                CustomerName = h.CustomerId.HasValue && customersById.TryGetValue(h.CustomerId.Value, out var c2) ? c2.Name : string.Empty,
                Date = h.ScheduledDate,
                Time = h.ScheduledTime,
                Status = h.Status.ToString()
            }));

            Events.Clear();
            foreach (var row in rows.OrderBy(r => r.Date ?? DateTime.MaxValue))
                Events.Add(row);
        });
    }

    public async Task<bool> AddAppointmentAsync(string title, string? description, Guid? customerId, DateTime date, string? time, string? notes)
    {
        if (string.IsNullOrWhiteSpace(title)) { ErrorMessage = "Enter a title first."; return false; }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            await uow.Appointments.AddAsync(new Appointment
            {
                CompanyId = ActiveCompanyId,
                Title = title.Trim(),
                Description = description,
                CustomerId = customerId,
                ScheduledDate = date,
                ScheduledTime = time,
                Notes = notes
            });
            await uow.SaveChangesAsync();
            success = true;
        });

        if (success) await LoadAsync();
        return success;
    }

    public async Task<bool> AddHouseCallAsync(string description, string? address, Guid? customerId, DateTime date, string? time, string? notes)
    {
        if (string.IsNullOrWhiteSpace(description)) { ErrorMessage = "Enter a description first."; return false; }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            await uow.HouseCalls.AddAsync(new HouseCall
            {
                CompanyId = ActiveCompanyId,
                Description = description.Trim(),
                Address = address,
                CustomerId = customerId,
                ScheduledDate = date,
                ScheduledTime = time,
                Notes = notes
            });
            await uow.SaveChangesAsync();
            success = true;
        });

        if (success) await LoadAsync();
        return success;
    }

    public async Task<bool> UpdateAppointmentStatusAsync(Guid id, AppointmentStatus status)
    {
        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var appointment = await uow.Appointments.GetByIdAsync(id) ?? throw new InvalidOperationException("This appointment no longer exists.");
            appointment.Status = status;
            uow.Appointments.Update(appointment);
            await uow.SaveChangesAsync();
            success = true;
        });

        if (success) await LoadAsync();
        return success;
    }

    public async Task<bool> UpdateHouseCallStatusAsync(Guid id, HouseCallStatus status)
    {
        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var houseCall = await uow.HouseCalls.GetByIdAsync(id) ?? throw new InvalidOperationException("This house call no longer exists.");
            houseCall.Status = status;
            uow.HouseCalls.Update(houseCall);
            await uow.SaveChangesAsync();
            success = true;
        });

        if (success) await LoadAsync();
        return success;
    }
}
