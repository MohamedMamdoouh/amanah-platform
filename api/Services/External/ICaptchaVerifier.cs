using Amanah.Api.Models.Results;

namespace Amanah.Api.Services.External;

public interface ICaptchaVerifier
{
    Task<Result> VerifyAsync(string token, CancellationToken cancellationToken = default);
}
