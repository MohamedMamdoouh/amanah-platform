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
    public async Task<Result<ReportCreateForm>> ParseAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.HasFormContentType)
        {
            return ReportFormParserCore.ReportPartError("Report submission must use multipart form data.");
        }

        var form = await request.ReadFormAsync(cancellationToken);

        var reportJson = await ReportFormParserCore.ReadReportJsonAsync(form, cancellationToken);
        if (string.IsNullOrWhiteSpace(reportJson))
        {
            return ReportFormParserCore.ReportPartError("Report data is required.");
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
            return ReportFormParserCore.ReportPartError("Report data is invalid.");
        }

        if (reportRequest is null)
        {
            return ReportFormParserCore.ReportPartError("Report data is required.");
        }

        var validationResult = await validator.ValidateAsync(reportRequest, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                ErrorCodes.ValidationFailed,
                ReportFormParserCore.MapValidationErrors(validationResult));
        }

        var photoFiles = form.Files.GetFiles("photos");
        var photoError = ReportFormParserCore.ValidatePhotos(photoFiles);
        if (photoError is not null)
        {
            return photoError;
        }

        return new ReportCreateForm(reportRequest, photoFiles);
    }
}
