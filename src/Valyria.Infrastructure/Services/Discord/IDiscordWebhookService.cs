using Valyria.Core.Entities;

namespace Valyria.Infrastructure.Services.Discord;

public interface IDiscordWebhookService
{
    Task SendNewDealsNotificationAsync(IReadOnlyCollection<long> discordIds, CancellationToken cancellationToken = default);
    Task SendMassMessageNotificationAsync(MassMessageCode code, IReadOnlyCollection<long> discordIds, CancellationToken cancellationToken = default);
}
