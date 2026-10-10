using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Valyria.Api.Controllers;
using Valyria.Core.DTOs;
using Valyria.Core.Interfaces;
using static Valyria.UnitTests.TestHelpers;

namespace Valyria.UnitTests.Api.Controllers;

public class MassMessagesControllerTests
{
    private readonly IMassMessagesRepository _repository = Substitute.For<IMassMessagesRepository>();

    private MassMessagesController CreateController(params Claim[] claims) =>
        new MassMessagesController(_repository, NullLogger<MassMessagesController>.Instance).WithUser(claims);

    [Fact]
    public async Task GetMassMessages_ReturnsTheSummary()
    {
        var summary = new MassMessagesSummary { Messages = [new MassMessage { Id = 1 }] };
        _repository.GetMassMessagesAsync().Returns(summary);

        Assert.Same(summary, AssertOk<MassMessagesSummary>(await CreateController().GetMassMessages()));
    }

    [Fact]
    public async Task GetMassMessages_Returns500_OnError()
    {
        _repository.GetMassMessagesAsync().ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController().GetMassMessages(), "boom");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetMessageSent_RecordsTheSignedInAccount(bool sent)
    {
        _repository.SetMessageSentAsync(2, sent, 42).Returns(true);

        var result = await CreateController(new Claim("AccountId", "42")).SetMessageSent(2, new MassMessageSentRequest { Sent = sent });

        Assert.IsType<NoContentResult>(result);
        await _repository.Received(1).SetMessageSentAsync(2, sent, 42);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-number")]
    public async Task SetMessageSent_Returns401_WithoutAnAccountIdClaim(string? accountId)
    {
        var claims = accountId == null ? [] : new[] { new Claim("AccountId", accountId) };

        Assert.IsType<UnauthorizedResult>(await CreateController(claims).SetMessageSent(2, new MassMessageSentRequest { Sent = true }));
        Assert.Empty(_repository.ReceivedCalls());
    }

    [Fact]
    public async Task SetMessageSent_Returns404_WhenTheMessageIsMissing()
    {
        _repository.SetMessageSentAsync(2, true, 42).Returns(false);

        Assert.IsType<NotFoundObjectResult>(await CreateController(new Claim("AccountId", "42")).SetMessageSent(2, new MassMessageSentRequest { Sent = true }));
    }

    [Fact]
    public async Task SetMessageSent_Returns500_OnError()
    {
        _repository.SetMessageSentAsync(2, true, 42).ThrowsAsync(new Exception("boom"));

        AssertServerError(await CreateController(new Claim("AccountId", "42")).SetMessageSent(2, new MassMessageSentRequest { Sent = true }), "boom");
    }
}
