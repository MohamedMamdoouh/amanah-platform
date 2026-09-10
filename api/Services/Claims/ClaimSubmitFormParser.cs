using System.Text.Json;
using Amanah.Api.Models.Common;
using Amanah.Api.Models.Errors;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Claims;

namespace Amanah.Api.Services.Claims;

public sealed record ClaimSubmitForm(
    SubmitClaimRequest Request,
    IFormFile? Photo);

public sealed class ClaimSubmitFormParser
{
    public async Task<Result<ClaimSubmitForm>> ParseAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.HasFormContentType)
        {
            return ClaimPartError("Claim submission must use multipart form data.");
        }

        var form = await request.ReadFormAsync(cancellationToken);

        var claimJson = form["claim"].FirstOrDefault()?.ToString();
        if (string.IsNullOrWhiteSpace(claimJson))
        {
            return ClaimPartError("Claim data is required.");
        }

        SubmitClaimRequest? claimRequest;
        try
        {
            claimRequest = JsonSerializer.Deserialize<SubmitClaimRequest>(
                claimJson,
                ApiJson.SerializerOptions);
        }
        catch (JsonException)
        {
            return ClaimPartError("Claim data is invalid.");
        }

        if (claimRequest is null)
        {
            return ClaimPartError("Claim data is required.");
        }

        var photoFiles = form.Files.GetFiles("photo");
        if (photoFiles.Count > 1)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                ErrorCodes.ValidationFailed,
                new Dictionary<string, string[]>
                {
                    ["photo"] = ["At most one photo is allowed."],
                });
        }

        IFormFile? photo = photoFiles.Count == 1 ? photoFiles[0] : null;
        if (photo is not null && photo.Length == 0)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                ErrorCodes.ValidationFailed,
                new Dictionary<string, string[]>
                {
                    ["photo"] = ["Photo file is required."],
                });
        }

        return new ClaimSubmitForm(claimRequest, photo);
    }

    private static ResultError ClaimPartError(string message) =>
        ResultError.BadRequest(
            "Please correct the errors in the form.",
            ErrorCodes.ValidationFailed,
            new Dictionary<string, string[]>
            {
                ["claim"] = [message],
            });
}
