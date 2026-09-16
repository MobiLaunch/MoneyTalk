namespace MoneyTalk.App.Services;

public interface INavigationService
{
    void Initialize(Microsoft.UI.Xaml.Controls.Frame frame);
    bool NavigateTo(string pageKey, object? parameter = null);
    bool GoBack();
    bool CanGoBack { get; }
}
