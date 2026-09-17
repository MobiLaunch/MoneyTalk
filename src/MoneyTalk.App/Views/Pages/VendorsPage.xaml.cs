using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class VendorsPage : Page
{
    public VendorsViewModel ViewModel { get; }

    public VendorsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<VendorsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void AddVendor_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var dialog = new SimpleFormDialog("Add Vendor", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "name", Label = "Vendor name" },
            new TextFieldDescriptor { Key = "email", Label = "Email" },
            new TextFieldDescriptor { Key = "phone", Label = "Phone" },
            new NumberFieldDescriptor { Key = "terms", Label = "Payment terms (days)", InitialValue = 30, Minimum = 0, Maximum = 365 },
            new CheckboxFieldDescriptor { Key = "is1099", Label = "1099 vendor" }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await ViewModel.AddVendorAsync(
            dialog.GetText("name"), dialog.GetText("email"), dialog.GetText("phone"),
            (int)dialog.GetNumber("terms"), dialog.GetBool("is1099"));
    }

    private async void VendorsGrid_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var vendor = ViewModel.SelectedVendor;
        if (vendor == null) return;

        var dialog = new SimpleFormDialog("Edit Vendor", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "name", Label = "Vendor name", InitialValue = vendor.Name },
            new TextFieldDescriptor { Key = "email", Label = "Email", InitialValue = vendor.Email ?? string.Empty },
            new TextFieldDescriptor { Key = "phone", Label = "Phone", InitialValue = vendor.Phone ?? string.Empty },
            new NumberFieldDescriptor { Key = "terms", Label = "Payment terms (days)", InitialValue = vendor.PaymentTermsDays, Minimum = 0, Maximum = 365 },
            new CheckboxFieldDescriptor { Key = "is1099", Label = "1099 vendor", InitialValue = vendor.Is1099Vendor },
            new CheckboxFieldDescriptor { Key = "isActive", Label = "Active", InitialValue = vendor.IsActive }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await ViewModel.EditVendorAsync(
            vendor.Id, dialog.GetText("name"), dialog.GetText("email"), dialog.GetText("phone"),
            (int)dialog.GetNumber("terms"), dialog.GetBool("is1099"), dialog.GetBool("isActive"));
    }
}
