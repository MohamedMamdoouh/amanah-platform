using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Amanah.Api.Data;
using Amanah.Api.Data.Seeds;
using Amanah.Api.Models.Common;
using Amanah.Api.Tests.Auth;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Reports;
using Amanah.Contracts.Responses.Auth;
using Amanah.Contracts.Responses.Reports;
using Amanah.Contracts.Responses.Uploads;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Reports;

public sealed class ReportTestContext : IAsyncDisposable
{
    private readonly AsyncServiceScope _scope;

    private ReportTestContext(
        HttpClient client,
        OtpSendTestContext authContext,
        AsyncServiceScope scope,
        AuthSessionResponse session)
    {
        Client = client;
        Auth = authContext;
        _scope = scope;
        Session = session;
        DbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public HttpClient Client { get; }

    public OtpSendTestContext Auth { get; }

    public AuthSessionResponse Session { get; }

    public AppDbContext DbContext { get; }

    public static async Task<ReportTestContext> CreateAsync(ApiWebApplicationFactory factory)
    {
        factory.CaptchaVerifier.ShouldSucceed = true;
        factory.SmsSender.ShouldThrow = false;
        factory.SmsSender.ShouldTimeout = false;
        factory.SmsSender.SentMessages.Clear();

        await using var setupScope = factory.Services.CreateAsyncScope();
        var setupContext = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await setupContext.Database.MigrateAsync();
        await setupContext.CategoryFields.ExecuteDeleteAsync();
        await setupContext.ReportPhotos.ExecuteDeleteAsync();
        await setupContext.Reports.ExecuteDeleteAsync();
        await setupContext.OtpCodes.ExecuteDeleteAsync();
        await setupContext.OtpSmsOutboxMessages.ExecuteDeleteAsync();
        await setupContext.RefreshTokens.ExecuteDeleteAsync();
        await setupContext.Users.ExecuteDeleteAsync();

        var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        var authContext = new OtpSendTestContext(
            client,
            factory.SmsSender,
            factory.CaptchaVerifier,
            scope);

        var (session, _) = await authContext.RegisterNewUserAsync();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        return new ReportTestContext(client, authContext, scope, session);
    }

    public async Task<(HttpResponseMessage Response, CreateReportResponse? Body)> SubmitReportAsync(
        CreateReportRequest request,
        IReadOnlyList<byte[]>? photoContents = null,
        string photoContentType = "image/jpeg") =>
        await SubmitReportAsync(request, photoContents, photoContentType, reportJsonAsFilePart: false);

    public async Task<(HttpResponseMessage Response, CreateReportResponse? Body)> SubmitReportAsync(
        CreateReportRequest request,
        IReadOnlyList<byte[]>? photoContents,
        string photoContentType,
        bool reportJsonAsFilePart) =>
        await SubmitReportJsonAsync(
            JsonSerializer.Serialize(request, ApiJson.SerializerOptions),
            photoContents,
            photoContentType,
            reportJsonAsFilePart);

    public async Task<(HttpResponseMessage Response, CreateReportResponse? Body)> SubmitReportJsonAsync(
        string reportJson,
        IReadOnlyList<byte[]>? photoContents = null,
        string photoContentType = "image/jpeg",
        bool reportJsonAsFilePart = false)
    {
        using var content = new MultipartFormDataContent();

        if (reportJsonAsFilePart)
        {
            var reportPart = new ByteArrayContent(Encoding.UTF8.GetBytes(reportJson));
            reportPart.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Add(reportPart, "report", "blob");
        }
        else
        {
            content.Add(new StringContent(reportJson, Encoding.UTF8, "application/json"), "report");
        }

        if (photoContents is not null)
        {
            for (var i = 0; i < photoContents.Count; i++)
            {
                var photoContent = new ByteArrayContent(photoContents[i]);
                photoContent.Headers.ContentType = new MediaTypeHeaderValue(photoContentType);
                content.Add(photoContent, "photos", $"photo{i}.jpg");
            }
        }

        var response = await Client.PostAsync("/api/v1/reports", content);
        CreateReportResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CreateReportResponse>()
            : null;

        return (response, body);
    }

    public async Task<(HttpResponseMessage Response, ReportPhotoPresignResponse? Body)> GetPhotoUrlAsync(Guid photoId)
    {
        var response = await Client.GetAsync($"/api/v1/uploads/report-photo/{photoId}/url");
        ReportPhotoPresignResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReportPhotoPresignResponse>()
            : null;

        return (response, body);
    }

    public async Task<(HttpResponseMessage Response, ReportListResponse? Body)> GetMineAsync(
        string? status = null)
    {
        var url = status is null
            ? "/api/v1/reports/mine"
            : $"/api/v1/reports/mine?status={status}";

        var response = await Client.GetAsync(url);
        ReportListResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReportListResponse>()
            : null;

        return (response, body);
    }

    public async Task<(HttpResponseMessage Response, ReportDetailResponse? Body)> GetReportAsync(Guid id)
    {
        var response = await Client.GetAsync($"/api/v1/reports/{id}");
        ReportDetailResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReportDetailResponse>()
            : null;

        return (response, body);
    }

    public async Task<HttpResponseMessage> WithdrawReportAsync(Guid id, WithdrawReportRequest? request = null)
    {
        return await Client.PostAsJsonAsync(
            $"/api/v1/reports/{id}/withdraw",
            request ?? new WithdrawReportRequest());
    }

    public async Task<ApiError?> ReadErrorAsync(HttpResponseMessage response) =>
        await Auth.ReadErrorAsync(response);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();
}
