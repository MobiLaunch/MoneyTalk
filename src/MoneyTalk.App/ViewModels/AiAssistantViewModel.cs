using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class AiAssistantViewModel : ViewModelBase
{
    private readonly FinancialAdvisorService _advisorService;
    private readonly ISecureTokenStore _secureTokenStore;

    private Guid? _conversationId;

    public ObservableCollection<AiMessage> Messages { get; } = new();

    [ObservableProperty] private string userInput = string.Empty;
    [ObservableProperty] private bool isSending;
    [ObservableProperty] private bool hasApiKey;

    public IReadOnlyList<string> SuggestedPrompts { get; } = new[]
    {
        "How is my cash flow looking for the next 30 days?",
        "Which customers owe me the most money right now?",
        "Where could I cut expenses this month?",
        "Am I on track with my budget this quarter?"
    };

    public AiAssistantViewModel(
        Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService,
        FinancialAdvisorService advisorService, ISecureTokenStore secureTokenStore)
        : base(unitOfWorkFactory, settingsService)
    {
        _advisorService = advisorService;
        _secureTokenStore = secureTokenStore;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        HasApiKey = !string.IsNullOrWhiteSpace(_secureTokenStore.GetSecret(SecretKeys.GeminiApiKey));

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var existing = await uow.AiConversations.FindAsync(c => c.CompanyId == ActiveCompanyId);
            var conversation = existing.OrderByDescending(c => c.ModifiedAtUtc).FirstOrDefault();

            if (conversation == null)
            {
                conversation = await _advisorService.StartConversationAsync(uow, ActiveCompanyId, "Financial Advisor");
            }

            _conversationId = conversation.Id;
            Messages.Clear();
            foreach (var message in conversation.Messages.OrderBy(m => m.CreatedAtUtc))
                Messages.Add(message);
        });
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        await SendMessageAsync(UserInput);
    }

    public async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || !_conversationId.HasValue || IsSending) return;

        var apiKey = _secureTokenStore.GetSecret(SecretKeys.GeminiApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ErrorMessage = "Add a Gemini API key on the Integrations page first.";
            return;
        }

        UserInput = string.Empty;
        // Held in a local so the failure path can undo exactly this optimistic bubble rather than
        // whatever happens to be last by then.
        var optimistic = new AiMessage { ConversationId = _conversationId.Value, Role = AiMessageRole.User, Content = message };
        Messages.Add(optimistic);

        IsSending = true;
        ErrorMessage = null;
        try
        {
            using var uow = NewUnitOfWork();
            var reply = await _advisorService.AskAsync(uow, ActiveCompanyId, _conversationId.Value, apiKey, message);
            Messages.Add(new AiMessage { ConversationId = _conversationId.Value, Role = AiMessageRole.Assistant, Content = reply });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            if (Messages.Count > 0 && ReferenceEquals(Messages[^1], optimistic))
                Messages.Remove(optimistic);
            // The send failed, so hand the user their text back instead of making them retype it.
            UserInput = message;
        }
        finally
        {
            IsSending = false;
        }
    }
}
