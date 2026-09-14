using System.Text.Json;
using Amanah.Api.Models.Common;
using Amanah.Api.Models.Errors;
using Amanah.Contracts.Errors;
using FluentValidation;
using FluentValidation.Results;

namespace Amanah.Api.Services.Reports;

public static class ReportFormParserCore
{
    public const int MaxPhotos = 5;

    public static async Task<string> ReadReportJsonAsync(
        IFormCollection form,
        CancellationToken cancellationToken)
    {
        var reportJson = form["report"].ToString();
        if (!string.IsNullOrWhiteSpace(reportJson))
        {
            return reportJson;
        }

        // Angular FormData.append(name, new Blob(...)) is a file part, not a string field.
        var reportFile = form.Files.GetFile("report");
        if (reportFile is null || reportFile.Length == 0)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(reportFile.OpenReadStream());
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public static ResultError ReportPartError(string message) =>
        ResultError.BadRequest(
            "Please correct the errors in the form.",
            ErrorCodes.ValidationFailed,
            new Dictionary<string, string[]>
            {
                ["report"] = [message],
            });

    public static Dictionary<string, string[]> MapValidationErrors(ValidationResult validationResult) =>
        validationResult.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => JsonNamingPolicy.CamelCase.ConvertName(group.Key),
                group => group.Select(failure => failure.ErrorMessage).ToArray());

    public static ResultError? ValidatePhotos(IReadOnlyList<IFormFile> photoFiles)
    {
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

        if (photoErrors.Count == 0)
        {
            return null;
        }

        return ResultError.BadRequest(
            "Please correct the errors in the form.",
            ErrorCodes.ValidationFailed,
            photoErrors);
    }
}
