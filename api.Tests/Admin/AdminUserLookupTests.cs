using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amanah.Api.Tests.Auth;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Admin;

public class AdminUserLookupTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Search_by_display_name_returns_matches_without_phone_numbers()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var uniqueFragment = $"Lookup{Random.Shared.Next(1000, 9999)}";
        var otherUser = TestAuthHelpers.CreateUser(
            context.Auth.PasswordHasher,
            $"+2010{Random.Shared.Next(10000000, 99999999)}",
            $"{uniqueFragment} User");

        context.DbContext.Users.Add(otherUser);
        await context.DbContext.SaveChangesAsync();

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync(
            $"/api/v1/admin/users?searchBy={AdminUserSearchBy.Name}&query={Uri.EscapeDataString(uniqueFragment)}");
        var body = await response.Content.ReadFromJsonAsync<AdminUserListResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains(body.Items, item => item.Id == otherUser.Id);
        Assert.All(body.Items, item => Assert.DoesNotContain("+20", item.DisplayName));

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        foreach (var item in document.RootElement.GetProperty("items").EnumerateArray())
        {
            Assert.False(item.TryGetProperty("normalizedPhone", out _));
            Assert.False(item.TryGetProperty("phone", out _));
        }
    }

    [Fact]
    public async Task Search_by_normalized_phone_returns_exact_user()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var loginPhone = $"010{Random.Shared.Next(10000000, 99999999)}";
        var normalizedPhone = $"+20{loginPhone[1..]}";
        var user = TestAuthHelpers.CreateUser(
            context.Auth.PasswordHasher,
            normalizedPhone,
            "Phone Lookup Target");

        context.DbContext.Users.Add(user);
        await context.DbContext.SaveChangesAsync();

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync(
            $"/api/v1/admin/users?searchBy={AdminUserSearchBy.Phone}&query={Uri.EscapeDataString(normalizedPhone)}");
        var body = await response.Content.ReadFromJsonAsync<AdminUserListResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.Equal(user.Id, body.Items[0].Id);
    }

    [Fact]
    public async Task Search_by_name_with_phone_shaped_query_does_not_exact_match_phone()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var loginPhone = $"010{Random.Shared.Next(10000000, 99999999)}";
        var normalizedPhone = $"+20{loginPhone[1..]}";
        var user = TestAuthHelpers.CreateUser(
            context.Auth.PasswordHasher,
            normalizedPhone,
            "Regular User");

        context.DbContext.Users.Add(user);
        await context.DbContext.SaveChangesAsync();

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync(
            $"/api/v1/admin/users?searchBy={AdminUserSearchBy.Name}&query={Uri.EscapeDataString(loginPhone)}");
        var body = await response.Content.ReadFromJsonAsync<AdminUserListResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task Search_by_phone_with_invalid_number_returns_bad_request()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync(
            $"/api/v1/admin/users?searchBy={AdminUserSearchBy.Phone}&query=not-a-phone");
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.InvalidPhone, error?.Code);
        Assert.NotNull(error?.Errors);
        Assert.Contains("query", error.Errors.Keys);
    }

    [Fact]
    public async Task Search_without_search_by_returns_bad_request()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync("/api/v1/admin/users?query=ahmed");
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error?.Code);
        Assert.NotNull(error?.Errors);
        Assert.Contains("searchBy", error.Errors.Keys);
    }

    [Fact]
    public async Task Search_with_invalid_search_by_returns_bad_request()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync("/api/v1/admin/users?searchBy=email&query=ahmed");
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error?.Code);
        Assert.NotNull(error?.Errors);
        Assert.Contains("searchBy", error.Errors.Keys);
    }

    [Fact]
    public async Task Get_user_detail_includes_reports_count_and_ban_fields()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        Assert.NotEqual(Guid.Empty, reportId);

        var userId = context.Session.User.Id;
        var expectedReportsCount = await context.DbContext.Reports
            .AsNoTracking()
            .CountAsync(report => report.ReporterId == userId);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var detailBeforeBan = await context.Client.GetAsync($"/api/v1/admin/users/{userId}");
        var detailBody = await detailBeforeBan.Content.ReadFromJsonAsync<AdminUserDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, detailBeforeBan.StatusCode);
        Assert.NotNull(detailBody);
        Assert.Equal(expectedReportsCount, detailBody.ReportsCount);
        Assert.Equal(context.Session.User.DisplayName, detailBody.DisplayName);
        Assert.False(detailBody.IsBanned);
        Assert.Null(detailBody.BanReason);
        Assert.Null(detailBody.BannedAt);

        var banResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{userId}/ban",
            new BanUserRequest { Reason = "Lookup test ban" });
        Assert.Equal(HttpStatusCode.OK, banResponse.StatusCode);

        var detailAfterBan = await context.Client.GetAsync($"/api/v1/admin/users/{userId}");
        var bannedBody = await detailAfterBan.Content.ReadFromJsonAsync<AdminUserDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, detailAfterBan.StatusCode);
        Assert.NotNull(bannedBody);
        Assert.True(bannedBody.IsBanned);
        Assert.Equal("Lookup test ban", bannedBody.BanReason);
        Assert.NotNull(bannedBody.BannedAt);
    }

    [Fact]
    public async Task Search_without_query_returns_bad_request()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync(
            $"/api/v1/admin/users?searchBy={AdminUserSearchBy.Name}&query=");
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error?.Code);
        Assert.NotNull(error?.Errors);
        Assert.Contains("query", error.Errors.Keys);
    }

    [Fact]
    public async Task Get_user_detail_as_non_admin_returns_forbidden()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);

        var response = await context.Client.GetAsync($"/api/v1/admin/users/{context.Session.User.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_missing_user_detail_returns_not_found()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var response = await context.Client.GetAsync($"/api/v1/admin/users/{Guid.NewGuid()}");
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, error?.Code);
    }
}
