using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Services.Discord.Models;

namespace Valyria.Infrastructure.Services.Discord;

public class DiscordWebhookService : IDiscordWebhookService
{
    private readonly ILogger<DiscordWebhookService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IListsRepository _listsRepository;
    private readonly DiscordWebhookOptions _options;

    public DiscordWebhookService(
        ILogger<DiscordWebhookService> logger,
        IHttpClientFactory httpClientFactory,
        IListsRepository listsRepository,
        IOptions<DiscordWebhookOptions> options)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _listsRepository = listsRepository;
        _options = options.Value;
    }

    public async Task SendListNotificationAsync(int listId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            _logger.LogWarning("Discord webhook URL is not configured. Skipping list notification.");
            return;
        }

        try
        {
            var list = await _listsRepository.GetAidListAsync(listId);

            if (list == null)
            {
                _logger.LogWarning("List with ID {listId} not found. Skipping notification.", listId);
                return;
            }

            var content = $"Heads up, you're due to send {(listId == 1 ? "cash" : "tech")} today! {(
                string.Join(" ", list.Recipients
                    .Where(r => r.DiscordUniqueId.HasValue && r.DiscordUniqueId.Value != 0)
                    .Select(r => $"<@{r.DiscordUniqueId}>"))
            )}";

            var message = new DiscordWebhookMessage
            {
                Content = content,
                Embeds =
                [
                    new()
                    {
                        Title = list.Subject,
                        Description = list.Message,
                        Color = null
                    }
                ],
                Attachments = []
            };

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsJsonAsync(_options.WebhookUrl, message, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send Discord webhook for list {listId}. Status: {statusCode}, Content: {errorContent}",
                    listId, response.StatusCode, errorContent);
            }
            else
            {
                _logger.LogInformation("Successfully sent notification for list {listId} ({listName}) to Discord webhook.",
                    listId, list.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending Discord webhook notification for list {listId}.", listId);
        }
    }
}
