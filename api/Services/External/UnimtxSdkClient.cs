using UniSdk;

namespace Amanah.Api.Services.External;

public interface IUnimtxClient
{
    Task SendMessageAsync(object request, CancellationToken cancellationToken = default);
}

public sealed class UnimtxSdkClient(UniClient client) : IUnimtxClient
{
    public async Task SendMessageAsync(
        object request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await client.Messages.SendAsync(request);
    }
}
