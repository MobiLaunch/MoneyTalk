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
}
