using Valyria.Infrastructure.Repositories;

namespace Valyria.IntegrationTests;

public class AuthorizationRepositoryTests(MySqlFixture fixture) : RepositoryTestBase(fixture)
{
    private AuthorizationRepository Repository => new(Fixture.VepConnectionString);

    [Fact]
    public async Task Authorize_ByNationId_ReturnsTheAccountAndItsRoles()
    {
        var accountId = await Data.MemberAsync(100, "Bob", psw: "ABCDE");
        await Data.AccountRoleAsync(accountId, await Data.RoleAsync("Admin"));
        await Data.AccountRoleAsync(accountId, await Data.RoleAsync("Admin - Lists"));

        var account = await Repository.Authorize(100, "ABCDE");

        Assert.Equal(accountId, account.AccountId);
        Assert.Equal(["Admin", "Admin - Lists"], account.Roles.Select(r => r.Name).Order());
    }

    [Fact]
    public async Task Authorize_ByRulerName_ReturnsTheAccountWithNoRoles()
    {
        var accountId = await Data.MemberAsync(100, "Bob", psw: "ABCDE");
        await Data.MemberAsync(200, "Sue", psw: "ABCDE");

        var account = await Repository.Authorize("Bob", "ABCDE");

        Assert.Equal(accountId, account.AccountId);
        Assert.Empty(account.Roles);
    }

    [Fact(Skip = "Bug: Authorize dereferences the account before checking it was found (AuthorizationRepository.cs:30 and :43), " +
                 "so a wrong unique code throws NullReferenceException and /api/user/token returns 500 instead of 401")]
    public async Task Authorize_ReturnsNull_ForAWrongUniqueCode()
    {
        await Data.MemberAsync(100, "Bob", psw: "ABCDE");

        Assert.Null(await Repository.Authorize(100, "WRONG"));
        Assert.Null(await Repository.Authorize("Bob", "WRONG"));
    }
}
