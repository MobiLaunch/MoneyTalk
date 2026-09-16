using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.ViewModels;
using Windows.System;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class AiAssistantPage : Page
{
    public AiAssistantViewModel ViewModel { get; }

    public AiAssistantPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<AiAssistantViewModel>();
        DataContext = ViewModel;
        ViewModel.Messages.CollectionChanged += (_, __) => ScrollToBottom();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private void ScrollToBottom() => MessagesScrollViewer.ChangeView(null, MessagesScrollViewer.ScrollableHeight, null);

    private async void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SendCommand.CanExecute(null))
        {
            e.Handled = true;
            await ViewModel.SendCommand.ExecuteAsync(null);
        }
    }

    private async void SuggestedPrompt_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button { Content: string prompt })
            await ViewModel.SendMessageAsync(prompt);
    }
}
