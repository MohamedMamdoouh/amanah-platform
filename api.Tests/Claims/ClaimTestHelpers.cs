using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Auth;
using Amanah.Api.Tests.Browse;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Claims;
using Amanah.Contracts.Responses.Auth;
using Amanah.Contracts.Responses.Claims;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Claims;

public static class ClaimTestHelpers
{
    public const string ValidAnswer =
        "Black leather wallet with a red stripe inside the main pocket.";

    public static async Task<(HttpResponseMessage Response, SubmitClaimResponse? Body)> SubmitClaimAsync(
        HttpClient client,
        Guid reportId,
        SubmitClaimRequest request)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/reports/{reportId}/claims", request);
        SubmitClaimResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SubmitClaimResponse>()
            : null;

        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, SubmitClaimResponse? Body)> SubmitClaimAsync(
        HttpClient client,
        Guid reportId,
        string submittedAnswer = ValidAnswer) =>
        await SubmitClaimAsync(client, reportId, new SubmitClaimRequest
        {
            SubmittedAnswer = submittedAnswer,
        });

    public static async Task<ApiError?> ReadErrorAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ApiError>();

    public static async Task<Guid> PublishLostReportAsync(ReportTestContext context) =>
        await BrowseTestHelpers.SubmitAndPublishAsync(
            context,
            TestReportHelpers.BuildValidLostRequest());

    public static async Task<Guid> PublishFoundReportAsync(ReportTestContext context) =>
        await BrowseTestHelpers.SubmitAndPublishAsync(
            context,
            TestReportHelpers.BuildValidFoundRequest());

    public static async Task<AuthSessionResponse> CreateAndLoginClaimantAsync(ReportTestContext context)
    {
        var loginPhone = $"010{Random.Shared.Next(10000000, 99999999)}";
        var normalizedPhone = $"+20{loginPhone[1..]}";
        var claimant = TestAuthHelpers.CreateUser(
            context.Auth.PasswordHasher,
            normalizedPhone,
            "Claimant");

        context.DbContext.Users.Add(claimant);
        await context.DbContext.SaveChangesAsync();

        var (loginResponse, session) = await context.Auth.LoginAsync(
            loginPhone,
            TestAuthHelpers.DefaultPassword);

        if (loginResponse.StatusCode != System.Net.HttpStatusCode.OK || session is null)
        {
            throw new InvalidOperationException($"Claimant login failed: {loginResponse.StatusCode}");
        }

        return session;
    }

    public static void Authenticate(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public static async Task SeedRejectedClaimAsync(
        ReportTestContext context,
        Guid reportId,
        Guid claimantId,
        bool countsAsFailure,
        int attemptNumber)
    {
        context.DbContext.Claims.Add(new Claim
        {
            Id = Guid.NewGuid(),
            ReportId = reportId,
            ClaimantId = claimantId,
            Status = ClaimStatus.Rejected,
            SubmittedAnswer = ValidAnswer,
            SubmittedAt = DateTimeOffset.UtcNow.AddHours(-1),
            AttemptNumber = attemptNumber,
            CountsAsFailure = countsAsFailure,
            ReviewedAt = DateTimeOffset.UtcNow,
        });

        await context.DbContext.SaveChangesAsync();
    }

    public static async Task<int> CountClaimsAsync(ReportTestContext context, Guid reportId, Guid claimantId) =>
        await context.DbContext.Claims
            .CountAsync(claim => claim.ReportId == reportId && claim.ClaimantId == claimantId);
}
