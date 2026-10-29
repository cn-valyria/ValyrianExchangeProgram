namespace Valyria.Core.DTOs;

/// <summary>
/// Settings members can change on their own account. Unlike AccountUpdateRequest, nothing here
/// affects matching or the account's role
/// </summary>
public class AccountSettings
{
    /// <summary>
    /// False to stop the data loader pinging the account on Discord
    /// </summary>
    public bool DiscordNotificationsEnabled { get; set; }
}
