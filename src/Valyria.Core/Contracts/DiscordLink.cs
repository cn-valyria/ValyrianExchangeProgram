using System;
using System.ComponentModel.DataAnnotations;

namespace Valyria.Core.Contracts;

/// <summary>
/// The Discord bot's request for a /register link token, for the Discord user who ran the command.
/// The ID is a string because Discord IDs don't fit in a JavaScript number
/// </summary>
public class DiscordLinkTokenRequest
{
    [Required]
    [RegularExpression(@"^\d{1,19}$", ErrorMessage = "DiscordId must be a Discord user ID")]
    public string DiscordId { get; set; } = string.Empty;

    // Discord usernames are at most 32 characters; account.discord holds 50
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string DiscordUsername { get; set; } = string.Empty;
}

public class DiscordLinkTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// A member's request to see, or approve, the Discord account behind a /register link token
/// </summary>
public class DiscordLinkRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// The Discord account a /register link token is for
/// </summary>
public class DiscordLinkDetails
{
    /// <summary>
    /// A string, since Discord IDs don't fit in a JavaScript number
    /// </summary>
    public string DiscordId { get; set; } = string.Empty;
    public string DiscordUsername { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
