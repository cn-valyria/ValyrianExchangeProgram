namespace Valyria.Core.Contracts;

/// <summary>
/// OAuth 2.0 style token request for password grant type
/// </summary>
public class TokenRequest
{
    /// <summary>
    /// The grant type (e.g., "password" or "refresh_token")
    /// </summary>
    public string GrantType { get; set; } = string.Empty;

    /// <summary>
    /// Nation ID (username alternative)
    /// </summary>
    public string? NationId { get; set; }

    /// <summary>
    /// Ruler name (username alternative)
    /// </summary>
    public string? RulerName { get; set; }

    /// <summary>
    /// Password/Unique code for authentication
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Refresh token for refresh grant type
    /// </summary>
    public string? RefreshToken { get; set; }
}
