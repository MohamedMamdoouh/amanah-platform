using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Amanah.Api.Tests;

public class RateLimitTests : IClassFixture<RateLimitWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RateLimitTests(RateLimitWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Rate_limit_exceeded_returns_429_with_retry_after()
    {
        _client.DefaultRequestHeaders.Add("Origin", "http://localhost:4200");

        await _client.GetAsync("/health");
        await _client.GetAsync("/health");

        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.RetryAfter is not null);
        Assert.True(int.TryParse(response.Headers.RetryAfter.ToString(), out var retryAfter));
        Assert.True(retryAfter > 0);
        Assert.Equal(
            "http://localhost:4200",
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("rate_limit.exceeded", body.GetProperty("code").GetString());
    }
}
