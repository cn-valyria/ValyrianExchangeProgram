namespace Valyria.Infrastructure.Services.Discord;

public interface IDiscordWebhookService
{
    Task SendNewDealsNotificationAsync(IReadOnlyCollection<long> discordIds, CancellationToken cancellationToken = default);
}
