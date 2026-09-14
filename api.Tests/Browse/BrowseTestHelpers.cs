using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Utilities.Reports;
using Amanah.Contracts.Requests.Reports;
using Amanah.Contracts.Responses.Browse;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Browse;

public static class BrowseTestHelpers
{
    private static int _defaultPublishedAtSequence;

    public static HttpClient CreateAnonymousClient(ApiWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

    public static async Task<(HttpResponseMessage Response, PaginatedResponse<PublicReportSummaryResponse>? Body)>
        GetBrowseAsync(HttpClient client, string? query = null)
    {
        var url = string.IsNullOrWhiteSpace(query)
            ? "/api/v1/reports"
            : $"/api/v1/reports?{query}";

        var response = await client.GetAsync(url);
        PaginatedResponse<PublicReportSummaryResponse>? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PaginatedResponse<PublicReportSummaryResponse>>()
            : null;

        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, PublicReportDetailResponse? Body)>
        GetPublicDetailAsync(HttpClient client, Guid reportId)
    {
        var response = await client.GetAsync($"/api/v1/reports/{reportId}/public");
        PublicReportDetailResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PublicReportDetailResponse>()
            : null;

        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, PublicReportDetailResponse? Body)>
        GetLostDetailAsync(HttpClient client, Guid reportId)
    {
        var response = await client.GetAsync($"/api/v1/lost/{reportId}");
        PublicReportDetailResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PublicReportDetailResponse>()
            : null;

        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, PublicReportDetailResponse? Body)>
        GetFoundDetailAsync(HttpClient client, Guid reportId)
    {
        var response = await client.GetAsync($"/api/v1/found/{reportId}");
        PublicReportDetailResponse? body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PublicReportDetailResponse>()
            : null;

        return (response, body);
    }

    public static async Task<Guid> SubmitAndPublishAsync(
        ReportTestContext context,
        CreateReportRequest request)
    {
        var (_, created) = await context.SubmitReportAsync(request);
        Assert.NotNull(created);
        await HttpTestHelpers.ApproveAsAdminAsync(context, created.Id);
        return created.Id;
    }

    public static async Task SetReportStatusAsync(
        ReportTestContext context,
        Guid reportId,
        ReportStatus status,
        DateTimeOffset? publishedAt = null)
    {
        var report = await context.DbContext.Reports.SingleAsync(item => item.Id == reportId);
        report.Status = status;
        report.PublishedAt = publishedAt ?? report.PublishedAt ?? DateTimeOffset.UtcNow;
        report.UpdatedAt = DateTimeOffset.UtcNow;
        await context.DbContext.SaveChangesAsync();
    }

    public static async Task<Guid> SeedReportAsync(
        ReportTestContext context,
        SeedReportOptions options)
    {
        var category = await context.DbContext.Categories
            .SingleAsync(item => item.Code == options.CategoryCode);
        var governorate = await context.DbContext.Governorates
            .SingleAsync(item => item.Code == options.GovernorateCode);

        var categoryFieldValues = options.CategoryFields.Values.ToList();
        var now = options.Timestamp ?? DateTimeOffset.UtcNow;
        var publishedAt = options.PublishedAt ?? DefaultPublishedAtForStatus(options.Status);

        var report = new Report
        {
            Id = Guid.NewGuid(),
            ReporterId = context.Session.User.Id,
            Type = options.Type,
            CategoryId = category.Id,
            Title = options.Title,
            Description = options.Description,
            DateLostOrFound = options.DateLostOrFound,
            GovernorateId = governorate.Id,
            AreaText = options.AreaText,
            HeldLocation = options.HeldLocation,
            HiddenDetail = options.HiddenDetail,
            Status = options.Status,
            HasReward = options.HasReward,
            RewardAmount = options.RewardAmount,
            PublishedAt = publishedAt,
            CreatedAt = now,
            UpdatedAt = now,
            NormalizedSearchText = SearchTextBuilder.Build(
                options.Title,
                options.Description,
                options.AreaText,
                categoryFieldValues),
        };

        context.DbContext.Reports.Add(report);

        foreach (var (fieldKey, value) in options.CategoryFields)
        {
            context.DbContext.CategoryFields.Add(new CategoryField
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                FieldKey = fieldKey,
                Value = value,
            });
        }

        if (options.WithPublicPhoto)
        {
            context.DbContext.ReportPhotos.Add(new ReportPhoto
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                StorageKey = $"reports/{report.Id}/photo.jpg",
                ThumbnailStorageKey = $"reports/{report.Id}/thumb.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 1024,
                SortOrder = 0,
            });
        }

        await context.DbContext.SaveChangesAsync();
        return report.Id;
    }

    private static DateTimeOffset? DefaultPublishedAtForStatus(ReportStatus status) =>
        status is ReportStatus.PendingReview or ReportStatus.Rejected
            ? null
            : NextDefaultPublishedAt();

    private static DateTimeOffset NextDefaultPublishedAt() =>
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            .AddSeconds(Interlocked.Increment(ref _defaultPublishedAtSequence));

    public sealed class SeedReportOptions
    {
        public ReportStatus Status { get; init; } = ReportStatus.Published;

        public ReportType Type { get; init; } = ReportType.Lost;

        public string CategoryCode { get; init; } = "phones";

        public string GovernorateCode { get; init; } = "cairo";

        public string Title { get; init; } = "Lost black iPhone";

        public string Description { get; init; } =
            "I lost my phone near Ramses station yesterday evening.";

        public DateOnly DateLostOrFound { get; init; } = new(2026, 9, 1);

        public string? AreaText { get; init; } = "Ramses station platform 2";

        public string? HeldLocation { get; init; }

        public string HiddenDetail { get; init; } = "Contains a photo of my family inside.";

        public DateTimeOffset? PublishedAt { get; init; }

        public DateTimeOffset? Timestamp { get; init; }

        public bool HasReward { get; init; }

        public int? RewardAmount { get; init; }

        public bool WithPublicPhoto { get; init; }

        public Dictionary<string, string> CategoryFields { get; init; } = new()
        {
            ["brand_model"] = "iPhone 14",
            ["colour"] = "black",
        };
    }
}
