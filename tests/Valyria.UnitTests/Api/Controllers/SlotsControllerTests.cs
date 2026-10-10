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

public class SlotsControllerTests
{
    private readonly IDealsRepository _repository = Substitute.For<IDealsRepository>();
    private readonly SlotsController _controller;

    public SlotsControllerTests()
    {
        _controller = new SlotsController(_repository, NullLogger<SlotsController>.Instance)
            .WithUser(new Claim(TokenClaims.Name, "Bob"), new Claim(TokenClaims.AdminRole, AdminRoles.SuperAdmin));
    }

    [Fact]
    public async Task GetAccountSlots_ReturnsTheSlots()
    {
        var slots = new AccountSlotsResponse { AccountId = 7, Slots = [new SlotDetail { Id = 1 }] };
        _repository.GetAccountSlotsAsync(7).Returns(slots);

        Assert.Same(slots, AssertOk<AccountSlotsResponse>(await _controller.GetAccountSlots(7)));
    }

    [Fact]
    public async Task GetAccountSlots_Returns404_WhenTheAccountIsMissing()
    {
        _repository.GetAccountSlotsAsync(7).Returns((AccountSlotsResponse)null!);

        Assert.IsType<NotFoundObjectResult>(await _controller.GetAccountSlots(7));
    }

    [Fact]
    public async Task GetAccountSlots_Returns500_OnError()
    {
        _repository.GetAccountSlotsAsync(7).ThrowsAsync(new Exception("boom"));

        AssertServerError(await _controller.GetAccountSlots(7), "boom");
    }

    [Fact]
    public async Task UpdateSlot_UsesTheRouteSlotId_AndTheSignedInRulerName()
    {
        _repository.UpdateSlotAsync(Arg.Any<SlotUpdateRequest>(), "Bob").Returns(true);
        var request = new SlotUpdateRequest { SlotId = 999, TechSentCycles = 2 };

        Assert.IsType<NoContentResult>(await _controller.UpdateSlot(4, request));

        await _repository.Received(1).UpdateSlotAsync(Arg.Is<SlotUpdateRequest>(r => r.SlotId == 4 && r.TechSentCycles == 2), "Bob");
    }

    [Fact]
    public async Task UpdateSlot_Returns404_WhenTheSlotIsMissing()
    {
        _repository.UpdateSlotAsync(Arg.Any<SlotUpdateRequest>(), Arg.Any<string>()).Returns(false);

        Assert.IsType<NotFoundObjectResult>(await _controller.UpdateSlot(4, new SlotUpdateRequest()));
    }

    [Fact]
    public async Task UpdateSlot_Returns500_OnError()
    {
        _repository.UpdateSlotAsync(Arg.Any<SlotUpdateRequest>(), Arg.Any<string>()).ThrowsAsync(new Exception("boom"));

        AssertServerError(await _controller.UpdateSlot(4, new SlotUpdateRequest()), "boom");
    }

    [Fact]
    public async Task GetAvailableSlots_ReturnsTheSlots()
    {
        var slots = new List<SlotDetail> { new() { Id = 1 } };
        _repository.GetAvailableSlotsAsync(7).Returns(slots);

        Assert.Same(slots, AssertOk<List<SlotDetail>>(await _controller.GetAvailableSlots(7)));
    }

    [Fact]
    public async Task GetAvailableSlots_Returns500_OnError()
    {
        _repository.GetAvailableSlotsAsync(7).ThrowsAsync(new Exception("boom"));

        AssertServerError(await _controller.GetAvailableSlots(7), "boom");
    }
}
