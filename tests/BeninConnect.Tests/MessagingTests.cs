using BeninConnect.App.Features.Messaging.Contracts;
using BeninConnect.App.Features.Messaging.ViewModels;
using BeninConnect.App.Shell;

namespace BeninConnect.Tests;

public class MessagingTests
{
    [Fact]
    public async Task Sending_message_updates_conversation_and_list()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var shell = await host.OpenAsync<ShellViewModel>();
        shell.ShowMessagesCommand.Execute(null);
        await shell.ActivationTask;
        var list = shell.Messages;
        Assert.Equal(12, list.Conversations.Count);

        var last = list.Conversations[^1];
        var conversation = await host.OpenAsync<ConversationViewModel>(last.Id);
        var before = conversation.Messages.Count;

        conversation.Draft = "Bonjour, ravi d'échanger avec toi !";
        await conversation.SendCommand.ExecuteAsync(null);

        Assert.Equal(before + 1, conversation.Messages.Count);
        Assert.True(conversation.Messages[^1].IsMine);
        Assert.Equal(string.Empty, conversation.Draft);

        // La liste est actualisée : la conversation remonte en tête avec le dernier message.
        Assert.Equal(last.Id, list.Conversations[0].Id);
        Assert.Equal("Vous : Bonjour, ravi d'échanger avec toi !", list.Conversations[0].LastMessage);
    }

    [Fact]
    public async Task Opening_conversation_marks_messages_as_read()
    {
        using var host = new TestHost();
        var me = host.SignInDemo();
        var repo = host.Get<IConversationRepository>();
        var shell = await host.OpenAsync<ShellViewModel>();
        var unreadBefore = await repo.GetUnreadCountAsync(me.Id);
        Assert.True(unreadBefore > 0);
        Assert.Equal(unreadBefore, shell.UnreadMessages);

        var summaries = await repo.GetSummariesAsync(me.Id);
        var unread = summaries.First(s => s.UnreadCount > 0);
        await host.OpenAsync<ConversationViewModel>(unread.Conversation.Id);

        Assert.Equal(unreadBefore - unread.UnreadCount, await repo.GetUnreadCountAsync(me.Id));
        Assert.Equal(unreadBefore - unread.UnreadCount, shell.UnreadMessages);
    }

    [Fact]
    public async Task Simulated_reply_is_explicit_and_flagged()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var id = host.Store.Conversations[0].Id;
        var conversation = await host.OpenAsync<ConversationViewModel>(id);

        await conversation.SimulateReplyCommand.ExecuteAsync(null);

        var reply = conversation.Messages[^1];
        Assert.False(reply.IsMine);
        Assert.True(reply.IsSimulated);
        Assert.Contains("démonstration", reply.Content);
    }

    [Fact]
    public async Task Empty_message_cannot_be_sent()
    {
        using var host = new TestHost();
        host.SignInDemo();
        var conversation = await host.OpenAsync<ConversationViewModel>(host.Store.Conversations[0].Id);

        conversation.Draft = "   ";

        Assert.False(conversation.SendCommand.CanExecute(null));
    }

    [Fact]
    public async Task Starting_conversation_from_profile_reuses_or_creates_it()
    {
        using var host = new TestHost();
        var me = host.SignInDemo();
        var repo = host.Get<IConversationRepository>();
        var stranger = host.Store.Users.First(u => u.Id != me.Id
            && !host.Store.Conversations.Any(c => c.ParticipantIds.Contains(u.Id)));

        var first = await repo.GetOrCreateDirectAsync(me.Id, stranger.Id);
        var second = await repo.GetOrCreateDirectAsync(me.Id, stranger.Id);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(13, host.Store.Conversations.Count);
    }
}
