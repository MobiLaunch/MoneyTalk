using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class CustomersPage : Page
{
    public CustomersViewModel ViewModel { get; }

    public CustomersPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<CustomersViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void AddCustomer_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var dialog = new SimpleFormDialog("Add Customer", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "name", Label = "Customer name" },
            new TextFieldDescriptor { Key = "email", Label = "Email" },
            new TextFieldDescriptor { Key = "phone", Label = "Phone" },
            new NumberFieldDescriptor { Key = "terms", Label = "Payment terms (days)", InitialValue = 30, Minimum = 0, Maximum = 365 },
            new CheckboxFieldDescriptor { Key = "taxExempt", Label = "Tax exempt" }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await ViewModel.AddCustomerAsync(
            dialog.GetText("name"), dialog.GetText("email"), dialog.GetText("phone"),
            (int)dialog.GetNumber("terms"), dialog.GetBool("taxExempt"));
    }

    private async void CustomersGrid_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var customer = ViewModel.SelectedCustomer;
        if (customer == null) return;

        var dialog = new SimpleFormDialog("Edit Customer", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "name", Label = "Customer name", InitialValue = customer.Name },
            new TextFieldDescriptor { Key = "email", Label = "Email", InitialValue = customer.Email ?? string.Empty },
            new TextFieldDescriptor { Key = "phone", Label = "Phone", InitialValue = customer.Phone ?? string.Empty },
            new NumberFieldDescriptor { Key = "terms", Label = "Payment terms (days)", InitialValue = customer.PaymentTermsDays, Minimum = 0, Maximum = 365 },
            new CheckboxFieldDescriptor { Key = "taxExempt", Label = "Tax exempt", InitialValue = customer.TaxExempt },
            new CheckboxFieldDescriptor { Key = "isActive", Label = "Active", InitialValue = customer.IsActive }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await ViewModel.EditCustomerAsync(
            customer.Id, dialog.GetText("name"), dialog.GetText("email"), dialog.GetText("phone"),
            (int)dialog.GetNumber("terms"), dialog.GetBool("taxExempt"), dialog.GetBool("isActive"));
    }
}
