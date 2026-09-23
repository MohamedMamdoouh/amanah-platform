using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amanah.Api.Models.Common;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Auth;
using Amanah.Contracts.Responses.Auth;

namespace Amanah.Api.Tests.Infrastructure;

public static class HttpTestHelpers
{
    public const string AdminPhone = "01011111111";
    public const string AdminPassword = "AdminPass123";

    public static async Task<ApiError?> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return System.Text.Json.JsonSerializer.Deserialize<ApiError>(body, ApiJson.SerializerOptions);
    }

    public static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>();

    public static async Task LoginAsAdminAsync(ReportTestContext context)
    {
        var (loginResponse, adminSession) = await context.Auth.LoginAsync(AdminPhone, AdminPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(adminSession);

        context.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
    }

    public static async Task LoginAsAdminAsync(HttpClient client)
    {
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                channel = AuthIdentifierChannels.Phone,
                identifier = AdminPhone,
                password = AdminPassword,
            });
        var adminSession = await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(adminSession);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
    }

    public static async Task ApproveAsAdminAsync(ReportTestContext context, Guid reportId)
    {
        await LoginAsAdminAsync(context);

        var approveResponse = await context.Client.PostAsync(
            $"/api/v1/admin/moderation/reports/{reportId}/approve",
            null);
        Assert.Equal(HttpStatusCode.NoContent, approveResponse.StatusCode);

        context.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", context.Session.AccessToken);
    }
}
