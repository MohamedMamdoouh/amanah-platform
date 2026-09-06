using Amanah.Api.Services.External;

namespace Amanah.Api.Tests.External;

public class ResendHttpStatusesTests
{
    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void Transient_status_codes_are_retried(int statusCode)
    {
        Assert.True(ResendHttpStatuses.IsTransient(statusCode));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(422)]
    public void Permanent_client_errors_are_not_retried(int statusCode)
    {
        Assert.False(ResendHttpStatuses.IsTransient(statusCode));
    }
}
