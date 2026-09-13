using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Auth;

namespace Amanah.Api.Tests.Resolution;

public static class ResolutionTestHelpers
{
    public sealed record ApprovedClaimScenario(
        Guid ReportId,
        Guid ClaimId,
        AuthSessionResponse ClaimantSession,
        ReportTestContext ReporterContext);

    public static async Task<ApprovedClaimScenario> CreateApprovedClaimScenarioAsync(
        ReportTestContext reporterContext,
        bool foundReport = false)
    {
        var reportId = foundReport
            ? await ClaimTestHelpers.PublishFoundReportAsync(reporterContext)
            : await ClaimTestHelpers.PublishLostReportAsync(reporterContext);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(reporterContext);
        ClaimTestHelpers.Authenticate(reporterContext.Client, claimantSession.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(reporterContext.Client, reportId);
        if (submitted is null)
        {
            throw new InvalidOperationException("Claim submission failed.");
        }

        ClaimTestHelpers.AuthenticateReporter(reporterContext.Client, reporterContext);
        var approveResponse = await ClaimTestHelpers.ApproveClaimAsync(reporterContext.Client, submitted.Id);
        if (!approveResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Claim approval failed: {approveResponse.StatusCode}");
        }

        return new ApprovedClaimScenario(reportId, submitted.Id, claimantSession, reporterContext);
    }

    public static void AuthenticateReporter(HttpClient client, ReportTestContext context) =>
        ClaimTestHelpers.AuthenticateReporter(client, context);

    public static void AuthenticateClaimant(HttpClient client, AuthSessionResponse session) =>
        ClaimTestHelpers.Authenticate(client, session.AccessToken);

    public static Task<HttpResponseMessage> ConfirmResolutionAsync(HttpClient client, Guid claimId) =>
        client.PostAsync($"/api/v1/claims/{claimId}/confirm-resolution", null);

    public static Task<HttpResponseMessage> CancelClaimAsync(HttpClient client, Guid claimId) =>
        client.PostAsync($"/api/v1/claims/{claimId}/cancel", null);

    public static async Task<ApiError?> ReadErrorAsync(HttpResponseMessage response) =>
        await ClaimTestHelpers.ReadErrorAsync(response);
}
