using System.Text.Json;
using Amanah.Api.Models.Common;
using Amanah.Api.Models.Errors;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Reports;
using FluentValidation;

namespace Amanah.Api.Services.Reports;

public sealed record ReportCreateForm(
    CreateReportRequest Request,
    IReadOnlyList<IFormFile> Photos);

public sealed class ReportCreateFormParser(IValidator<CreateReportRequest> validator)
{
    private const int MaxPhotos = 5;

    public async Task<Result<ReportCreateForm>> ParseAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.HasFormContentType)
        {
            return ReportPartError("Report submission must use multipart form data.");
        }

        var form = await request.ReadFormAsync(cancellationToken);

        var reportJson = form["report"].ToString();
        if (string.IsNullOrWhiteSpace(reportJson))
        {
            return ReportPartError("Report data is required.");
        }

        CreateReportRequest? reportRequest;
        try
        {
            reportRequest = JsonSerializer.Deserialize<CreateReportRequest>(
                reportJson,
                ApiJson.SerializerOptions);
        }
        catch (JsonException)
        {
            return ReportPartError("Report data is invalid.");
        }

        if (reportRequest is null)
        {
            return ReportPartError("Report data is required.");
        }

        var validationResult = await validator.ValidateAsync(reportRequest, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => JsonNamingPolicy.CamelCase.ConvertName(group.Key),
                    group => group.Select(failure => failure.ErrorMessage).ToArray());

            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                ErrorCodes.ValidationFailed,
                errors);
        }

        var photoFiles = form.Files.GetFiles("photos");
        if (photoFiles.Count > MaxPhotos)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                ErrorCodes.ValidationFailed,
                new Dictionary<string, string[]>
                {
                    ["photos"] = [$"At most {MaxPhotos} photos are allowed."],
                });
        }

        var photoErrors = new Dictionary<string, string[]>();
        for (var index = 0; index < photoFiles.Count; index++)
        {
            if (photoFiles[index].Length == 0)
            {
                photoErrors[$"photos[{index}]"] = ["Photo file is required."];
            }
        }

        if (photoErrors.Count > 0)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                ErrorCodes.ValidationFailed,
                photoErrors);
        }

        return new ReportCreateForm(reportRequest, photoFiles);
    }

    private static ResultError ReportPartError(string message) =>
        ResultError.BadRequest(
            "Please correct the errors in the form.",
            ErrorCodes.ValidationFailed,
            new Dictionary<string, string[]>
            {
                ["report"] = [message],
            });
}
