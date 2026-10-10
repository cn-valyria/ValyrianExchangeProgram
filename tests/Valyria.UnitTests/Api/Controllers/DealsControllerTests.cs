using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Valyria.Api.Controllers;
using Valyria.Core.DTOs;
using Valyria.Core.Entities;
using Valyria.Core.Interfaces;
using static Valyria.UnitTests.TestHelpers;

namespace Valyria.UnitTests.Api.Controllers;

public class DealsControllerTests
{
    private readonly IDealsRepository _repository = Substitute.For<IDealsRepository>();

    private DealsController CreateController(string? userName = "admin")
    {
        var controller = new DealsController(_repository, NullLogger<DealsController>.Instance);
        return userName == null ? controller.WithUser() : controller.WithUser(new Claim(ClaimTypes.Name, userName));
    }

    [Fact]
    public async Task GetAccountDeals_ReturnsTheSummary()
    {
        var summary = new AccountDealsSummary { ActiveDeals = [new DealDetail { Id = 1 }] };
        _repository.GetAccountDealsAsync(7).Returns(summary);

        Assert.Same(summary, AssertOk<AccountDealsSummary>(await CreateController().GetAccountDeals(7)));
    }

    [Fact]
    public async Task GetAccountDeals_Returns500_OnError()
    {
        _repository.GetAccountDealsAsync(7).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().GetAccountDeals(7), "boom");
    }

    [Fact]
    public async Task GetAccountDealHistory_DefaultsToTheFirstPageOfTen()
    {
        var page = new DealHistoryPage { TotalCount = 0 };
        _repository.GetAccountDealHistoryAsync(7, 10, 0).Returns(page);

        Assert.Same(page, AssertOk<DealHistoryPage>(await CreateController().GetAccountDealHistory(7, null, null)));
    }

    [Fact]
    public async Task GetAccountDealHistory_PassesPagingThrough()
    {
        var page = new DealHistoryPage { TotalCount = 50 };
        _repository.GetAccountDealHistoryAsync(7, 5, 20).Returns(page);

        Assert.Same(page, AssertOk<DealHistoryPage>(await CreateController().GetAccountDealHistory(7, 5, 20)));
    }

    [Fact]
    public async Task GetAccountDealHistory_Returns500_OnError()
    {
        _repository.GetAccountDealHistoryAsync(7, Arg.Any<int>(), Arg.Any<int>()).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().GetAccountDealHistory(7, null, null), "boom");
    }

    [Fact]
    public async Task GetDealActivity_ReturnsTheActivity()
    {
        var activity = new List<DealActivityDetail> { new() { Id = 1 } };
        _repository.GetDealActivityAsync(3, 7).Returns(activity);

        Assert.Same(activity, AssertOk<List<DealActivityDetail>>(await CreateController().GetDealActivity(3, 7)));
    }

    [Fact]
    public async Task GetDealActivity_Returns404_WhenTheAccountIsNotPartOfTheDeal()
    {
        _repository.GetDealActivityAsync(3, 7).Returns((List<DealActivityDetail>)null!);

        Assert.IsType<NotFoundObjectResult>(await CreateController().GetDealActivity(3, 7));
    }

    [Fact]
    public async Task GetDealActivity_Returns500_OnError()
    {
        _repository.GetDealActivityAsync(3, 7).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().GetDealActivity(3, 7), "boom");
    }

    [Fact]
    public async Task GetDeal_ReturnsTheDeal()
    {
        var deal = new Deal { Id = 3 };
        _repository.GetDealByIdAsync(3).Returns(deal);

        Assert.Same(deal, AssertOk<Deal>(await CreateController().GetDeal(3)));
    }

    [Fact]
    public async Task GetDeal_Returns404_WhenMissing()
    {
        _repository.GetDealByIdAsync(3).Returns((Deal)null!);

        Assert.IsType<NotFoundObjectResult>(await CreateController().GetDeal(3));
    }

    [Fact]
    public async Task GetDeal_Returns500_OnError()
    {
        _repository.GetDealByIdAsync(3).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().GetDeal(3), "boom");
    }

    [Fact]
    public async Task CreateDeal_StampsTheSignedInUser_AndReturnsCreatedAtGetDeal()
    {
        _repository.CreateDealAsync(Arg.Any<CreateDealRequest>()).Returns(55);
        var request = new CreateDealRequest { Slot1Id = 1, Slot2Id = 2, CreatedBy = "spoofed" };

        var created = Assert.IsType<CreatedAtActionResult>(await CreateController("admin").CreateDeal(request));

        Assert.Equal(nameof(DealsController.GetDeal), created.ActionName);
        Assert.Equal(55, created.RouteValues!["id"]);
        Assert.Equal(55, Prop(created.Value, "id"));
        await _repository.Received(1).CreateDealAsync(Arg.Is<CreateDealRequest>(r => r.CreatedBy == "admin"));
    }

    [Fact]
    public async Task CreateDeal_FallsBackToUnknown_WhenTheUserHasNoName()
    {
        _repository.CreateDealAsync(Arg.Any<CreateDealRequest>()).Returns(55);

        await CreateController(userName: null).CreateDeal(new CreateDealRequest { Slot1Id = 1, Slot2Id = 2 });

        await _repository.Received(1).CreateDealAsync(Arg.Is<CreateDealRequest>(r => r.CreatedBy == "unknown"));
    }

    [Fact]
    public async Task CreateDeal_Returns500_OnError()
    {
        _repository.CreateDealAsync(Arg.Any<CreateDealRequest>()).ThrowsAsync(new Exception("duplicate"));

        AssertServerError(await CreateController().CreateDeal(new CreateDealRequest()), "duplicate");
    }

    [Fact]
    public async Task CancelDeal_CancelsAsTheSignedInUser()
    {
        Assert.IsType<NoContentResult>(await CreateController("admin").CancelDeal(3, new CancelDealRequest { Reason = "inactive" }));

        await _repository.Received(1).CancelDealAsync(3, "inactive", "admin");
    }

    [Fact]
    public async Task CancelDeal_Returns500_OnError()
    {
        _repository.CancelDealAsync(3, Arg.Any<string>(), Arg.Any<string>()).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().CancelDeal(3, new CancelDealRequest { Reason = "x" }), "boom");
    }

    [Fact]
    public async Task GetActiveDeals_DefaultsToTheFirstPageOf25()
    {
        var page = new AdminDealPage();
        _repository.GetActiveDealsAsync(null, 25, 0).Returns(page);

        Assert.Same(page, AssertOk<AdminDealPage>(await CreateController().GetActiveDeals(null, null, null)));
    }

    [Fact]
    public async Task GetActiveDeals_PassesSearchAndPagingThrough()
    {
        var page = new AdminDealPage();
        _repository.GetActiveDealsAsync("bob", 10, 30).Returns(page);

        Assert.Same(page, AssertOk<AdminDealPage>(await CreateController().GetActiveDeals("bob", 10, 30)));
    }

    [Fact]
    public async Task GetActiveDeals_Returns500_OnError()
    {
        _repository.GetActiveDealsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>()).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().GetActiveDeals(null, null, null), "boom");
    }

    [Fact]
    public async Task SearchAidActivity_DefaultsToTheFirstPageOf25()
    {
        var filters = new AidActivityFilters { Search = "bob" };
        var page = new AidActivityPage();
        _repository.SearchAidActivityAsync(filters, 25, 0).Returns(page);

        Assert.Same(page, AssertOk<AidActivityPage>(await CreateController().SearchAidActivity(filters, null, null)));
    }

    [Fact]
    public async Task SearchAidActivity_Returns500_OnError()
    {
        _repository.SearchAidActivityAsync(Arg.Any<AidActivityFilters>(), Arg.Any<int>(), Arg.Any<int>()).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().SearchAidActivity(new AidActivityFilters(), 5, 5), "boom");
    }

    [Fact]
    public async Task GetWaitQueue_ReturnsTheQueue()
    {
        var queue = new List<WaitQueueEntry> { new() { AccountId = 1 } };
        _repository.GetWaitQueueAsync().Returns(queue);

        Assert.Same(queue, AssertOk<List<WaitQueueEntry>>(await CreateController().GetWaitQueue()));
    }

    [Fact]
    public async Task GetWaitQueue_Returns500_OnError()
    {
        _repository.GetWaitQueueAsync().ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().GetWaitQueue(), "boom");
    }

    [Fact]
    public async Task GetFlaggedDeals_ReturnsTheFlags()
    {
        var flags = new List<DealFlagDetail> { new() { Id = 1 } };
        _repository.GetFlaggedDealsAsync().Returns(flags);

        Assert.Same(flags, AssertOk<List<DealFlagDetail>>(await CreateController().GetFlaggedDeals()));
    }

    [Fact]
    public async Task GetFlaggedDeals_Returns500_OnError()
    {
        _repository.GetFlaggedDealsAsync().ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().GetFlaggedDeals(), "boom");
    }

    [Fact]
    public async Task ResolveDealFlag_ResolvesAsTheSignedInUser()
    {
        Assert.IsType<NoContentResult>(await CreateController("admin").ResolveDealFlag(9, new ResolveFlagRequest { AdminNotes = "ok" }));

        await _repository.Received(1).ResolveDealFlagAsync(9, "admin", "ok");
    }

    [Fact]
    public async Task ResolveDealFlag_Returns500_OnError()
    {
        _repository.ResolveDealFlagAsync(9, Arg.Any<string>(), Arg.Any<string>()).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().ResolveDealFlag(9, new ResolveFlagRequest()), "boom");
    }
}
