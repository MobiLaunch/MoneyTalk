using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class CalendarPage : Page
{
    public CalendarViewModel ViewModel { get; }

    public CalendarPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<CalendarViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void AddAppointment_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var customerNames = ViewModel.Customers.Select(c => c.Name).ToList();
        customerNames.Insert(0, "(none)");

        var dialog = new SimpleFormDialog("Add Appointment", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "title", Label = "Title" },
            new TextFieldDescriptor { Key = "description", Label = "Description (optional)" },
            new ComboFieldDescriptor { Key = "customer", Label = "Customer (optional)", Options = customerNames },
            new DateFieldDescriptor { Key = "date", Label = "Date" },
            new TextFieldDescriptor { Key = "time", Label = "Time (optional)", PlaceholderText = "e.g. 2:00 PM" },
            new TextFieldDescriptor { Key = "notes", Label = "Notes (optional)" }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var customerIndex = dialog.GetComboIndex("customer");
        Guid? customerId = customerIndex > 0 ? ViewModel.Customers[customerIndex - 1].Id : null;

        await ViewModel.AddAppointmentAsync(
            dialog.GetText("title"), dialog.GetText("description"), customerId,
            dialog.GetDate("date").DateTime, dialog.GetText("time"), dialog.GetText("notes"));
    }

    private async void AddHouseCall_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var customerNames = ViewModel.Customers.Select(c => c.Name).ToList();
        customerNames.Insert(0, "(none)");

        var dialog = new SimpleFormDialog("Add House Call", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "description", Label = "Description" },
            new TextFieldDescriptor { Key = "address", Label = "Address (optional)" },
            new ComboFieldDescriptor { Key = "customer", Label = "Customer (optional)", Options = customerNames },
            new DateFieldDescriptor { Key = "date", Label = "Date" },
            new TextFieldDescriptor { Key = "time", Label = "Time (optional)", PlaceholderText = "e.g. 2:00 PM" },
            new TextFieldDescriptor { Key = "notes", Label = "Notes (optional)" }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var customerIndex = dialog.GetComboIndex("customer");
        Guid? customerId = customerIndex > 0 ? ViewModel.Customers[customerIndex - 1].Id : null;

        await ViewModel.AddHouseCallAsync(
            dialog.GetText("description"), dialog.GetText("address"), customerId,
            dialog.GetDate("date").DateTime, dialog.GetText("time"), dialog.GetText("notes"));
    }

    private async void EventsGrid_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var row = ViewModel.SelectedEvent;
        if (row == null) return;

        var statusOptions = row.EventType == CalendarEventType.Appointment
            ? CalendarViewModel.AppointmentStatusOptions
            : CalendarViewModel.HouseCallStatusOptions;

        var dialog = new SimpleFormDialog($"Update Status — {row.Title}", new FormFieldDescriptor[]
        {
            new ComboFieldDescriptor { Key = "status", Label = "Status", Options = statusOptions, InitialIndex = Math.Max(statusOptions.ToList().IndexOf(row.Status), 0) }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        if (row.EventType == CalendarEventType.Appointment)
            await ViewModel.UpdateAppointmentStatusAsync(row.Id, Enum.Parse<AppointmentStatus>(dialog.GetComboValue("status")));
        else
            await ViewModel.UpdateHouseCallStatusAsync(row.Id, Enum.Parse<HouseCallStatus>(dialog.GetComboValue("status")));
    }
}
