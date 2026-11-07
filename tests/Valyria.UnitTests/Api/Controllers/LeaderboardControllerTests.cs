using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Valyria.Api.Auth;
using Valyria.Api.Controllers;
using Valyria.Core.DTOs;
using Valyria.Core.Interfaces;
using static Valyria.UnitTests.TestHelpers;

namespace Valyria.UnitTests.Api.Controllers;

public class LeaderboardControllerTests
{
    private readonly IAccountsRepository _repository = Substitute.For<IAccountsRepository>();

    private LeaderboardController CreateController(params Claim[] claims) =>
        new LeaderboardController(_repository, NullLogger<LeaderboardController>.Instance).WithUser(claims);

    [Fact]
    public async Task GetLeaderboard_ReturnsTheLeaderboard_ForTheSignedInAccount()
    {
        var leaderboard = new Leaderboard { RankedAccounts = 3 };
        _repository.GetLeaderboardAsync(5).Returns(leaderboard);

        var result = await CreateController(new Claim(TokenClaims.AccountId, "5")).GetLeaderboard();

        Assert.Same(leaderboard, AssertOk<Leaderboard>(result));
    }

    [Fact]
    public async Task GetLeaderboard_Returns401_WithoutAnAccount()
    {
        Assert.IsType<UnauthorizedResult>(await CreateController().GetLeaderboard());
        Assert.Empty(_repository.ReceivedCalls());
    }

    [Fact]
    public async Task GetLeaderboard_Returns500_OnError()
    {
        _repository.GetLeaderboardAsync(5).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController(new Claim(TokenClaims.AccountId, "5")).GetLeaderboard(), "boom");
    }
}
