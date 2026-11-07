using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Microsoft.AspNetCore.Mvc;
using Valyria.Api.Auth;
using Valyria.Api.Controllers;
using Valyria.Core.Contracts;
using Valyria.Core.DTOs;
using Valyria.Core.Interfaces;
using static Valyria.UnitTests.TestHelpers;

namespace Valyria.UnitTests.Api.Controllers;

public class DiscordControllerTests
{
    private const long DiscordId = 123456789012345678;

    private readonly IAccountsRepository _accountsRepository = Substitute.For<IAccountsRepository>();
    private readonly IDiscordLinkTokenProvider _linkTokenProvider = Substitute.For<IDiscordLinkTokenProvider>();

    private DiscordController CreateController() =>
        new DiscordController(_accountsRepository, _linkTokenProvider, NullLogger<DiscordController>.Instance).WithUser();

    [Fact]
    public async Task GetAccountsByDiscordId_ReturnsTheLinkedAccounts()
    {
        var accounts = new[]
        {
            new DiscordAccount { NationId = 1, NationName = "Bobland", RulerName = "Bob", UniqueCode = "ABCDE" },
            new DiscordAccount { NationId = 2, UniqueCode = "QWERT" }
        };
        _accountsRepository.GetAccountsByDiscordIdAsync(DiscordId).Returns(accounts);

        var result = AssertOk<IEnumerable<DiscordAccount>>(await CreateController().GetAccountsByDiscordId(DiscordId));

        Assert.Equal(accounts, result);
    }

    [Fact]
    public async Task GetAccountsByDiscordId_ReturnsAnEmptyList_ForAnUnregisteredUser()
    {
        _accountsRepository.GetAccountsByDiscordIdAsync(DiscordId).Returns([]);

        Assert.Empty(AssertOk<IEnumerable<DiscordAccount>>(await CreateController().GetAccountsByDiscordId(DiscordId)));
    }

    [Fact]
    public async Task GetAccountsByDiscordId_Returns500_WhenTheLookupFails()
    {
        _accountsRepository.GetAccountsByDiscordIdAsync(DiscordId).ThrowsAsync(new InvalidOperationException("db down"));

        AssertServerError(await CreateController().GetAccountsByDiscordId(DiscordId), "db down");
    }

    [Fact]
    public void CreateLinkToken_IssuesATokenForTheDiscordUser()
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(15);
        _linkTokenProvider.CreateToken(DiscordId, "bob_").Returns(("link-token", expiresAt));

        var response = AssertOk<DiscordLinkTokenResponse>(CreateController().CreateLinkToken(
            new DiscordLinkTokenRequest { DiscordId = "123456789012345678", DiscordUsername = " bob_ " }));

        Assert.Equal("link-token", response.Token);
        Assert.Equal(expiresAt, response.ExpiresAt);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("99999999999999999999")]
    [InlineData("abc")]
    public void CreateLinkToken_Returns400_ForSomethingThatIsntADiscordId(string discordId)
    {
        var result = CreateController().CreateLinkToken(new DiscordLinkTokenRequest { DiscordId = discordId, DiscordUsername = "bob_" });

        Assert.IsType<BadRequestObjectResult>(result);
        _linkTokenProvider.DidNotReceiveWithAnyArgs().CreateToken(default, default!);
    }
}
