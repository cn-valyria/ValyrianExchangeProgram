using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Valyria.Core.Entities;
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

    public Task SendNewDealsNotificationAsync(IReadOnlyCollection<long> discordIds, CancellationToken cancellationToken = default) =>
        SendMentionsAsync("new deals", NewDealsHeader, discordIds, cancellationToken);

    public Task SendMassMessageNotificationAsync(MassMessageCode code, IReadOnlyCollection<long> discordIds, CancellationToken cancellationToken = default) =>
        SendMentionsAsync($"{code} mass message", GetMassMessageHeader(code), discordIds, cancellationToken);

    // Like the mass messages themselves, these never include deal-specific information
    private static string GetMassMessageHeader(MassMessageCode code) => code switch
    {
        MassMessageCode.SendAid => "Heads up, you have VEP aid to send or an aid slot to free up today! Check the \"Action Needed Today\" card on your account page to see what to do.",
        MassMessageCode.AcceptAid => "Heads up, you have a VEP aid offer waiting for you to accept in-game! If it expires, the deal is cancelled. Check the \"Action Needed Today\" card on your account page.",
        MassMessageCode.Overdue => "One of your VEP deals has been flagged because it's waiting on you. Please check the \"Action Needed Today\" card on your account page as soon as possible.",
        MassMessageCode.FinalWarning => "FINAL WARNING: One of your VEP deals will be cancelled at the next daily update unless you act today. Check the \"Action Needed Today\" card on your account page now.",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown mass message code")
    };

    private async Task SendMentionsAsync(string notificationName, string header, IReadOnlyCollection<long> discordIds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            _logger.LogWarning("Discord webhook URL is not configured. Skipping {notificationName} notification.", notificationName);
            return;
        }

        if (discordIds.Count == 0)
        {
            _logger.LogInformation("No one to notify. Skipping {notificationName} notification.", notificationName);
            return;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            foreach (var content in BuildMentionMessages(header, discordIds))
            {
                var message = new DiscordWebhookMessage { Content = content, Embeds = [], Attachments = [] };
                var response = await httpClient.PostAsJsonAsync(_options.WebhookUrl, message, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to send {notificationName} Discord webhook. Status: {statusCode}, Content: {errorContent}",
                        notificationName, response.StatusCode, errorContent);
                    return;
                }
            }

            _logger.LogInformation("Successfully sent {notificationName} notification to {count} accounts on Discord.", notificationName, discordIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending {notificationName} Discord webhook notification.", notificationName);
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
