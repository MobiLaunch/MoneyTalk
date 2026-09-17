using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class VendorRepairsPage : Page
{
    public VendorRepairsViewModel ViewModel { get; }

    public VendorRepairsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<VendorRepairsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void AddVendorRepair_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var customerNames = ViewModel.Customers.Select(c => c.Name).ToList();
        customerNames.Insert(0, "(none)");

        var dialog = new SimpleFormDialog("Send Device to Vendor", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "device", Label = "Device" },
            new TextFieldDescriptor { Key = "issue", Label = "Issue" },
            new TextFieldDescriptor { Key = "vendor", Label = "Vendor name" },
            new ComboFieldDescriptor { Key = "customer", Label = "Customer (optional)", Options = customerNames },
            new DateFieldDescriptor { Key = "sentDate", Label = "Sent date" },
            new DateFieldDescriptor { Key = "estReturn", Label = "Estimated return date" },
            new TextFieldDescriptor { Key = "notes", Label = "Notes (optional)" }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var customerIndex = dialog.GetComboIndex("customer");
        Guid? customerId = customerIndex > 0 ? ViewModel.Customers[customerIndex - 1].Id : null;

        await ViewModel.AddVendorRepairAsync(
            dialog.GetText("device"), dialog.GetText("issue"), dialog.GetText("vendor"), customerId,
            dialog.GetDate("sentDate").DateTime, dialog.GetDate("estReturn").DateTime, dialog.GetText("notes"));
    }

    private async void Grid_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var row = ViewModel.SelectedVendorRepair;
        if (row == null) return;

        var existingNotes = await ViewModel.GetVendorRepairNotesAsync(row.Id);

        var dialog = new SimpleFormDialog("Update Vendor Repair", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "device", Label = "Device", InitialValue = row.Device },
            new TextFieldDescriptor { Key = "issue", Label = "Issue", InitialValue = row.Issue },
            new TextFieldDescriptor { Key = "vendor", Label = "Vendor name", InitialValue = row.VendorName },
            new TextFieldDescriptor { Key = "tracking", Label = "Tracking #", InitialValue = row.TrackingNumber },
            new TextFieldDescriptor { Key = "status", Label = "Status", InitialValue = row.Status },
            new DateFieldDescriptor { Key = "sentDate", Label = "Sent date", InitialValue = row.SentDate ?? DateTimeOffset.Now.Date },
            new DateFieldDescriptor { Key = "estReturn", Label = "Estimated return date", InitialValue = row.EstimatedReturnDate ?? DateTimeOffset.Now.Date },
            new TextFieldDescriptor { Key = "notes", Label = "Notes (optional)", InitialValue = existingNotes ?? string.Empty }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await ViewModel.EditVendorRepairAsync(
            row.Id, dialog.GetText("device"), dialog.GetText("issue"), dialog.GetText("vendor"),
            dialog.GetText("tracking"), dialog.GetText("status"),
            dialog.GetDate("sentDate").DateTime, dialog.GetDate("estReturn").DateTime, dialog.GetText("notes"));
    }
}
