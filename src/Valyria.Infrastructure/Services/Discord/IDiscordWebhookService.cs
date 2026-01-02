namespace Valyria.Infrastructure.Services.Discord;

public interface IDiscordWebhookService
{
    Task SendListNotificationAsync(int listId, CancellationToken cancellationToken = default);
}
