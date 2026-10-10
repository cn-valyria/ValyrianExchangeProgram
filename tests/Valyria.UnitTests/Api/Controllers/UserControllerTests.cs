using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Valyria.Api.Auth;
using Valyria.Api.Controllers;
using Valyria.Core.Contracts;
using Valyria.Core.DTOs;
using static Valyria.UnitTests.TestHelpers;

namespace Valyria.UnitTests.Api.Controllers;

public class UserControllerTests
{
    private readonly ITokenProvider _tokenProvider = Substitute.For<ITokenProvider>();
    private readonly IAuthenticationProvider _authenticationProvider = Substitute.For<IAuthenticationProvider>();
    private readonly AuthorizedAccount _account = new() { AccountId = 42, NationId = 1, RulerName = "Bob", Role = 'B', Roles = [] };

    public UserControllerTests()
    {
        _tokenProvider.GenerateAccessToken(Arg.Any<AuthorizeUserRequest>(), Arg.Any<AuthorizedAccount>()).Returns("access");
        _tokenProvider.GenerateRefreshToken(Arg.Any<AuthorizeUserRequest>(), Arg.Any<AuthorizedAccount>()).Returns("refresh");
    }

    private UserController CreateController(params Claim[] claims) =>
        new UserController(_tokenProvider, _authenticationProvider, NullLogger<UserController>.Instance).WithUser(claims);

    private static void AssertError<TResult>(IActionResult result, string error) where TResult : ObjectResult
    {
        var objectResult = Assert.IsType<TResult>(result);
        Assert.Equal(error, Prop(objectResult.Value, "error"));
    }

