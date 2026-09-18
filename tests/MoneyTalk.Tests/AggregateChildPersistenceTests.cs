using MoneyTalk.Core.Entities;
using Xunit;

namespace MoneyTalk.Tests;

/// <summary>Guards the model's client-assigned-Guid key configuration. EF's default convention
/// treats a Guid primary key as store-generated, which makes a brand-new child added to an
/// already-persisted parent's collection look like an existing row: EF emits an UPDATE for a row
/// that was never inserted and the save fails with "expected to affect 1 row(s), but actually
/// affected 0". Needs a real relational provider to catch — the EF InMemory provider reports no
/// rows-affected mismatch at all.</summary>
public class AggregateChildPersistenceTests
{
    [Fact]
    public async Task AddingChildToPersistedParent_InsertsTheNewChild()
    {
        using var db = new TestDatabase();
        var company = await db.CreateCompanyAsync();

        Guid conversationId;
        using (var uow = db.NewUnitOfWork())
        {
            var conversation = new AiConversation { CompanyId = company.Id, Title = "Cash flow" };
            conversation.Messages.Add(new AiMessage
            {
                ConversationId = conversation.Id,
                Role = AiMessageRole.User,
                Content = "How is cash flow?"
            });
            await uow.AiConversations.AddAsync(conversation);
            await uow.SaveChangesAsync();
            conversationId = conversation.Id;
        }

        using (var uow = db.NewUnitOfWork())
        {
            var conversation = await uow.AiConversations.GetByIdAsync(conversationId);
            conversation!.Messages.Add(new AiMessage
            {
                ConversationId = conversationId,
                Role = AiMessageRole.Assistant,
                Content = "Healthy for now."
            });
            await uow.SaveChangesAsync();
        }

        using var verifyUow = db.NewUnitOfWork();
        var reloaded = await verifyUow.AiConversations.GetByIdAsync(conversationId);
        Assert.Equal(2, reloaded!.Messages.Count);
        Assert.Equal("Healthy for now.", reloaded.Messages.Single(m => m.Role == AiMessageRole.Assistant).Content);
    }
}
