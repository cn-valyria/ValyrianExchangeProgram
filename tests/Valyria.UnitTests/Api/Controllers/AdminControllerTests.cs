using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Valyria.Api.Controllers;
using Valyria.Core.DTOs;
using Valyria.Core.Interfaces;
using static Valyria.UnitTests.TestHelpers;

namespace Valyria.UnitTests.Api.Controllers;

public class AdminControllerTests
{
    private readonly IAdminRepository _repository = Substitute.For<IAdminRepository>();
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _controller = new AdminController(_repository, NullLogger<AdminController>.Instance).WithUser();
    }

    public interface IReadEndpoint
    {
        object Arrange(IAdminRepository repository);
        void ArrangeError(IAdminRepository repository, Exception error);
        Task<IActionResult> Act(AdminController controller);
    }

    private sealed class ReadEndpoint<T>(
        string name,
        Func<IAdminRepository, Task<T>> repositoryCall,
        T value,
        Func<AdminController, Task<IActionResult>> act) : IReadEndpoint
    {
        public object Arrange(IAdminRepository repository)
        {
            repositoryCall(repository).Returns(value);
            return value!;
        }

        public void ArrangeError(IAdminRepository repository, Exception error) => repositoryCall(repository).ThrowsAsync(error);

        public Task<IActionResult> Act(AdminController controller) => act(controller);

        public override string ToString() => name;
    }

    /// <summary>
    /// Every dashboard and settings read is a straight pass-through of one repository call
    /// </summary>
    public static TheoryData<IReadEndpoint> ReadEndpoints => new()
    {
        new ReadEndpoint<ProgramSummary>(nameof(AdminController.GetProgramSummary),
            r => r.GetProgramSummaryAsync(), new() { TotalAccounts = 10, ActiveDeals = 4 }, c => c.GetProgramSummary()),
        new ReadEndpoint<List<SlotConfigSummary>>(nameof(AdminController.GetSlotConfigs),
            r => r.GetSlotConfigsAsync(), [new() { CashSentCycles = 1, TechReceivedCycles = 1, TotalSlots = 4 }], c => c.GetSlotConfigs()),
        new ReadEndpoint<List<DealHealthByPairing>>(nameof(AdminController.GetDealHealth),
            r => r.GetDealHealthAsync(), [new() { Health = DealHealth.AtRisk, Deals = 2 }], c => c.GetDealHealth()),
        new ReadEndpoint<List<DailyDealActivity>>(nameof(AdminController.GetDealActivity),
            r => r.GetDealActivityAsync(14), [new() { LegsSent = 3 }], c => c.GetDealActivity()),
        new ReadEndpoint<List<CancellationReasonCount>>(nameof(AdminController.GetCancellationReasons),
            r => r.GetCancellationReasonsAsync(30), [new() { Reason = "Nation deleted", Deals = 1 }], c => c.GetCancellationReasons(30)),
        new ReadEndpoint<List<AdminAccessSetting>>(nameof(AdminController.GetAdminAccessSettings),
            r => r.GetAdminAccessSettingsAsync(), [new() { AccountId = 1, AdminRoleName = "Admin" }], c => c.GetAdminAccessSettings()),
    };

    [Theory]
    [MemberData(nameof(ReadEndpoints))]
    public async Task ReadEndpoints_ReturnTheRepositoryResult(IReadEndpoint endpoint)
    {
        var expected = endpoint.Arrange(_repository);

        var ok = Assert.IsType<OkObjectResult>(await endpoint.Act(_controller));

        Assert.Same(expected, ok.Value);
    }

    [Theory]
    [MemberData(nameof(ReadEndpoints))]
    public async Task ReadEndpoints_Return500_OnError(IReadEndpoint endpoint)
    {
        endpoint.ArrangeError(_repository, new Exception("boom"));

        AssertServerError(await endpoint.Act(_controller), "boom");
    }

    [Fact]
    public async Task CreateAdminAccess_ReturnsOk_WhenCreated()
    {
        _repository.CreateAdminAccessAsync(1, 2).Returns(true);

        var ok = Assert.IsType<OkObjectResult>(await _controller.CreateAdminAccess(new AdminAccessCreateRequest { AccountId = 1, AdminRoleId = 2 }));
        Assert.Equal(true, Prop(ok.Value, "success"));
    }

    [Fact]
    public async Task CreateAdminAccess_Returns400_WhenNothingWasCreated()
    {
        _repository.CreateAdminAccessAsync(1, 2).Returns(false);

        Assert.IsType<BadRequestObjectResult>(await _controller.CreateAdminAccess(new AdminAccessCreateRequest { AccountId = 1, AdminRoleId = 2 }));
    }

    [Fact]
    public async Task CreateAdminAccess_Returns500_OnError()
    {
        _repository.CreateAdminAccessAsync(1, 2).ThrowsAsync(new Exception("duplicate"));

        AssertServerError(await _controller.CreateAdminAccess(new AdminAccessCreateRequest { AccountId = 1, AdminRoleId = 2 }), "duplicate");
    }

    [Fact]
    public async Task DeleteAdminAccess_ReturnsOk_WhenDeleted()
    {
        _repository.DeleteAdminAccessAsync(1, 2).Returns(true);

        Assert.IsType<OkObjectResult>(await _controller.DeleteAdminAccess(new AdminAccessDeleteRequest { AccountId = 1, AdminRoleId = 2 }));
    }

    [Fact]
    public async Task DeleteAdminAccess_Returns400_WhenNothingWasDeleted()
    {
        _repository.DeleteAdminAccessAsync(1, 2).Returns(false);

        Assert.IsType<BadRequestObjectResult>(await _controller.DeleteAdminAccess(new AdminAccessDeleteRequest { AccountId = 1, AdminRoleId = 2 }));
    }

    [Fact]
    public async Task DeleteAdminAccess_Returns500_OnError()
    {
        _repository.DeleteAdminAccessAsync(1, 2).ThrowsAsync(new Exception("boom"));

        AssertServerError(await _controller.DeleteAdminAccess(new AdminAccessDeleteRequest { AccountId = 1, AdminRoleId = 2 }), "boom");
    }
}
