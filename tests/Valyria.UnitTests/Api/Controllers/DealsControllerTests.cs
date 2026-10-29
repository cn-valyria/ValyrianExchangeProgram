using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Valyria.Api.Auth;
using Valyria.Api.Controllers;
using Valyria.Core.DTOs;
using Valyria.Core.Entities;
using Valyria.Core.Interfaces;
using static Valyria.UnitTests.TestHelpers;

namespace Valyria.UnitTests.Api.Controllers;

public class DealsControllerTests
{
    private readonly IDealsRepository _repository = Substitute.For<IDealsRepository>();

    /// <summary>
    /// Signed in as member account 7 (ruler Bob) unless told otherwise; a null ruler name means no claims at all
    /// </summary>
    private DealsController CreateController(string? rulerName = "Bob", int accountId = 7, params string[] adminRoles)
    {
        var controller = new DealsController(_repository, NullLogger<DealsController>.Instance);
        if (rulerName == null)
            return controller.WithUser();

        var claims = new List<Claim> { new(TokenClaims.Name, rulerName), new(TokenClaims.AccountId, accountId.ToString()) };
        claims.AddRange(adminRoles.Select(role => new Claim(TokenClaims.AdminRole, role)));
        return controller.WithUser([.. claims]);
    }

    [Fact]
    public async Task AccountEndpoints_ForbidOtherMembers()
    {
        var controller = CreateController(accountId: 8);

        Assert.IsType<ForbidResult>(await controller.GetAccountDeals(7));
        Assert.IsType<ForbidResult>(await controller.GetAccountDealHistory(7, null, null));
        Assert.IsType<ForbidResult>(await controller.GetDealActivity(3, 7));
        Assert.IsType<ForbidResult>(await controller.GetAccountDealFlag(3, 7));
        Assert.Empty(_repository.ReceivedCalls());
    }

    [Theory]
    [InlineData(AdminRoles.SuperAdmin)]
    [InlineData(AdminRoles.ReadOnly)]
    public async Task AccountEndpoints_AllowAdminPortalViewers(string adminRole)
    {
        _repository.GetAccountDealsAsync(7).Returns(new AccountDealsSummary());
        _repository.GetAccountDealHistoryAsync(7, 10, 0).Returns(new DealHistoryPage());
        _repository.GetDealActivityAsync(3, 7).Returns([]);
        _repository.GetAccountDealFlagAsync(3, 7).Returns(new AccountDealFlag { Flag = new DealFlagDetail() });
        var controller = CreateController("Admin", 1, adminRole);

        Assert.IsType<OkObjectResult>(await controller.GetAccountDeals(7));
        Assert.IsType<OkObjectResult>(await controller.GetAccountDealHistory(7, null, null));
        Assert.IsType<OkObjectResult>(await controller.GetDealActivity(3, 7));
        Assert.IsType<OkObjectResult>(await controller.GetAccountDealFlag(3, 7));
    }

