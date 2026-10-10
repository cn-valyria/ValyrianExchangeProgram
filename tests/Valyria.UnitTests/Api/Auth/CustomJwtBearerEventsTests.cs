using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Valyria.Api.Auth;
using Valyria.Core.DTOs;
using Valyria.Core.Interfaces;

namespace Valyria.UnitTests.Api.Auth;

public class CustomJwtBearerEventsTests
{
    private readonly IAuthorizationRepository _repository = Substitute.For<IAuthorizationRepository>();

    private TokenValidatedContext CreateContext(ClaimsPrincipal? principal)
    {
        var services = new ServiceCollection()
            .AddSingleton(_repository)
            .AddSingleton<ILogger<CustomJwtBearerEvents>>(NullLogger<CustomJwtBearerEvents>.Instance)
            .BuildServiceProvider();

        return new TokenValidatedContext(
            new DefaultHttpContext { RequestServices = services },
            new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)),
            new JwtBearerOptions())
        {
            Principal = principal
        };
    }

    private static ClaimsPrincipal Principal(string? nationId, string? rulerName, string? uniqueCode, params Claim[] accountClaims)
    {
        var claims = new List<Claim>(accountClaims);
        if (nationId != null) claims.Add(new Claim(TokenClaims.NationId, nationId));
        if (rulerName != null) claims.Add(new Claim(TokenClaims.RulerName, rulerName));
        if (uniqueCode != null) claims.Add(new Claim(TokenClaims.UniqueCode, uniqueCode));
        // Same name and role claim types as the API's JWT configuration
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer", TokenClaims.Name, TokenClaims.AdminRole));
    }

    [Fact]
    public async Task FailsWhenThereIsNoIdentity()
    {
        var context = CreateContext(principal: null);

        await new CustomJwtBearerEvents().TokenValidated(context);

        Assert.Equal("Invalid token", context.Result?.Failure?.Message);
    }

    [Fact]
    public async Task FailsWhenTheUniqueCodeClaimIsMissing()
    {
        var context = CreateContext(Principal("123", "Bob", uniqueCode: null));

        await new CustomJwtBearerEvents().TokenValidated(context);

        Assert.Equal("Invalid token: missing unique code", context.Result?.Failure?.Message);
        Assert.Empty(_repository.ReceivedCalls());
    }

    [Fact]
    public async Task FailsWhenNeitherNationIdNorRulerNameIsPresent()
    {
        var context = CreateContext(Principal("", "", "ABCDE"));

        await new CustomJwtBearerEvents().TokenValidated(context);

        Assert.Equal("User not authorized", context.Result?.Failure?.Message);
        Assert.Empty(_repository.ReceivedCalls());
    }

    [Fact]
    public async Task FailsWhenTheAccountNoLongerExists()
    {
        _repository.Authorize(123, "ABCDE").Returns((AuthorizedAccount)null!);
        var context = CreateContext(Principal("123", "Bob", "ABCDE"));

        await new CustomJwtBearerEvents().TokenValidated(context);

        Assert.Equal("User not authorized", context.Result?.Failure?.Message);
    }

    [Fact]
    public async Task FailsWhenTheDatabaseThrows()
    {
        _repository.Authorize(123, "ABCDE").ThrowsAsync(new InvalidOperationException("db down"));
        var context = CreateContext(Principal("123", "Bob", "ABCDE"));

        await new CustomJwtBearerEvents().TokenValidated(context);

        Assert.Equal("Authentication failed", context.Result?.Failure?.Message);
    }

    [Fact]
    public async Task AddsTheAccountsClaims_WhenAuthorizedByNationId()
    {
        _repository.Authorize(123, "ABCDE").Returns(new AuthorizedAccount
        {
            AccountId = 42,
            RulerName = "Bob",
            Role = 'S',
            Roles = [new AccountRole { Id = 1, Name = AdminRoles.SuperAdmin }, new AccountRole { Id = 3, Name = AdminRoles.Lists }]
        });
        var context = CreateContext(Principal("123", "Bob", "ABCDE"));

        await new CustomJwtBearerEvents().TokenValidated(context);

        Assert.Null(context.Result);
        Assert.Equal(42, context.Principal!.GetAccountId());
        Assert.Equal("Bob", context.Principal.Identity!.Name);
        Assert.Equal("S", context.Principal.FindFirst(TokenClaims.VepRole)?.Value);
        Assert.True(context.Principal.IsInRole(AdminRoles.SuperAdmin));
        Assert.True(context.Principal.IsInRole(AdminRoles.Lists));
    }

    [Fact]
    public async Task ReplacesStaleAccountClaimsFromTheToken_WithTheDatabasesCurrentValues()
    {
        _repository.Authorize(123, "ABCDE").Returns(new AuthorizedAccount
        {
            AccountId = 42, RulerName = "Bob the Second", Role = 'H', Roles = [new AccountRole { Id = 2, Name = AdminRoles.ReadOnly }]
        });
        var context = CreateContext(Principal("123", "Bob", "ABCDE",
            new Claim(TokenClaims.AccountId, "42"),
            new Claim(TokenClaims.Name, "Bob"),
            new Claim(TokenClaims.VepRole, "B"),
            new Claim(TokenClaims.AdminRole, AdminRoles.SuperAdmin)));

        await new CustomJwtBearerEvents().TokenValidated(context);

        var principal = context.Principal!;
        Assert.Equal(["42"], principal.FindAll(TokenClaims.AccountId).Select(c => c.Value));
        Assert.Equal(["Bob the Second"], principal.FindAll(TokenClaims.Name).Select(c => c.Value));
        Assert.Equal(["H"], principal.FindAll(TokenClaims.VepRole).Select(c => c.Value));
        Assert.Equal([AdminRoles.ReadOnly], principal.FindAll(TokenClaims.AdminRole).Select(c => c.Value));
        Assert.False(principal.IsInRole(AdminRoles.SuperAdmin));
        // Credentials are left alone
        Assert.Equal("ABCDE", principal.FindFirst(TokenClaims.UniqueCode)?.Value);
    }

    [Fact]
    public async Task AuthorizesByRulerName_WhenNationIdIsNotNumeric_AndToleratesNullRoles()
    {
        _repository.Authorize("Bob", "ABCDE").Returns(new AuthorizedAccount { AccountId = 42, Roles = null! });
        var context = CreateContext(Principal("", "Bob", "ABCDE"));

        await new CustomJwtBearerEvents().TokenValidated(context);

        Assert.Null(context.Result);
        Assert.Equal(42, context.Principal!.GetAccountId());
        Assert.Empty(context.Principal.FindAll(TokenClaims.AdminRole));
    }
}
