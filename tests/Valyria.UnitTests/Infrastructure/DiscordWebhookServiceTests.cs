using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Valyria.Infrastructure.Services.Discord;
using Valyria.Infrastructure.Services.Discord.Models;

namespace Valyria.UnitTests.Infrastructure;

public class DiscordWebhookServiceTests
{
    private const string WebhookUrl = "https://discord.test/api/webhooks/1/abc";

    /// <summary>
    /// Records every request and answers with the queued responses (200 once they run out)
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(Uri? Uri, DiscordWebhookMessage? Body)> Requests { get; } = [];
        public Queue<HttpResponseMessage> Responses { get; } = new();
        public Exception? ThrowOnSend { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ThrowOnSend != null)
                throw ThrowOnSend;

            Requests.Add((request.RequestUri, await request.Content!.ReadFromJsonAsync<DiscordWebhookMessage>(cancellationToken)));
            return Responses.TryDequeue(out var response) ? response : new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }

    private readonly RecordingHandler _handler = new();

    private DiscordWebhookService CreateService(string? webhookUrl = WebhookUrl)
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(_handler));

        return new DiscordWebhookService(
            NullLogger<DiscordWebhookService>.Instance,
            httpClientFactory,
            Options.Create(new DiscordWebhookOptions { WebhookUrl = webhookUrl }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SkipsSending_WhenTheWebhookIsNotConfigured(string? webhookUrl)
    {
        await CreateService(webhookUrl).SendNewDealsNotificationAsync([111]);

        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task SkipsSending_WhenNobodyHasANewPartner()
    {
        await CreateService().SendNewDealsNotificationAsync([]);

        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task MentionsEveryoneInOneMessage_WhenItFits()
    {
        await CreateService().SendNewDealsNotificationAsync([111, 222]);

        var (uri, body) = Assert.Single(_handler.Requests);
        Assert.Equal(WebhookUrl, uri!.ToString());
        Assert.StartsWith("You've been matched with a new VEP partner!", body!.Content);
        Assert.EndsWith(" <@111> <@222>", body.Content);
        Assert.Empty(body.Embeds!);
        Assert.Empty(body.Attachments!);
    }

    [Fact]
    public async Task SplitsMentionsAcrossMessages_ToStayUnderDiscordsLimit()
    {
        var ids = Enumerable.Range(0, 250).Select(i => 100_000_000_000_000_000L + i).ToList();

        await CreateService().SendNewDealsNotificationAsync(ids);

        Assert.True(_handler.Requests.Count > 1);
        Assert.All(_handler.Requests, r => Assert.InRange(r.Body!.Content!.Length, 1, 2000));
        var allContent = string.Join(" ", _handler.Requests.Select(r => r.Body!.Content));
        Assert.All(ids, id => Assert.Single(System.Text.RegularExpressions.Regex.Matches(allContent, $"<@{id}>")));
    }

    [Fact]
    public async Task StopsSending_AfterAFailedResponse()
    {
        var ids = Enumerable.Range(0, 250).Select(i => 100_000_000_000_000_000L + i).ToList();
        _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("slow down") });

        await CreateService().SendNewDealsNotificationAsync(ids);

        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task SwallowsNetworkErrors()
    {
        _handler.ThrowOnSend = new HttpRequestException("no route to host");

        var exception = await Record.ExceptionAsync(() => CreateService().SendNewDealsNotificationAsync([111]));

        Assert.Null(exception);
    }
}
