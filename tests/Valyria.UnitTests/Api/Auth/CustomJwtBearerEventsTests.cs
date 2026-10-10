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

    private static ClaimsPrincipal Principal(string? nationId, string? rulerName, string? uniqueCode)
    {
        var claims = new List<Claim>();
        if (nationId != null) claims.Add(new Claim(TokenClaims.NationId, nationId));
        if (rulerName != null) claims.Add(new Claim(TokenClaims.RulerName, rulerName));
        if (uniqueCode != null) claims.Add(new Claim(TokenClaims.UniqueCode, uniqueCode));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
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
    public async Task AddsAccountIdAndRoleClaims_WhenAuthorizedByNationId()
    {
        _repository.Authorize(123, "ABCDE").Returns(new AuthorizedAccount
        {
            AccountId = 42,
            Roles = [new AccountRole { Id = 1, Name = "Admin" }, new AccountRole { Id = 2, Name = "Admin - Lists" }]
        });
        var context = CreateContext(Principal("123", "Bob", "ABCDE"));

        await new CustomJwtBearerEvents().TokenValidated(context);

        Assert.Null(context.Result);
        Assert.Equal("42", context.Principal!.FindFirst("AccountId")?.Value);
        Assert.True(context.Principal.IsInRole("Admin"));
        Assert.True(context.Principal.IsInRole("Admin - Lists"));
    }

    [Fact]
    public async Task AuthorizesByRulerName_WhenNationIdIsNotNumeric_AndToleratesNullRoles()
    {
        _repository.Authorize("Bob", "ABCDE").Returns(new AuthorizedAccount { AccountId = 42, Roles = null! });
        var context = CreateContext(Principal("", "Bob", "ABCDE"));

        await new CustomJwtBearerEvents().TokenValidated(context);

        Assert.Null(context.Result);
        Assert.Equal("42", context.Principal!.FindFirst("AccountId")?.Value);
        Assert.Empty(context.Principal.FindAll(ClaimTypes.Role));
    }
}
