namespace Valyria.Core.DTOs;

/// <summary>
/// An account linked to a Discord user, as the Discord bot's /uniquecode command shows it
/// </summary>
public class DiscordAccount
{
    public int NationId { get; set; }
    /// <summary>
    /// Null if the nation isn't in cybernations_db.nation (e.g. CN deleted it)
    /// </summary>
    public string? NationName { get; set; }
    public string? RulerName { get; set; }
    public string UniqueCode { get; set; } = string.Empty;
}