    private static void AssertTokens(IActionResult result)
    {
        var tokens = AssertOk<TokenResponse>(result);
        Assert.Equal("access", tokens.AccessToken);
        Assert.Equal("refresh", tokens.RefreshToken);
        Assert.Equal("Bearer", tokens.TokenType);
        Assert.Equal(86400, tokens.ExpiresIn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("client_credentials")]
    public async Task Token_RejectsUnsupportedGrantTypes(string grantType)
    {
        AssertError<BadRequestObjectResult>(await CreateController().Token(new TokenRequest { GrantType = grantType }), "unsupported_grant_type");
    }

    [Fact]
    public async Task Token_RejectsNullGrantType()
    {
        AssertError<BadRequestObjectResult>(await CreateController().Token(new TokenRequest { GrantType = null! }), "unsupported_grant_type");
    }

    [Fact]
    public async Task PasswordGrant_RequiresAPassword()
    {
        var result = await CreateController().Token(new TokenRequest { GrantType = "password", NationId = "1" });

        AssertError<BadRequestObjectResult>(result, "invalid_request");
    }

    [Fact]
    public async Task PasswordGrant_RequiresNationIdOrRulerName()
    {
        var result = await CreateController().Token(new TokenRequest { GrantType = "password", Password = "ABCDE" });

        AssertError<BadRequestObjectResult>(result, "invalid_request");
    }

    [Fact]
    public async Task PasswordGrant_Returns401_ForBadCredentials()
    {
        _authenticationProvider.GetAuthorizedAccount(Arg.Any<AuthorizeUserRequest>()).Returns((AuthorizedAccount)null!);

        var result = await CreateController().Token(new TokenRequest { GrantType = "password", NationId = "1", Password = "nope" });

        AssertError<UnauthorizedObjectResult>(result, "invalid_grant");
    }

    [Fact]
    public async Task PasswordGrant_IssuesTokensForTheAuthorizedAccount_AndIsCaseInsensitive()
    {
        _authenticationProvider.GetAuthorizedAccount(Arg.Is<AuthorizeUserRequest>(r => r.RulerName == "Bob" && r.UniqueCode == "ABCDE")).Returns(_account);

        var result = await CreateController().Token(new TokenRequest { GrantType = "PASSWORD", RulerName = "Bob", Password = "ABCDE" });

        AssertTokens(result);
        _tokenProvider.Received(1).GenerateAccessToken(Arg.Is<AuthorizeUserRequest>(r => r.UniqueCode == "ABCDE"), _account);
        _tokenProvider.Received(1).GenerateRefreshToken(Arg.Is<AuthorizeUserRequest>(r => r.UniqueCode == "ABCDE"), _account);
    }

    [Fact]
    public async Task Token_Returns500_WhenAuthenticationThrows()
    {
        _authenticationProvider.GetAuthorizedAccount(Arg.Any<AuthorizeUserRequest>()).ThrowsAsync(new Exception("db down"));

        var result = await CreateController().Token(new TokenRequest { GrantType = "password", NationId = "1", Password = "ABCDE" });

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.Equal("server_error", Prop(error.Value, "error"));
    }

    [Fact]
    public async Task RefreshGrant_RequiresARefreshToken()
    {
        AssertError<BadRequestObjectResult>(await CreateController().Token(new TokenRequest { GrantType = "refresh_token" }), "invalid_request");
    }

    [Fact]
    public async Task RefreshGrant_Returns401_ForInvalidRefreshToken()
    {
        _tokenProvider.ReadToken("expired").Returns((AuthorizeUserRequest)null!);

        var result = await CreateController().Token(new TokenRequest { GrantType = "refresh_token", RefreshToken = "expired" });

        AssertError<UnauthorizedObjectResult>(result, "invalid_grant");
    }

    [Fact]
    public async Task RefreshGrant_Returns401_WhenTheAccountNoLongerExists()
    {
        var account = new AuthorizeUserRequest { NationId = "1", UniqueCode = "ABCDE" };
        _tokenProvider.ReadToken("refresh-in").Returns(account);
        _authenticationProvider.GetAuthorizedAccount(account).Returns((AuthorizedAccount)null!);

        var result = await CreateController().Token(new TokenRequest { GrantType = "refresh_token", RefreshToken = "refresh-in" });

        AssertError<UnauthorizedObjectResult>(result, "invalid_grant");
    }

    [Fact]
    public async Task RefreshGrant_IssuesNewTokens_WithTheAccountsCurrentDetails()
    {
        var credentials = new AuthorizeUserRequest { NationId = "1", UniqueCode = "ABCDE" };
        _tokenProvider.ReadToken("refresh-in").Returns(credentials);
        _authenticationProvider.GetAuthorizedAccount(credentials).Returns(_account);

        AssertTokens(await CreateController().Token(new TokenRequest { GrantType = "refresh_token", RefreshToken = "refresh-in" }));
        _tokenProvider.Received(1).GenerateAccessToken(credentials, _account);
    }

    [Fact]
    public async Task Authenticate_IsALegacyAliasForThePasswordGrant()
    {
        _authenticationProvider.GetAuthorizedAccount(Arg.Is<AuthorizeUserRequest>(r => r.NationId == "1" && r.UniqueCode == "ABCDE")).Returns(_account);

        var result = await CreateController().Authenticate(new AuthorizeUserRequest { NationId = "1", RulerName = "Bob", UniqueCode = "ABCDE" });

        AssertTokens(result);
    }

    [Fact]
    public async Task RefreshToken_IsALegacyAliasForTheRefreshGrant()
    {
        var account = new AuthorizeUserRequest { NationId = "1", UniqueCode = "ABCDE" };
        _tokenProvider.ReadToken("refresh-in").Returns(account);
        _authenticationProvider.GetAuthorizedAccount(account).Returns(_account);

        AssertTokens(await CreateController().RefreshToken(new Dictionary<string, string> { ["refresh_token"] = "refresh-in" }));
    }

    [Fact]
    public async Task RefreshToken_RequiresTheRefreshTokenKey()
    {
        AssertError<BadRequestObjectResult>(await CreateController().RefreshToken(new Dictionary<string, string>()), "invalid_request");
    }

    [Fact]
    public async Task GetAuthorizedAccount_UsesTheTokenClaims()
    {
        var account = new AuthorizedAccount { AccountId = 42 };
        _authenticationProvider.GetAuthorizedAccount(Arg.Is<AuthorizeUserRequest>(r =>
            r.NationId == "1" && r.RulerName == "Bob" && r.UniqueCode == "ABCDE")).Returns(account);

        var result = await CreateController(
            new Claim(TokenClaims.AccountId, "42"),
            new Claim(TokenClaims.NationId, "1"),
            new Claim(TokenClaims.RulerName, "Bob"),
            new Claim(TokenClaims.UniqueCode, "ABCDE")).GetAuthorizedAccount();

        Assert.Same(account, AssertOk<AuthorizedAccount>(result));
    }

    [Fact]
    public async Task GetAuthorizedAccount_Returns401_WhenTheUniqueCodeClaimIsMissing()
    {
        var result = await CreateController(new Claim(TokenClaims.AccountId, "42"), new Claim(TokenClaims.NationId, "1")).GetAuthorizedAccount();

        AssertError<UnauthorizedObjectResult>(result, "invalid_token");
    }

    [Fact]
    public async Task GetAuthorizedAccount_Returns401_WhenTheAccountIsGone()
    {
        _authenticationProvider.GetAuthorizedAccount(Arg.Any<AuthorizeUserRequest>()).Returns((AuthorizedAccount)null!);

        var result = await CreateController(new Claim(TokenClaims.AccountId, "42"), new Claim(TokenClaims.UniqueCode, "ABCDE")).GetAuthorizedAccount();

        AssertError<UnauthorizedObjectResult>(result, "invalid_token");
    }

    [Fact]
    public async Task GetAuthorizedAccount_FallsBackToReadingTheBearerToken_WhenThereIsNoAccountIdClaim()
    {
        var controller = CreateController();
        controller.Request.Headers.Authorization = "Bearer the-token";
        var tokenAccount = new AuthorizeUserRequest { NationId = "1", UniqueCode = "ABCDE" };
        var account = new AuthorizedAccount { AccountId = 42 };
        _tokenProvider.ReadToken("the-token").Returns(tokenAccount);
        _authenticationProvider.GetAuthorizedAccount(tokenAccount).Returns(account);

        Assert.Same(account, AssertOk<AuthorizedAccount>(await controller.GetAuthorizedAccount()));
    }

    [Fact]
    public async Task GetAuthorizedAccount_Fallback_Returns401_ForAnInvalidToken()
    {
        var controller = CreateController();
        controller.Request.Headers.Authorization = "the-token";
        _tokenProvider.ReadToken("the-token").Returns((AuthorizeUserRequest)null!);

        AssertError<UnauthorizedObjectResult>(await controller.GetAuthorizedAccount(), "invalid_token");
    }

    [Fact]
    public async Task GetAuthorizedAccount_Fallback_Returns401_WhenTheAccountIsGone()
    {
        var controller = CreateController(new Claim(TokenClaims.AccountId, "not-a-number"));
        controller.Request.Headers.Authorization = "Bearer the-token";
        _tokenProvider.ReadToken("the-token").Returns(new AuthorizeUserRequest());
        _authenticationProvider.GetAuthorizedAccount(Arg.Any<AuthorizeUserRequest>()).Returns((AuthorizedAccount)null!);

        AssertError<UnauthorizedObjectResult>(await controller.GetAuthorizedAccount(), "invalid_token");
    }

    [Fact]
    public async Task GetAuthorizedAccount_Returns500_OnError()
    {
        _authenticationProvider.GetAuthorizedAccount(Arg.Any<AuthorizeUserRequest>()).ThrowsAsync(new Exception("db down"));

        var result = await CreateController(new Claim(TokenClaims.AccountId, "42"), new Claim(TokenClaims.UniqueCode, "ABCDE")).GetAuthorizedAccount();

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
    }
}
