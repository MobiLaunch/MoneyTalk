using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MoneyTalk.App.Dialogs;

/// <summary>A single search hit — which page to jump to and, for entities with a dedicated edit
/// page (tickets), the navigation parameter to open that specific record.</summary>
public class CommandPaletteResult
{
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string PageKey { get; init; } = string.Empty;
    public object? NavigationParameter { get; init; }

    public string DisplayText => string.IsNullOrEmpty(Subtitle) ? $"{Category}: {Title}" : $"{Category}: {Title} — {Subtitle}";
}

/// <summary>Ctrl+K global search across customers/items/tickets, built programmatically like
/// <see cref="SimpleFormDialog"/> rather than as a XAML file — the search box and result list are
/// the same shape regardless of what's being searched, so no template file is needed. The actual
/// database query is supplied by the caller (see <c>MainWindow.xaml.cs</c>) so this dialog has no
/// dependency on <see cref="MoneyTalk.Core.Interfaces.IUnitOfWork"/> or DI.</summary>
public class CommandPaletteDialog : ContentDialog
{
    private readonly Func<string, Task<List<CommandPaletteResult>>> _search;
    private readonly TextBox _searchBox;
    private readonly ListView _resultsList;
    private readonly TextBlock _emptyText;

    public CommandPaletteResult? SelectedResult { get; private set; }

    public CommandPaletteDialog(Func<string, Task<List<CommandPaletteResult>>> search)
    {
        _search = search;
        Title = "Search MoneyTalk";
        PrimaryButtonText = "Go";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        _searchBox = new TextBox { PlaceholderText = "Search customers, items, tickets…", MinWidth = 380 };
        _searchBox.TextChanged += async (_, _) => await RunSearchAsync();

        _resultsList = new ListView { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 320, DisplayMemberPath = nameof(CommandPaletteResult.DisplayText) };

        _emptyText = new TextBlock { Text = "Type to search.", Opacity = 0.6, Margin = new Thickness(0, 8, 0, 0) };

        Content = new StackPanel
        {
            Spacing = 10,
            Children = { _searchBox, _resultsList, _emptyText }
        };

        PrimaryButtonClick += (_, args) =>
        {
            if (_resultsList.SelectedItem is CommandPaletteResult result) SelectedResult = result;
            else args.Cancel = true;
        };

        Opened += (_, _) => _searchBox.Focus(FocusState.Programmatic);
    }

    private async Task RunSearchAsync()
    {
        var query = _searchBox.Text;
        var results = string.IsNullOrWhiteSpace(query) ? new List<CommandPaletteResult>() : await _search(query);

        _resultsList.ItemsSource = results;
        _emptyText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _emptyText.Text = string.IsNullOrWhiteSpace(query) ? "Type to search." : "No matches.";
        if (results.Count > 0) _resultsList.SelectedIndex = 0;
    }
}
