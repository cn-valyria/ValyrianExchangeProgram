using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Valyria.Core.Contracts;
using Valyria.Core.Interfaces;

namespace Valyria.Api.Auth;

/// <summary>
/// Custom JWT Bearer events that validate the user against the database
/// using the AuthorizationRepository after token validation.
/// </summary>
public class CustomJwtBearerEvents : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var authorizationRepository = context.HttpContext.RequestServices
            .GetRequiredService<IAuthorizationRepository>();

        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<CustomJwtBearerEvents>>();

        var claimsIdentity = context.Principal?.Identity as ClaimsIdentity;
        if (claimsIdentity == null)
        {
            logger.LogWarning("TokenValidated: No claims identity found");
            context.Fail("Invalid token");
            return;
        }

        // Extract claims from the token
        var nationIdClaim = claimsIdentity.FindFirst(TokenClaims.NationId)?.Value;
        var rulerNameClaim = claimsIdentity.FindFirst(TokenClaims.RulerName)?.Value;
        var uniqueCodeClaim = claimsIdentity.FindFirst(TokenClaims.UniqueCode)?.Value;

        if (string.IsNullOrEmpty(uniqueCodeClaim))
        {
            logger.LogWarning("TokenValidated: UniqueCode claim is missing");
            context.Fail("Invalid token: missing unique code");
            return;
        }

        // Validate the user exists in the database
        try
        {
            var authorizedAccount = await ValidateUserInDatabase(
                authorizationRepository,
                nationIdClaim,
                rulerNameClaim,
                uniqueCodeClaim);

            if (authorizedAccount == null)
            {
                logger.LogWarning("TokenValidated: User not found in database. NationId: {NationId}, RulerName: {RulerName}",
                    nationIdClaim, rulerNameClaim);
                context.Fail("User not authorized");
                return;
            }

            // Add the AccountId and Roles to the claims for easy access in controllers
            claimsIdentity.AddClaim(new Claim("AccountId", authorizedAccount.AccountId.ToString()));

            if (authorizedAccount.Roles != null)
            {
                foreach (var role in authorizedAccount.Roles)
                {
                    claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role.Name));
                }
            }

            logger.LogDebug("TokenValidated: User authenticated successfully. AccountId: {AccountId}",
                authorizedAccount.AccountId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TokenValidated: Error validating user in database");
            context.Fail("Authentication failed");
            return;
        }

        await base.TokenValidated(context);
    }

    private static async Task<Core.DTOs.AuthorizedAccount?> ValidateUserInDatabase(
        IAuthorizationRepository authorizationRepository,
        string? nationId,
        string? rulerName,
        string uniqueCode)
    {
        if (!string.IsNullOrEmpty(nationId) && int.TryParse(nationId, out var nationIdInt))
        {
            return await authorizationRepository.Authorize(nationIdInt, uniqueCode);
        }
        else if (!string.IsNullOrEmpty(rulerName))
        {
            return await authorizationRepository.Authorize(rulerName, uniqueCode);
        }

        return null;
    }
}
