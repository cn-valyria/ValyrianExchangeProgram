using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Valyria.Infrastructure.Services.Discord.Models;

namespace Valyria.Infrastructure.Services.Discord;

public class DiscordWebhookService : IDiscordWebhookService
{
    // Discord rejects message content over 2000 characters, so mentions are split across messages
    private const int MaxContentLength = 2000;
    private const string NewDealsHeader = "You've been matched with a new VEP partner! Check your account page to see who you're trading with.";

    private readonly ILogger<DiscordWebhookService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DiscordWebhookOptions _options;

    public DiscordWebhookService(
        ILogger<DiscordWebhookService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<DiscordWebhookOptions> options)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task SendNewDealsNotificationAsync(IReadOnlyCollection<long> discordIds, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            _logger.LogWarning("Discord webhook URL is not configured. Skipping new deals notification.");
            return;
        }

        if (discordIds.Count == 0)
        {
            _logger.LogInformation("No one picked up a new deal partner. Skipping new deals notification.");
            return;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            foreach (var content in BuildMentionMessages(NewDealsHeader, discordIds))
            {
                var message = new DiscordWebhookMessage { Content = content, Embeds = [], Attachments = [] };
                var response = await httpClient.PostAsJsonAsync(_options.WebhookUrl, message, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to send new deals Discord webhook. Status: {statusCode}, Content: {errorContent}",
                        response.StatusCode, errorContent);
                    return;
                }
            }

            _logger.LogInformation("Successfully notified {count} accounts of new deal partners on Discord.", discordIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending new deals Discord webhook notification.");
        }
    }

    private static IEnumerable<string> BuildMentionMessages(string header, IEnumerable<long> discordIds)
    {
        var content = header;
        foreach (var mention in discordIds.Select(id => $"<@{id}>"))
        {
            if (content.Length + mention.Length + 1 > MaxContentLength)
            {
                yield return content;
                content = mention;
                continue;
            }

            content += $" {mention}";
        }

        yield return content;
    }
}
