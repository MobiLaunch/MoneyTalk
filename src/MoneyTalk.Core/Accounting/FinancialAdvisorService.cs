using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;
using MoneyTalk.Core.Interfaces.Integrations;

namespace MoneyTalk.Core.Accounting;

/// <summary>Orchestrates the AI Assistant chat: grounds every turn in a fresh snapshot of the
/// company's actual books (via <see cref="FinancialContextBuilder"/>) and persists the
/// conversation. Depends only on <see cref="IGeminiClient"/>, so it never needs to know how the
/// HTTP call to Google is actually made.</summary>
public class FinancialAdvisorService
{
    private readonly IGeminiClient _geminiClient;
    private readonly FinancialContextBuilder _contextBuilder = new();

    public FinancialAdvisorService(IGeminiClient geminiClient)
    {
        _geminiClient = geminiClient;
    }

    public async Task<AiConversation> StartConversationAsync(IUnitOfWork uow, Guid companyId, string title, CancellationToken ct = default)
    {
        var conversation = new AiConversation { CompanyId = companyId, Title = title };
        await uow.AiConversations.AddAsync(conversation, ct);
        await uow.SaveChangesAsync(ct);
        return conversation;
    }

    /// <summary>Sends <paramref name="userMessage"/> to Gemini along with the conversation's
    /// prior turns and a freshly-built financial-context system instruction, then appends both
    /// the user's message and the assistant's reply to the conversation.</summary>
    public async Task<string> AskAsync(
        IUnitOfWork uow, Guid companyId, Guid conversationId, string apiKey, string userMessage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("No Gemini API key is configured. Add one on the Integrations settings page.");

        var conversation = await uow.AiConversations.GetByIdAsync(conversationId, ct)
            ?? throw new InvalidOperationException($"Conversation {conversationId} was not found.");

        var systemInstruction = await _contextBuilder.BuildSystemInstructionAsync(uow, companyId, DateTime.UtcNow, ct);
        var history = conversation.Messages
            .OrderBy(m => m.CreatedAtUtc)
            .Where(m => m.Role != AiMessageRole.System)
            .Select(m => new GeminiChatTurn(m.Role == AiMessageRole.User, m.Content))
            .ToList();

        var reply = await _geminiClient.GenerateContentAsync(apiKey, systemInstruction, history, userMessage, ct);

        conversation.Messages.Add(new AiMessage { ConversationId = conversationId, Role = AiMessageRole.User, Content = userMessage });
        conversation.Messages.Add(new AiMessage { ConversationId = conversationId, Role = AiMessageRole.Assistant, Content = reply });
        conversation.ModifiedAtUtc = DateTime.UtcNow;
        uow.AiConversations.Update(conversation);
        await uow.SaveChangesAsync(ct);

        return reply;
    }
}
