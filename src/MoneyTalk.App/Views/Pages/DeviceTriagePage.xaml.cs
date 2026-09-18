using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class DeviceTriagePage : Page
{
    public DeviceTriageViewModel ViewModel { get; }

    public DeviceTriagePage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DeviceTriageViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void AddRecord_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var customerNames = ViewModel.Customers.Select(c => c.Name).ToList();
        customerNames.Insert(0, "(none)");

        var dialog = new SimpleFormDialog("Add Device", new FormFieldDescriptor[]
        {
            new ComboFieldDescriptor { Key = "platform", Label = "Platform", Options = DeviceTriageViewModel.PlatformOptions },
            new TextFieldDescriptor { Key = "label", Label = "Device label", PlaceholderText = "e.g. iPhone 13 Pro — Jane's phone" },
            new TextFieldDescriptor { Key = "serial", Label = "Serial / IMEI (optional)" },
            new ComboFieldDescriptor { Key = "customer", Label = "Customer (optional)", Options = customerNames },
            new TextFieldDescriptor { Key = "notes", Label = "Notes (optional)" }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var customerIndex = dialog.GetComboIndex("customer");
        Guid? customerId = customerIndex > 0 ? ViewModel.Customers[customerIndex - 1].Id : null;

        await ViewModel.AddRecordAsync(
            dialog.GetComboValue("platform"), dialog.GetText("label"), dialog.GetText("serial"), customerId, dialog.GetText("notes"));
    }

    private async void Grid_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var row = ViewModel.SelectedRecord;
        if (row == null) return;

        var customers = ViewModel.Customers.ToList();
        var customerNames = customers.Select(c => c.Name).ToList();
        customerNames.Insert(0, "(none)");
        var currentCustomerIndex = row.CustomerId.HasValue ? customers.FindIndex(c => c.Id == row.CustomerId.Value) + 1 : 0;

        var platformOptions = DeviceTriageViewModel.PlatformOptions;
        var statusOptions = DeviceTriageViewModel.StatusOptions;

        var dialog = new SimpleFormDialog("Edit Device", new FormFieldDescriptor[]
        {
            new ComboFieldDescriptor { Key = "platform", Label = "Platform", Options = platformOptions, InitialIndex = platformOptions.ToList().IndexOf(row.Platform) },
            new TextFieldDescriptor { Key = "label", Label = "Device label", InitialValue = row.DeviceLabel },
            new TextFieldDescriptor { Key = "serial", Label = "Serial / IMEI (optional)", InitialValue = row.SerialOrIdentifier ?? string.Empty },
            new ComboFieldDescriptor { Key = "customer", Label = "Customer (optional)", Options = customerNames, InitialIndex = Math.Max(currentCustomerIndex, 0) },
            new ComboFieldDescriptor { Key = "status", Label = "Status", Options = statusOptions, InitialIndex = statusOptions.ToList().IndexOf(row.Status) },
            new CheckboxFieldDescriptor { Key = "backup", Label = "Backup completed", InitialValue = row.BackupCompleted },
            new CheckboxFieldDescriptor { Key = "diagnostics", Label = "Diagnostics completed", InitialValue = row.DiagnosticsCompleted },
            new CheckboxFieldDescriptor { Key = "restore", Label = "Restore completed", InitialValue = row.RestoreCompleted },
            new TextFieldDescriptor { Key = "osVersion", Label = "OS version detected (optional)", InitialValue = row.OsVersionDetected ?? string.Empty },
            new TextFieldDescriptor { Key = "diagSummary", Label = "Diagnostics summary (optional)", InitialValue = row.DiagnosticsSummary ?? string.Empty },
            new TextFieldDescriptor { Key = "notes", Label = "Notes (optional)", InitialValue = row.Notes ?? string.Empty }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var customerIndex = dialog.GetComboIndex("customer");
        Guid? customerId = customerIndex > 0 ? customers[customerIndex - 1].Id : null;

        await ViewModel.EditRecordAsync(
            row.Id, dialog.GetComboValue("platform"), dialog.GetText("label"), dialog.GetText("serial"), customerId,
            dialog.GetComboValue("status"), dialog.GetBool("backup"), dialog.GetBool("diagnostics"), dialog.GetBool("restore"),
            dialog.GetText("osVersion"), dialog.GetText("diagSummary"), dialog.GetText("notes"));
    }

    private async void BackUpAndroidDevice_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FileSavePicker { SuggestedFileName = "android-backup" };
        picker.FileTypeChoices.Add("Android Backup", new List<string> { ".ab" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        await ViewModel.BackUpAndroidDeviceAsync(file.Path);
    }
}