    [Fact]
    public async Task AccountEndpoints_ForbidListsAdmins_LikeTheUi()
    {
        Assert.IsType<ForbidResult>(await CreateController("Lister", 1, AdminRoles.Lists).GetAccountDeals(7));
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
    public async Task GetAccountDealFlag_ReturnsTheFlag()
    {
        var flag = new AccountDealFlag { Flag = new DealFlagDetail { Id = 9 }, Evidence = new DealFlagEvidence() };
        _repository.GetAccountDealFlagAsync(3, 7).Returns(flag);

        Assert.Same(flag, AssertOk<AccountDealFlag>(await CreateController().GetAccountDealFlag(3, 7)));
    }

    [Fact]
    public async Task GetAccountDealFlag_Returns404_WhenTheAccountHasNoOpenFlagOnTheDeal()
    {
        _repository.GetAccountDealFlagAsync(3, 7).Returns((AccountDealFlag)null!);

        Assert.IsType<NotFoundObjectResult>(await CreateController().GetAccountDealFlag(3, 7));
    }

    [Fact]
    public async Task GetAccountDealFlag_Returns500_OnError()
    {
        _repository.GetAccountDealFlagAsync(3, 7).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().GetAccountDealFlag(3, 7), "boom");
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
    public async Task CreateDeal_StampsTheSignedInRulerName_AndReturnsCreatedAtGetDeal()
    {
        _repository.CreateDealAsync(Arg.Any<CreateDealRequest>()).Returns(55);
        var request = new CreateDealRequest { Slot1Id = 1, Slot2Id = 2, CreatedBy = "spoofed" };

        var created = Assert.IsType<CreatedAtActionResult>(await CreateController("Bob").CreateDeal(request));

        Assert.Equal(nameof(DealsController.GetDeal), created.ActionName);
        Assert.Equal(55, created.RouteValues!["id"]);
        Assert.Equal(55, Prop(created.Value, "id"));
        await _repository.Received(1).CreateDealAsync(Arg.Is<CreateDealRequest>(r => r.CreatedBy == "Bob"));
    }

    [Fact]
    public async Task CreateDeal_FallsBackToUnknown_WhenThereIsNoNameClaim()
    {
        _repository.CreateDealAsync(Arg.Any<CreateDealRequest>()).Returns(55);

        await CreateController(rulerName: null).CreateDeal(new CreateDealRequest { Slot1Id = 1, Slot2Id = 2 });

        await _repository.Received(1).CreateDealAsync(Arg.Is<CreateDealRequest>(r => r.CreatedBy == "unknown"));
    }

    [Fact]
    public async Task CreateDeal_Returns500_OnError()
    {
        _repository.CreateDealAsync(Arg.Any<CreateDealRequest>()).ThrowsAsync(new Exception("duplicate"));

        AssertServerError(await CreateController().CreateDeal(new CreateDealRequest()), "duplicate");
    }

    [Fact]
    public async Task CancelDeal_CancelsAsTheSignedInRulerName()
    {
        Assert.IsType<NoContentResult>(await CreateController("Bob").CancelDeal(3, new CancelDealRequest { Reason = "inactive" }));

        await _repository.Received(1).CancelDealAsync(3, "inactive", "Bob");
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
    public async Task ResolveDealFlag_ResolvesAsTheSignedInRulerName()
    {
        Assert.IsType<NoContentResult>(await CreateController("Bob").ResolveDealFlag(9, new ResolveFlagRequest { AdminNotes = "ok" }));

        await _repository.Received(1).ResolveDealFlagAsync(9, "Bob", "ok");
    }

    [Fact]
    public async Task ResolveDealFlag_Returns500_OnError()
    {
        _repository.ResolveDealFlagAsync(9, Arg.Any<string>(), Arg.Any<string>()).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().ResolveDealFlag(9, new ResolveFlagRequest()), "boom");
    }

    [Fact]
    public async Task GetDealFlagEvidence_ReturnsTheEvidence()
    {
        var evidence = new DealFlagEvidence { FlaggedSlotsTotal = 4 };
        _repository.GetDealFlagEvidenceAsync(9).Returns(evidence);

        Assert.Same(evidence, AssertOk<DealFlagEvidence>(await CreateController().GetDealFlagEvidence(9)));
    }

    [Fact]
    public async Task GetDealFlagEvidence_Returns404_WhenTheFlagIsMissing()
    {
        _repository.GetDealFlagEvidenceAsync(9).Returns((DealFlagEvidence)null!);

        Assert.IsType<NotFoundObjectResult>(await CreateController().GetDealFlagEvidence(9));
    }

    [Fact]
    public async Task GetDealFlagEvidence_Returns500_OnError()
    {
        _repository.GetDealFlagEvidenceAsync(9).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().GetDealFlagEvidence(9), "boom");
    }

    [Theory]
    [InlineData(DealRestoreResult.Restored, typeof(NoContentResult))]
    [InlineData(DealRestoreResult.FlagNotFound, typeof(NotFoundObjectResult))]
    [InlineData(DealRestoreResult.NotRestorable, typeof(ConflictObjectResult))]
    public async Task RestoreFlaggedDeal_RestoresAsTheSignedInRulerName(DealRestoreResult result, Type expected)
    {
        _repository.RestoreFlaggedDealAsync(9, "Bob", "ok").Returns(result);

        Assert.IsType(expected, await CreateController("Bob").RestoreFlaggedDeal(9, new ResolveFlagRequest { AdminNotes = "ok" }));

        await _repository.Received(1).RestoreFlaggedDealAsync(9, "Bob", "ok");
    }

