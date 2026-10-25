using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Valyria.Api.Auth;
using Valyria.Core.Contracts;
using Valyria.Core.DTOs;
using Valyria.Core.Interfaces;
using Valyria.UnitTests.Api.Auth;

namespace Valyria.UnitTests.Api;

/// <summary>
/// Boots the real API (Program.cs) in memory with substituted repositories, so authentication,
/// authorization, routing and serialization are exercised the way the UI hits them
/// </summary>
public class ApiPipelineTests : IDisposable
{
    private sealed class ValyriaApiFactory : WebApplicationFactory<Program>
    {
        public IAuthorizationRepository AuthorizationRepository { get; } = Substitute.For<IAuthorizationRepository>();
        public IAccountsRepository AccountsRepository { get; } = Substitute.For<IAccountsRepository>();
        public IAdminRepository AdminRepository { get; } = Substitute.For<IAdminRepository>();
        public IDealsRepository DealsRepository { get; } = Substitute.For<IDealsRepository>();
        public IMassMessagesRepository MassMessagesRepository { get; } = Substitute.For<IMassMessagesRepository>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("JwtSecret", TokenProviderTests.Secret);
            builder.UseSetting("VepDbConnectionString", "Server=unused-in-tests");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(AuthorizationRepository);
                services.AddSingleton(AccountsRepository);
                services.AddSingleton(AdminRepository);
                services.AddSingleton(DealsRepository);
                services.AddSingleton(MassMessagesRepository);
            });
        }
    }

    private const int BobAccountId = 42;

    private readonly ValyriaApiFactory _factory = new();
    private readonly HttpClient _client;

    public ApiPipelineTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static AuthorizedAccount Bob(params string[] adminRoles) => new()
    {
        AccountId = BobAccountId,
        NationId = 123,
        RulerName = "Bob",
        Role = 'B',
        Roles = adminRoles.Select((name, i) => new AccountRole { Id = i + 1, Name = name }).ToList()
    };

    private static string CreateToken(AuthorizedAccount account, string secret = TokenProviderTests.Secret)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSecret"] = secret })
            .Build();
        return new TokenProvider(configuration, NullLogger<TokenProvider>.Instance)
            .GenerateAccessToken(new AuthorizeUserRequest { NationId = "123", UniqueCode = "ABCDE" }, account);
    }

    /// <summary>
    /// What the database says about Bob on each request (CustomJwtBearerEvents re-checks it)
    /// </summary>
    private void BobInDatabase(AuthorizedAccount? account) =>
        _factory.AuthorizationRepository.Authorize(123, "ABCDE").Returns(account!);

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, string? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await _client.SendAsync(request);
    }

    private const string Member = "member";
    private const string View = "view";
    private const string Manage = "manage";
    private const string Messages = "messages";
    private const string SendMessages = "send";

    private static readonly Dictionary<string, string[]> AllowedRoles = new()
    {
        [View] = [AdminRoles.SuperAdmin, AdminRoles.ReadOnly],
        [Manage] = [AdminRoles.SuperAdmin],
        [Messages] = [AdminRoles.SuperAdmin, AdminRoles.ReadOnly, AdminRoles.Lists],
        [SendMessages] = [AdminRoles.SuperAdmin, AdminRoles.Lists],
    };

    /// <summary>
    /// Every protected endpoint and who the UI lets use it (middleware/auth.global.ts and the admin pages)
    /// </summary>
    public static TheoryData<string, string, string?, string> Endpoints => new()
    {
        { "GET", "/api/accounts/topScorers", null, Member },
        { "GET", "/api/accounts", null, View },
        { "POST", "/api/accounts", "{}", Manage },
        { "PUT", "/api/accounts", "{}", Manage },
        { "DELETE", "/api/accounts/5", null, Manage },
        { "GET", "/api/accounts/prospect?nationId=1", null, Manage },
        { "GET", "/api/admin/dashboard/summary", null, View },
        { "GET", "/api/admin/dashboard/activity?days=14", null, View },
        { "GET", "/api/admin/settings/access", null, Manage },
        { "POST", "/api/admin/settings/access", "{\"accountId\":1,\"adminRoleId\":2}", Manage },
        { "DELETE", "/api/admin/settings/access", "{\"accountId\":1,\"adminRoleId\":2}", Manage },
        { "GET", "/api/deals/3", null, View },
        { "POST", "/api/deals", "{\"slot1Id\":1,\"slot2Id\":2}", Manage },
        { "PUT", "/api/deals/3/cancel", "{\"reason\":\"x\"}", Manage },
        { "GET", "/api/deals/admin/active", null, View },
        { "GET", "/api/deals/admin/aid-activity", null, View },
        { "GET", "/api/deals/admin/wait-queue", null, View },
        { "GET", "/api/deals/admin/flagged", null, View },
        { "PUT", "/api/deals/admin/flags/1/resolve", "{}", Manage },
        { "GET", "/api/slots/account/7", null, View },
        { "GET", "/api/slots/available/7", null, View },
        { "PUT", "/api/slots/1", "{}", Manage },
        { "GET", "/api/mass-messages", null, Messages },
        { "PUT", "/api/mass-messages/1/sent", "{\"sent\":true}", SendMessages },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoints_AreLimitedToTheAdminRolesTheUiAllows(string method, string path, string? body, string access)
    {
        string?[] signedInAs = [null, AdminRoles.SuperAdmin, AdminRoles.ReadOnly, AdminRoles.Lists];
        foreach (var adminRole in signedInAs)
        {
            var account = adminRole == null ? Bob() : Bob(adminRole);
            BobInDatabase(account);

            var response = await Send(new HttpMethod(method), path, CreateToken(account), body);

            var allowed = access == Member || (adminRole != null && AllowedRoles[access].Contains(adminRole));
            if (allowed)
                Assert.True(response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden),
                    $"{adminRole ?? "A member"} should be able to {method} {path}, but got {(int)response.StatusCode}");
            else
                Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
                    $"{adminRole ?? "A member"} should get 403 from {method} {path}, but got {(int)response.StatusCode}");
        }
    }

    [Theory]
    [InlineData("/api/accounts/{0}")]
    [InlineData("/api/deals/account/{0}")]
    [InlineData("/api/deals/account/{0}/history")]
    [InlineData("/api/deals/3/activity?accountId={0}")]
    public async Task AccountEndpoints_OnlyServeTheMembersOwnAccount(string pathFormat)
    {
        BobInDatabase(Bob());
        var token = CreateToken(Bob());

        var own = await Send(HttpMethod.Get, string.Format(pathFormat, BobAccountId), token);
        var other = await Send(HttpMethod.Get, string.Format(pathFormat, BobAccountId + 1), token);

        Assert.NotEqual(HttpStatusCode.Forbidden, own.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WithoutAToken()
    {
        var response = await Send(HttpMethod.Get, "/api/accounts/topScorers", token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.AccountsRepository.ReceivedCalls());
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_ForATokenSignedWithAnotherSecret()
    {
        BobInDatabase(Bob());

        var response = await Send(HttpMethod.Get, "/api/accounts/topScorers",
            CreateToken(Bob(), "some-other-secret-that-is-long-enough-for-hmac-sha256-signing"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WhenTheTokenAccountNoLongerExists()
    {
        BobInDatabase(null);

        var response = await Send(HttpMethod.Get, "/api/accounts/topScorers", CreateToken(Bob()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminRolesRevokedInTheDatabase_StopWorkingBeforeTheTokenExpires()
    {
        var token = CreateToken(Bob(AdminRoles.SuperAdmin));
        BobInDatabase(Bob());

        var response = await Send(HttpMethod.Get, "/api/accounts", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminRolesGrantedInTheDatabase_WorkWithAnOlderToken()
    {
        var token = CreateToken(Bob());
        BobInDatabase(Bob(AdminRoles.ReadOnly));
        _factory.AccountsRepository.GetAccountDetailsAsync(false).Returns([new AccountDetail { Id = 1, NationName = "Buyland" }]);

        var response = await Send(HttpMethod.Get, "/api/accounts", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accounts = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Buyland", accounts[0].GetProperty("nationName").GetString());
    }

    [Fact]
    public async Task AuditFields_RecordTheRulerName()
    {
        BobInDatabase(Bob(AdminRoles.SuperAdmin));

        var response = await Send(HttpMethod.Post, "/api/deals", CreateToken(Bob(AdminRoles.SuperAdmin)), "{\"slot1Id\":1,\"slot2Id\":2}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await _factory.DealsRepository.Received(1).CreateDealAsync(Arg.Is<CreateDealRequest>(r => r.CreatedBy == "Bob"));
    }

    [Fact]
    public async Task UserAccount_ReturnsTheSignedInAccount()
    {
        BobInDatabase(Bob(AdminRoles.SuperAdmin));

        var response = await Send(HttpMethod.Get, "/api/user/account", CreateToken(Bob()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(BobAccountId, account.GetProperty("accountId").GetInt32());
        Assert.Equal("Bob", account.GetProperty("rulerName").GetString());
        Assert.Equal(AdminRoles.SuperAdmin, account.GetProperty("roles")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task TokenEndpoint_IssuesTokensWithTheAccountsClaims_ThatTheApiAccepts()
    {
        BobInDatabase(Bob(AdminRoles.ReadOnly));

        var tokenResponse = await _client.PostAsJsonAsync("/api/user/token",
            new { grantType = "password", nationId = "123", password = "ABCDE" });

        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var accessToken = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.ToList();
        Assert.Equal("Bob", claims.Single(c => c.Type == TokenClaims.Name).Value);
        Assert.Equal("B", claims.Single(c => c.Type == TokenClaims.VepRole).Value);
        Assert.Equal(AdminRoles.ReadOnly, claims.Single(c => c.Type == TokenClaims.AdminRole).Value);
        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Get, "/api/accounts", accessToken)).StatusCode);
    }

    [Fact]
    public async Task TokenEndpoint_Returns401_ForAWrongUniqueCode()
    {
        _factory.AuthorizationRepository.Authorize(123, "WRONG").Returns((AuthorizedAccount)null!);

        var response = await _client.PostAsJsonAsync("/api/user/token",
            new { grantType = "password", nationId = "123", password = "WRONG" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedBody_ReturnsTheCustomValidationResponse()
    {
        var response = await _client.PostAsync("/api/user/token",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("One or more validation errors occurred.", body.GetProperty("title").GetString());
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.True(body.GetProperty("errors").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Swagger_IsServed()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Valyria API", await response.Content.ReadAsStringAsync());
    }
}
