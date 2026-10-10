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
/// routing and serialization are exercised the way the UI hits them
/// </summary>
public class ApiPipelineTests : IDisposable
{
    private sealed class ValyriaApiFactory : WebApplicationFactory<Program>
    {
        public IAuthorizationRepository AuthorizationRepository { get; } = Substitute.For<IAuthorizationRepository>();
        public IAccountsRepository AccountsRepository { get; } = Substitute.For<IAccountsRepository>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("JwtSecret", TokenProviderTests.Secret);
            builder.UseSetting("VepDbConnectionString", "Server=unused-in-tests");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(AuthorizationRepository);
                services.AddSingleton(AccountsRepository);
            });
        }
    }

    private readonly ValyriaApiFactory _factory = new();
    private readonly HttpClient _client;

    public ApiPipelineTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static string CreateToken(string secret = TokenProviderTests.Secret)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSecret"] = secret })
            .Build();
        return new TokenProvider(configuration, NullLogger<TokenProvider>.Instance)
            .GenerateAccessToken(new AuthorizeUserRequest { NationId = "123", RulerName = "Bob", UniqueCode = "ABCDE" });
    }

    private void AccountExists() =>
        _factory.AuthorizationRepository.Authorize(123, "ABCDE").Returns(new AuthorizedAccount
        {
            AccountId = 42,
            Roles = [new AccountRole { Id = 1, Name = "Admin" }]
        });

    private async Task<HttpResponseMessage> GetAccounts(string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/accounts");
        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WithoutAToken()
    {
        var response = await GetAccounts(token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.AccountsRepository.ReceivedCalls());
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_ForATokenSignedWithAnotherSecret()
    {
        AccountExists();

        var response = await GetAccounts(CreateToken("some-other-secret-that-is-long-enough-for-hmac-sha256-signing"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WhenTheTokenAccountNoLongerExists()
    {
        _factory.AuthorizationRepository.Authorize(123, "ABCDE").Returns((AuthorizedAccount)null!);

        var response = await GetAccounts(CreateToken());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_ServesValidTokens()
    {
        AccountExists();
        _factory.AccountsRepository.GetAccountDetailsAsync(false).Returns([new AccountDetail { Id = 1, NationName = "Buyland" }]);

        var response = await GetAccounts(CreateToken());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accounts = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Buyland", accounts[0].GetProperty("nationName").GetString());
    }

    [Fact]
    public async Task UserAccount_UsesTheAccountIdAddedDuringTokenValidation()
    {
        AccountExists();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/user/account");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(42, account.GetProperty("accountId").GetInt32());
        Assert.Equal("Admin", account.GetProperty("roles")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task TokenEndpoint_IssuesTokensThatTheApiAccepts()
    {
        AccountExists();

        var tokenResponse = await _client.PostAsJsonAsync("/api/user/token",
            new { grantType = "password", nationId = "123", password = "ABCDE" });

        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var tokens = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokens.GetProperty("accessToken").GetString();
        Assert.Equal(HttpStatusCode.OK, (await GetAccounts(accessToken)).StatusCode);
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