    [Fact]
    public async Task RestoreFlaggedDeal_Returns500_OnError()
    {
        _repository.RestoreFlaggedDealAsync(9, Arg.Any<string>(), Arg.Any<string?>()).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().RestoreFlaggedDeal(9, new ResolveFlagRequest()), "boom");
    }

    [Theory]
    [InlineData(DealExtendResult.Extended, typeof(NoContentResult))]
    [InlineData(DealExtendResult.FlagNotFound, typeof(NotFoundObjectResult))]
    [InlineData(DealExtendResult.NotExtendable, typeof(ConflictObjectResult))]
    public async Task ExtendFlaggedDeal_ExtendsAsTheSignedInRulerName(DealExtendResult result, Type expected)
    {
        _repository.ExtendFlaggedDealAsync(9, DealExtensionKind.UntilNextLeg, "Bob", "ok").Returns(result);

        Assert.IsType(expected, await CreateController("Bob").ExtendFlaggedDeal(9,
            new ExtendFlagRequest { Kind = DealExtensionKind.UntilNextLeg, AdminNotes = "ok" }));

        await _repository.Received(1).ExtendFlaggedDealAsync(9, DealExtensionKind.UntilNextLeg, "Bob", "ok");
    }

    [Fact]
    public async Task ExtendFlaggedDeal_Returns500_OnError()
    {
        _repository.ExtendFlaggedDealAsync(9, Arg.Any<DealExtensionKind>(), Arg.Any<string>(), Arg.Any<string?>())
            .ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().ExtendFlaggedDeal(9, new ExtendFlagRequest()), "boom");
    }

    [Fact]
    public void DealFlagDetail_CanOnlyExtendWarningsOnDealsTheirAccountIsStillHoldingUp()
    {
        static DealFlagDetail Flag(Action<DealFlagDetail>? change = null)
        {
            var flag = new DealFlagDetail
            {
                FlagType = DealFlagType.OneDayInactive,
                DealStatus = DealStatus.Active,
                FlaggedAccount = new AccountSimplified { AccountId = 7 },
                ResponsibleAccountId = 7
            };
            change?.Invoke(flag);
            return flag;
        }

        Assert.True(Flag().CanExtendDeal);
        Assert.False(Flag(f => f.ResponsibleAccountId = 8).CanExtendDeal);
        Assert.False(Flag(f => f.ResponsibleAccountId = null).CanExtendDeal);
        Assert.False(Flag(f => f.DealStatus = DealStatus.Cancelled).CanExtendDeal);
        Assert.False(Flag(f => f.FlagType = DealFlagType.FiveDayReplacement).CanExtendDeal);
    }

    [Fact]
    public void DealFlagDetail_CanOnlyRestoreDealsItsFlagCancelled_WhileTheirSlotsAndAccountsAreFree()
    {
        static DealFlagDetail Flag(Action<DealFlagDetail>? change = null)
        {
            var flag = new DealFlagDetail
            {
                FlagType = DealFlagType.FiveDayReplacement,
                DealStatus = DealStatus.Cancelled,
                DealCancelledBy = "system",
                FlaggedSlot = new SlotDetail { CashSentCycles = 1, TechReceivedCycles = 1 },
                InnocentSlot = new SlotDetail { CashReceivedCycles = 1, TechSentCycles = 1 }
            };
            change?.Invoke(flag);
            return flag;
        }

        Assert.True(Flag().CanRestoreDeal);
        Assert.True(Flag(f => f.FlagType = DealFlagType.AidFailed).CanRestoreDeal);
        Assert.False(Flag(f => f.FlagType = DealFlagType.OneDayInactive).CanRestoreDeal);
        Assert.False(Flag(f => f.DealStatus = DealStatus.Active).CanRestoreDeal);
        Assert.False(Flag(f => f.DealCancelledBy = "Bob").CanRestoreDeal);
        Assert.False(Flag(f => f.FlaggedSlotOtherDealId = 3).CanRestoreDeal);
        Assert.False(Flag(f => f.InnocentSlotOtherDealId = 3).CanRestoreDeal);
        Assert.False(Flag(f => f.PairOtherDealId = 3).CanRestoreDeal);
        Assert.False(Flag(f => f.FlaggedAccountOnHold = true).CanRestoreDeal);
        Assert.False(Flag(f => f.InnocentAccountOnHold = true).CanRestoreDeal);
        Assert.False(Flag(f => f.InnocentSlot.TechSentCycles = 2).CanRestoreDeal);
    }
}
