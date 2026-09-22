using System.Net;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Amanah.Api.Tests.Uploads;

public class PhotoUploadRateLimitTests : IClassFixture<PhotoUploadPerMinuteWebApplicationFactory>
{
    private readonly PhotoUploadPerMinuteWebApplicationFactory _factory;

    public PhotoUploadRateLimitTests(PhotoUploadPerMinuteWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Exceeding_per_minute_photo_upload_limit_returns_429() =>
        await PhotoUploadRateLimitAssertions.AssertBurstRejectedAsync(_factory);
}

public class PhotoUploadHourlyRateLimitTests : IClassFixture<PhotoUploadHourlyWebApplicationFactory>
{
    private readonly PhotoUploadHourlyWebApplicationFactory _factory;

    public PhotoUploadHourlyRateLimitTests(PhotoUploadHourlyWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task Exceeding_hourly_photo_upload_limit_returns_429() =>
        await PhotoUploadRateLimitAssertions.AssertBurstRejectedAsync(_factory);
}

internal static class PhotoUploadRateLimitAssertions
{
    public static async Task AssertBurstRejectedAsync(ApiWebApplicationFactory factory)
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var photo = TestImageFactory.CreateMinimalJpeg();

        for (var i = 0; i < 2; i++)
        {
            var request = TestReportHelpers.BuildValidLostRequest(title: $"Lost black iPhone {i}");
            var (response, _) = await context.SubmitReportAsync(request, [photo]);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var limitedRequest = TestReportHelpers.BuildValidLostRequest(title: "Lost black iPhone over limit");
        var (limited, _) = await context.SubmitReportAsync(limitedRequest, [photo]);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(limited);
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.RateLimitExceeded, error.Code);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.True(limited.Headers.RetryAfter!.Delta > TimeSpan.Zero);
    }
}

public sealed class PhotoUploadPerMinuteWebApplicationFactory : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Policies:photo-upload:PermitLimit"] = "2",
                ["RateLimit:Policies:photo-upload:WindowSeconds"] = "60",
                ["RateLimit:Policies:photo-upload:PartitionBy"] = "userId",
                ["RateLimit:Policies:photo-upload-hourly:PermitLimit"] = "100",
                ["RateLimit:Policies:photo-upload-hourly:WindowSeconds"] = "3600",
                ["RateLimit:Policies:photo-upload-hourly:PartitionBy"] = "userId",
            });
        });
    }
}

public sealed class PhotoUploadHourlyWebApplicationFactory : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Policies:photo-upload:PermitLimit"] = "100",
                ["RateLimit:Policies:photo-upload:WindowSeconds"] = "60",
                ["RateLimit:Policies:photo-upload:PartitionBy"] = "userId",
                ["RateLimit:Policies:photo-upload-hourly:PermitLimit"] = "2",
                ["RateLimit:Policies:photo-upload-hourly:WindowSeconds"] = "3600",
                ["RateLimit:Policies:photo-upload-hourly:PartitionBy"] = "userId",
            });
        });
    }
}
