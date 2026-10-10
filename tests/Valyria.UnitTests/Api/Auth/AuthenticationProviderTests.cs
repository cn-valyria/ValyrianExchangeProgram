using NSubstitute;
using Valyria.Api.Auth;
using Valyria.Core.Contracts;
using Valyria.Core.DTOs;
using Valyria.Core.Interfaces;

namespace Valyria.UnitTests.Api.Auth;

public class AuthenticationProviderTests
{
    private readonly IAuthorizationRepository _repository = Substitute.For<IAuthorizationRepository>();
    private readonly AuthenticationProvider _provider;

    public AuthenticationProviderTests() => _provider = new AuthenticationProvider(_repository);

    public static TheoryData<AuthorizeUserRequest> InvalidRequests => new()
    {
        new AuthorizeUserRequest { NationId = "1", RulerName = "Bob", UniqueCode = null },
        new AuthorizeUserRequest { NationId = "1", RulerName = "Bob", UniqueCode = "" },
        new AuthorizeUserRequest { NationId = null, RulerName = null, UniqueCode = "ABCDE" },
        new AuthorizeUserRequest { NationId = "", RulerName = "", UniqueCode = "ABCDE" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequests_AreRejected_WithoutHittingTheDatabase(AuthorizeUserRequest request)
    {
        Assert.Null(await _provider.GetAuthorizedAccount(request));

        Assert.Empty(_repository.ReceivedCalls());
    }

    [Fact]
    public async Task NumericNationId_AuthorizesByNationId()
    {
        var account = new AuthorizedAccount { AccountId = 7 };
        _repository.Authorize(123, "ABCDE").Returns(account);

        var result = await _provider.GetAuthorizedAccount(new AuthorizeUserRequest { NationId = "123", RulerName = "Bob", UniqueCode = "ABCDE" });

        Assert.Same(account, result);
        await _repository.DidNotReceive().Authorize(Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    public async Task MissingOrNonNumericNationId_AuthorizesByRulerName(string? nationId)
    {
        var account = new AuthorizedAccount { AccountId = 7 };
        _repository.Authorize("Bob", "ABCDE").Returns(account);

        var result = await _provider.GetAuthorizedAccount(new AuthorizeUserRequest { NationId = nationId, RulerName = "Bob", UniqueCode = "ABCDE" });

        Assert.Same(account, result);
        await _repository.DidNotReceive().Authorize(Arg.Any<int>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReturnsNull_WhenTheRepositoryFindsNothing()
    {
        _repository.Authorize(123, "WRONG").Returns((AuthorizedAccount)null!);

        Assert.Null(await _provider.GetAuthorizedAccount(new AuthorizeUserRequest { NationId = "123", UniqueCode = "WRONG" }));
    }
}
