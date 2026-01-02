using System.Text.Json.Serialization;

namespace Valyria.Infrastructure.Services.Discord.Models;

public class DiscordWebhookMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("embeds")]
    public List<DiscordEmbed>? Embeds { get; set; }

    [JsonPropertyName("attachments")]
    public List<object>? Attachments { get; set; }
}

public class DiscordEmbed
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("color")]
    public int? Color { get; set; }
}
