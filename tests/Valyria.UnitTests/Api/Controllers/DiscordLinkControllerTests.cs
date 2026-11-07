using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Valyria.Api.Auth;
using Valyria.Api.Controllers;
using Valyria.Core.Contracts;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Exceptions;
using static Valyria.UnitTests.TestHelpers;

namespace Valyria.UnitTests.Api.Controllers;

public class DiscordLinkControllerTests
{
    private const long DiscordId = 123456789012345678;
    private const string Token = "link-token";

    private readonly IAccountsRepository _repository = Substitute.For<IAccountsRepository>();
    private readonly IDiscordLinkTokenProvider _tokens = Substitute.For<IDiscordLinkTokenProvider>();
    private readonly DiscordLink _link = new(DiscordId, "bob_", new DateTime(2026, 11, 7, 16, 15, 0, DateTimeKind.Utc));

    public DiscordLinkControllerTests()
    {
        _tokens.ReadToken(Token).Returns(_link);
    }

    private DiscordLinkController CreateController(int? accountId = 5) =>
        new DiscordLinkController(_repository, _tokens, NullLogger<DiscordLinkController>.Instance)
            .WithUser(accountId is null ? [new Claim(TokenClaims.Name, "Bob")] : [new Claim(TokenClaims.AccountId, accountId.ToString()!)]);

    [Fact]
    public void Preview_ReturnsTheDiscordAccountInTheToken_WithTheIdAsAString()
    {
        var details = AssertOk<DiscordLinkDetails>(CreateController().Preview(new DiscordLinkRequest { Token = Token }));

        Assert.Equal("123456789012345678", details.DiscordId);
        Assert.Equal("bob_", details.DiscordUsername);
        Assert.Equal(_link.ExpiresAt, details.ExpiresAt);
    }

    [Fact]
    public async Task PreviewAndLink_Return400_ForAnInvalidOrExpiredToken()
    {
        var request = new DiscordLinkRequest { Token = "bad" };

        Assert.IsType<BadRequestObjectResult>(CreateController().Preview(request));
        Assert.IsType<BadRequestObjectResult>(await CreateController().Link(request));
        await _repository.DidNotReceiveWithAnyArgs().LinkDiscordAccountAsync(default, default, default!);
    }

    [Fact]
    public async Task Link_LinksTheTokensDiscordAccount_ToTheSignedInMembersAccount()
    {
        var details = AssertOk<DiscordLinkDetails>(await CreateController(accountId: 5).Link(new DiscordLinkRequest { Token = Token }));

        await _repository.Received(1).LinkDiscordAccountAsync(5, DiscordId, "bob_");
        Assert.Equal("123456789012345678", details.DiscordId);
    }

    [Fact]
    public async Task Link_Forbids_WithoutAnAccountId()
    {
        Assert.IsType<ForbidResult>(await CreateController(accountId: null).Link(new DiscordLinkRequest { Token = Token }));
        await _repository.DidNotReceiveWithAnyArgs().LinkDiscordAccountAsync(default, default, default!);
    }

    [Fact]
    public async Task Link_Returns404_WhenTheAccountIsNotFound()
    {
        _repository.LinkDiscordAccountAsync(5, DiscordId, "bob_").ThrowsAsync(new NotFoundException("missing"));

        Assert.IsType<NotFoundObjectResult>(await CreateController().Link(new DiscordLinkRequest { Token = Token }));
    }

    [Fact]
    public async Task Link_Returns500_OnError()
    {
        _repository.LinkDiscordAccountAsync(5, DiscordId, "bob_").ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().Link(new DiscordLinkRequest { Token = Token }), "boom");
    }
}
