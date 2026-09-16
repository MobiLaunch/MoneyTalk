using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MoneyTalk.App.ViewModels;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class OnboardingPage : Page
{
    public OnboardingViewModel ViewModel { get; }

    public OnboardingPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<OnboardingViewModel>();
        DataContext = ViewModel;
    }
}
