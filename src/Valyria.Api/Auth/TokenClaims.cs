using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Valyria.Core.DTOs;

namespace Valyria.Api.Auth;

/// <summary>
/// Claim types in the tokens the API issues. They're written as-is (no inbound/outbound mapping),
/// so clients decoding the token see these exact names.
/// </summary>
public static class TokenClaims
{
    // Credentials, used to re-check the account against the database on every request
    public const string NationId = "nid";
    public const string RulerName = "rul";
    public const string UniqueCode = "uqc";

    // Who the account is. Refreshed from the database on every request (see CustomJwtBearerEvents)
    public const string AccountId = "aid";
    /// <summary>
    /// The ruler name. Identity.Name reads this claim
    /// </summary>
    public const string Name = "name";
    /// <summary>
    /// The VEP role code (account.va), e.g. "B" for buyer
    /// </summary>
    public const string VepRole = "vep_role";
    /// <summary>
    /// One claim per admin role, e.g. "Super Admin". IsInRole and [Authorize] policies read this claim
    /// </summary>
    public const string AdminRole = "role";

    /// <summary>
    /// Claim types that describe the account, as opposed to the credentials
    /// </summary>
    public static readonly IReadOnlySet<string> AccountClaimTypes = new HashSet<string> { AccountId, Name, VepRole, AdminRole };

    public static IEnumerable<Claim> ForAccount(AuthorizedAccount account)
    {
        yield return new Claim(AccountId, account.AccountId.ToString());
        if (!string.IsNullOrEmpty(account.RulerName))
            yield return new Claim(Name, account.RulerName);
        if (account.Role != default)
            yield return new Claim(VepRole, account.Role.ToString());
        foreach (var role in account.Roles ?? Enumerable.Empty<AccountRole>())
            yield return new Claim(AdminRole, role.Name);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static int? GetAccountId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(TokenClaims.AccountId)?.Value, out var accountId) ? accountId : null;

    /// <summary>
    /// The ruler name to record in audit fields (created_by, cancelled_by, ...)
    /// </summary>
    public static string GetAuditName(this ClaimsPrincipal user) =>
        user.FindFirst(TokenClaims.Name)?.Value is { Length: > 0 } name ? name : "unknown";

    /// <summary>
    /// Members can only see their own account. Admins who can view the admin portal can see any account
    /// </summary>
    public static bool CanAccessAccount(this ClaimsPrincipal user, int accountId) =>
        user.GetAccountId() == accountId || AdminRoles.CanViewAdminPortal.Any(user.IsInRole);
}
