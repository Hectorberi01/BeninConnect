using System.Collections.ObjectModel;
using BeninConnect.App.Core.Mvvm;
using BeninConnect.App.Core.Navigation;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using BeninConnect.App.Features.Messaging.Contracts;
using BeninConnect.App.Features.Messaging.Models;
using BeninConnect.App.Features.Profile.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Features.Messaging.ViewModels;

public sealed class MessageItemViewModel(Message message, bool isMine, bool showDay, DateTimeOffset now)
{
    public Guid Id => message.Id;
    public string Content => message.Content;
    public bool IsMine { get; } = isMine;
    public bool IsTheirs => !IsMine;
    public bool IsSimulated => message.IsSimulated;
    public string TimeText => TimeFormatter.Clock(message.SentAt);
    public bool ShowDay { get; } = showDay;
    public string DayText => message.SentAt.Date == now.Date ? "Aujourd'hui"
        : message.SentAt.Date == now.Date.AddDays(-1) ? "Hier"
        : TimeFormatter.LongDate(message.SentAt);
}

/// <summary>Conversation privée : historique, envoi, lecture, réponse simulée explicite.</summary>
public sealed partial class ConversationViewModel(
    INavigationService navigation,
    IConversationRepository conversations,
    ISessionService session,
    IToastService toast,
    TimeProvider clock) : PageViewModel(navigation)
{
    // Réponses du scénario de démonstration : toujours étiquetées « réponse simulée » dans l'interface.
    private static readonly string[] DemoReplies =
    [
        "Merci pour ton message ! (réponse de démonstration)",
        "Bien reçu, je te redis ça rapidement. (réponse de démonstration)",
        "Super idée, on en parle ce week-end ? (réponse de démonstration)",
        "Avec plaisir ! (réponse de démonstration)",
    ];

    private Guid _conversationId;
    private Guid _otherUserId;
    private int _replyIndex;

    public ObservableCollection<MessageItemViewModel> Messages { get; } = [];

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private string _initials = string.Empty;
    [ObservableProperty] private string _avatarColor = "#087F5B";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _draft = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand), nameof(SimulateReplyCommand))]
    private bool _isSending;

    /// <summary>Déclenché après ajout d'un message (la vue fait défiler vers le bas).</summary>
    public event EventHandler? MessageAppended;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        _conversationId = parameter is Guid id ? id : Guid.Empty;
        return ReloadAsync();
    }

    protected override Task ReloadAsync() => LoadAsync(async ct =>
    {
        var me = session.CurrentUserId;
        var summary = await conversations.GetSummaryAsync(_conversationId, me, ct) ?? throw new NotFoundException("Conversation");
        _otherUserId = summary.OtherParticipant.Id;
        Title = summary.OtherParticipant.DisplayName;
        Subtitle = summary.OtherParticipant.Location;
        Initials = summary.OtherParticipant.Initials;
        AvatarColor = summary.OtherParticipant.AvatarColor;

        var messages = await conversations.GetMessagesAsync(_conversationId, ct);
        Messages.Clear();
        DateTime? lastDay = null;
        var now = clock.GetLocalNow();
        foreach (var m in messages)
        {
            Messages.Add(new MessageItemViewModel(m, m.SenderId == me, lastDay != m.SentAt.Date, now));
            lastDay = m.SentAt.Date;
        }

        await conversations.MarkAsReadAsync(_conversationId, me, ct);
        MessageAppended?.Invoke(this, EventArgs.Empty);
        return Messages.Count > 0;
    });

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(Draft);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var content = Draft;
        IsSending = true;
        try
        {
            var message = await conversations.SendAsync(new SendMessageRequest(_conversationId, session.CurrentUserId, content));
            Draft = string.Empty;
            Append(message, isMine: true);
        }
        catch (Exception ex)
        {
            toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool CanSimulate() => !IsSending;

    /// <summary>Scénario de démonstration explicite : ajoute une réponse clairement marquée comme simulée.</summary>
    [RelayCommand(CanExecute = nameof(CanSimulate))]
    private async Task SimulateReplyAsync()
    {
        IsSending = true;
        try
        {
            var text = DemoReplies[_replyIndex++ % DemoReplies.Length];
            var message = await conversations.SendAsync(new SendMessageRequest(_conversationId, _otherUserId, text, IsSimulated: true));
            Append(message, isMine: false);
            await conversations.MarkAsReadAsync(_conversationId, session.CurrentUserId);
        }
        catch (Exception ex)
        {
            toast.Show(ErrorMessages.From(ex), ToastKind.Error);
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private void OpenProfile() => Navigation.NavigateTo<ProfileViewModel>(_otherUserId);

    private void Append(Message message, bool isMine)
    {
        var showDay = Messages.Count == 0 || Messages[^1].DayText != "Aujourd'hui";
        Messages.Add(new MessageItemViewModel(message, isMine, showDay, clock.GetLocalNow()));
        State = ViewState.Content;
        MessageAppended?.Invoke(this, EventArgs.Empty);
    }
}
