using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Valyria.Api.Auth;
using Valyria.Api.Controllers;
using Valyria.Core.DTOs;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Exceptions;
using static Valyria.UnitTests.TestHelpers;

namespace Valyria.UnitTests.Api.Controllers;

public class AccountsControllerTests
{
    private readonly IAccountsRepository _repository = Substitute.For<IAccountsRepository>();
    private readonly AccountsController _controller;

    public AccountsControllerTests()
    {
        _controller = CreateController(accountId: 5);
    }

    private AccountsController CreateController(int accountId, params string[] adminRoles) =>
        new AccountsController(_repository, Substitute.For<ITokenProvider>(), NullLogger<AccountsController>.Instance)
            .WithUser([new Claim(TokenClaims.AccountId, accountId.ToString()), .. adminRoles.Select(role => new Claim(TokenClaims.AdminRole, role))]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAllAccounts_PassesIncludeOnHoldThrough(bool includeOnHold)
    {
        var accounts = new[] { new AccountDetail { Id = 1 }, new AccountDetail { Id = 2 } };
        _repository.GetAccountDetailsAsync(includeOnHold).Returns(accounts);

        var result = await _controller.GetAllAccounts(includeOnHold);

        Assert.Same(accounts, AssertOk<IEnumerable<AccountDetail>>(result));
    }

    [Fact]
    public async Task GetAllAccounts_Returns500_OnError()
    {
        _repository.GetAccountDetailsAsync(Arg.Any<bool>()).ThrowsAsync(new Exception("boom"));

        AssertServerError(await _controller.GetAllAccounts(), "boom");
    }

    [Fact]
    public async Task GetAccount_ReturnsTheAccount()
    {
        var account = new AccountDetail { Id = 5 };
        _repository.GetAccountByIdAsync(5).Returns(account);

        Assert.Same(account, AssertOk<AccountDetail>(await _controller.GetAccount(5)));
    }

    [Fact]
    public async Task GetAccount_Returns404_WhenMissing()
    {
        _repository.GetAccountByIdAsync(5).Returns((AccountDetail)null!);

        Assert.IsType<NotFoundObjectResult>(await _controller.GetAccount(5));
    }

    [Fact]
    public async Task GetAccount_ForbidsOtherMembers()
    {
        Assert.IsType<ForbidResult>(await CreateController(accountId: 6).GetAccount(5));
        Assert.Empty(_repository.ReceivedCalls());
    }

    [Fact]
    public async Task GetAccount_AllowsAdminPortalViewers()
    {
        _repository.GetAccountByIdAsync(5).Returns(new AccountDetail { Id = 5 });

        Assert.IsType<OkObjectResult>(await CreateController(1, AdminRoles.ReadOnly).GetAccount(5));
    }

    [Fact]
    public async Task GetAccount_Returns500_OnError()
    {
        _repository.GetAccountByIdAsync(5).ThrowsAsync(new Exception("boom"));

        AssertServerError(await _controller.GetAccount(5), "boom");
    }

    [Fact]
    public async Task CreateAccount_ReturnsTheCreatedAccount()
    {
        var request = new AccountCreateRequest { NationId = 123 };
        var created = new AccountDetail { Id = 9, NationId = 123 };
        _repository.CreateAccountAsync(request).Returns(created);

        Assert.Same(created, AssertOk<AccountDetail>(await _controller.CreateAccount(request)));
    }

    [Fact]
    public async Task CreateAccount_Returns404_WhenTheNationIsNotFound()
    {
        _repository.CreateAccountAsync(Arg.Any<AccountCreateRequest>()).ThrowsAsync(new NotFoundException("no nation"));

        var notFound = Assert.IsType<NotFoundObjectResult>(await _controller.CreateAccount(new AccountCreateRequest()));
        Assert.Equal("no nation", notFound.Value);
    }

    [Fact]
    public async Task CreateAccount_Returns500_OnError()
    {
        _repository.CreateAccountAsync(Arg.Any<AccountCreateRequest>()).ThrowsAsync(new Exception("boom"));

        AssertServerError(await _controller.CreateAccount(new AccountCreateRequest()), "boom");
    }

    [Fact]
    public async Task UpdateAccount_ReturnsOk()
    {
        var request = new AccountUpdateRequest { Id = 3 };

        Assert.IsType<OkResult>(await _controller.UpdateAccount(request));
        await _repository.Received(1).UpdateAccountAsync(request);
    }

    [Fact]
    public async Task UpdateAccount_Returns404_WhenTheAccountIsNotFound()
    {
        _repository.UpdateAccountAsync(Arg.Any<AccountUpdateRequest>()).ThrowsAsync(new NotFoundException("missing"));

        Assert.IsType<NotFoundObjectResult>(await _controller.UpdateAccount(new AccountUpdateRequest()));
    }

    [Fact]
    public async Task UpdateAccount_Returns500_OnError()
    {
        _repository.UpdateAccountAsync(Arg.Any<AccountUpdateRequest>()).ThrowsAsync(new Exception("boom"));

        AssertServerError(await _controller.UpdateAccount(new AccountUpdateRequest()), "boom");
    }

    [Fact]
    public async Task RemoveAccount_ReturnsOk()
    {
        Assert.IsType<OkResult>(await _controller.RemoveAccount(3));
        await _repository.Received(1).RemoveAccountAsync(3);
    }

    [Fact]
    public async Task RemoveAccount_Returns404_WhenTheAccountIsNotFound()
    {
        _repository.RemoveAccountAsync(3).ThrowsAsync(new NotFoundException("missing"));

        Assert.IsType<NotFoundObjectResult>(await _controller.RemoveAccount(3));
    }

    [Fact]
    public async Task RemoveAccount_Returns500_OnError()
    {
        _repository.RemoveAccountAsync(3).ThrowsAsync(new Exception("boom"));

        AssertServerError(await _controller.RemoveAccount(3), "boom");
    }

    [Fact]
    public async Task FindProspectAccount_ReturnsTheProspect()
    {
        var prospect = new ProspectAccount { NationId = 123 };
        _repository.FindProspectAccountAsync(123).Returns(prospect);

        Assert.Same(prospect, AssertOk<ProspectAccount>(await _controller.FindProspectAccount(123)));
    }

    [Fact]
    public async Task FindProspectAccount_Returns404_WhenNoNationMatches()
    {
        _repository.FindProspectAccountAsync(123).Returns((ProspectAccount)null!);

        Assert.IsType<NotFoundObjectResult>(await _controller.FindProspectAccount(123));
    }

    [Fact]
    public async Task FindProspectAccount_Returns409_WhenTheNationAlreadyHasAnAccount()
    {
        _repository.FindProspectAccountAsync(123).ThrowsAsync(new AlreadyExistsException("exists"));

        var conflict = Assert.IsType<ConflictObjectResult>(await _controller.FindProspectAccount(123));
        Assert.Equal("exists", conflict.Value);
    }

    [Fact]
    public async Task FindProspectAccount_Returns500_OnError()
    {
        _repository.FindProspectAccountAsync(123).ThrowsAsync(new Exception("boom"));

        AssertServerError(await _controller.FindProspectAccount(123), "boom");
    }

    [Fact]
    public async Task GetTopScorers_ReturnsTheScorers()
    {
        var scorers = new[] { new TopScorerAccount { Id = 1, Rank = 1 } };
        _repository.GetTopScorerAccountsAsync().Returns(scorers);

        Assert.Same(scorers, AssertOk<IEnumerable<TopScorerAccount>>(await _controller.GetTopScorers()));
    }

    [Fact]
    public async Task GetTopScorers_Returns500_OnError()
    {
        _repository.GetTopScorerAccountsAsync().ThrowsAsync(new Exception("boom"));

        AssertServerError(await _controller.GetTopScorers(), "boom");
    }
}
