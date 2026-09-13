using System.Net;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Resolution;
using Amanah.Contracts.Errors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Amanah.Api.Tests.Chats;

public class ChatRateLimitTests : IClassFixture<ChatRateLimitWebApplicationFactory>
{
    private readonly ChatRateLimitWebApplicationFactory _factory;

    public ChatRateLimitTests(ChatRateLimitWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Exceeding_per_minute_chat_message_limit_returns_429()
    {
        await using var context = await ReportTestContext.CreateAsync(_factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ResolutionTestHelpers.AuthenticateReporter(context.Client, context);

        for (var i = 0; i < 2; i++)
        {
            var send = await ChatTestHelpers.SendMessageAsync(context.Client, threadId, $"Message {i}");
            Assert.Equal(HttpStatusCode.Created, send.Response.StatusCode);
        }

        var limited = await ChatTestHelpers.SendMessageAsync(context.Client, threadId, "Too many");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.Response.StatusCode);

        var error = await ChatTestHelpers.ReadErrorAsync(limited.Response);
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.RateLimitExceeded, error.Code);
    }
}

public sealed class ChatRateLimitWebApplicationFactory : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Policies:chat-message:PermitLimit"] = "2",
                ["RateLimit:Policies:chat-message:WindowSeconds"] = "60",
                ["RateLimit:Policies:chat-message:PartitionBy"] = "userId",
                ["RateLimit:Policies:chat-message-hourly:PermitLimit"] = "100",
                ["RateLimit:Policies:chat-message-hourly:WindowSeconds"] = "3600",
                ["RateLimit:Policies:chat-message-hourly:PartitionBy"] = "userId",
            });
        });
    }
}
