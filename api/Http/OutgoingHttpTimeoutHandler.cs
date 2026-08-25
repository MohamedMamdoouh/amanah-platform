using Amanah.Api.Options;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Http;

public sealed class OutgoingHttpTimeoutHandler(IOptions<HttpTimeoutOptions> options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(options.Value.OutgoingHttpSeconds);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await base.SendAsync(request, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Outgoing HTTP request timed out after {timeout.TotalSeconds:0}s: {request.Method} {request.RequestUri}");
        }
    }
}
