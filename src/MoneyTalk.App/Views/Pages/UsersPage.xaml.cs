using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class UsersPage : Page
{
    public UsersViewModel ViewModel { get; }

    public UsersPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<UsersViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void AddUser_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var dialog = new SimpleFormDialog("Add User", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "name", Label = "Name" },
            new TextFieldDescriptor { Key = "email", Label = "Email" },
            new ComboFieldDescriptor { Key = "role", Label = "Role", Options = UsersViewModel.RoleOptions, InitialIndex = 2 }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var role = Enum.Parse<UserRole>(dialog.GetComboValue("role"));
        await ViewModel.AddUserAsync(dialog.GetText("name"), dialog.GetText("email"), role);
    }

    private void ToggleActive_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button { Tag: User user })
            ViewModel.ToggleActiveCommand.Execute(user);
    }
}
