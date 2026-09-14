using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Data.Extensions;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Storage;
using Amanah.Api.Utilities.Common;
using Amanah.Api.Utilities.Reports;
using Amanah.Contracts.Requests.Browse;
using Amanah.Contracts.Responses.Browse;
using Amanah.Contracts.Responses.Reports;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Browse;

public sealed class BrowseService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage)
{
    public async Task<Result<PaginatedResponse<PublicReportSummaryResponse>>> ListReportsAsync(
        BrowseReportsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page;
        var pageSize = query.PageSize;

        IQueryable<Report> reportsQuery = dbContext.Reports
            .AsNoTracking()
            .WithBrowseSummaryIncludes()
            .Where(report =>
                report.Status == ReportStatus.Published
                || report.Status == ReportStatus.ClaimInProgress);

        var terms = ArabicNormalizer.BuildSearchTerms(query.Q ?? string.Empty);
        reportsQuery = reportsQuery.WhereMatchesAllSearchTerms(terms);

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var categoryCode = query.Category.Trim();
            reportsQuery = reportsQuery.Where(report => report.Category.Code == categoryCode);
        }

        if (!string.IsNullOrWhiteSpace(query.Governorate))
        {
            var governorateCode = query.Governorate.Trim();
            reportsQuery = reportsQuery.Where(report => report.Governorate.Code == governorateCode);
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            if (!ReportApiStrings.TryParseType(query.Type, out var reportType))
            {
                return ResultError.BadRequest(
                    "Please correct the errors in the form.",
                    errors: new Dictionary<string, string[]>
                    {
                        ["type"] = ["Type must be lost or found."],
                    });
            }

            reportsQuery = reportsQuery.Where(report => report.Type == reportType);
        }

        if (query.DateFrom.HasValue)
        {
            reportsQuery = reportsQuery.Where(report => report.DateLostOrFound >= query.DateFrom.Value);
        }

        if (query.DateTo.HasValue)
        {
            reportsQuery = reportsQuery.Where(report => report.DateLostOrFound <= query.DateTo.Value);
        }

        var totalCount = await reportsQuery.CountAsync(cancellationToken);

        var reports = await reportsQuery
            .OrderByDescending(report => report.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Pagination.Create(
            [.. reports.Select(ToPublicSummary)],
            page,
            pageSize,
            totalCount);
    }

    public Task<Result<PublicReportDetailResponse>> GetPublicDetailAsync(
        Guid reportId,
        CancellationToken cancellationToken = default) =>
        GetPublicDetailAsync(reportId, expectedType: null, cancellationToken);

    public Task<Result<PublicReportDetailResponse>> GetLostDetailAsync(
        Guid reportId,
        CancellationToken cancellationToken = default) =>
        GetPublicDetailAsync(reportId, ReportType.Lost, cancellationToken);

    public Task<Result<PublicReportDetailResponse>> GetFoundDetailAsync(
        Guid reportId,
        CancellationToken cancellationToken = default) =>
        GetPublicDetailAsync(reportId, ReportType.Found, cancellationToken);

    private async Task<Result<PublicReportDetailResponse>> GetPublicDetailAsync(
        Guid reportId,
        ReportType? expectedType,
        CancellationToken cancellationToken)
    {
        var report = await dbContext.Reports
            .AsNoTracking()
            .WithPublicDetailIncludes()
            .SingleOrDefaultAsync(report => report.Id == reportId, cancellationToken);

        if (report is null)
        {
            return ResultError.NotFound("Report not found.");
        }

        if (expectedType.HasValue && report.Type != expectedType.Value)
        {
            return ResultError.NotFound("Report not found.");
        }

        return ResolvePublicAccess(report.Status) switch
        {
            PublicAccessOutcome.NotFound => ResultError.NotFound("Report not found."),
            PublicAccessOutcome.Unavailable => ResultError.Gone("This report is no longer available."),
            _ => ToPublicDetail(report),
        };
    }

    private PublicReportDetailResponse ToPublicDetail(Report report) =>
        new()
        {
            Id = report.Id,
            Type = ReportApiStrings.ToType(report.Type),
            Status = ReportApiStrings.ToStatus(report.Status),
            Title = report.Title,
            CategoryCode = report.Category.Code,
            GovernorateCode = report.Governorate.Code,
            PublishedAt = report.PublishedAt,
            Description = report.Description,
            DateLostOrFound = report.DateLostOrFound,
            AreaText = report.AreaText,
            HeldLocation = report.HeldLocation,
            HasReward = report.HasReward,
            RewardAmount = report.RewardAmount,
            ReporterDisplayName = report.Reporter.DisplayName ?? string.Empty,
            CategoryFields = report.CategoryFields
                .OrderBy(field => field.FieldKey)
                .ToDictionary(field => field.FieldKey, field => field.Value),
            Photos = report.Photos
                .OrderBy(photo => photo.SortOrder)
                .Select(photo => new ReportPhotoResponse
                {
                    Id = photo.Id,
                    ThumbnailUrl = ReportPhotoUrlMapper.ToThumbnailUrl(
                        bucketStorage,
                        report.Category.PhotosPrivate,
                        photo.ThumbnailStorageKey),
                    SortOrder = photo.SortOrder,
                })
                .ToList(),
        };

    private static PublicAccessOutcome ResolvePublicAccess(ReportStatus status) =>
        status switch
        {
            ReportStatus.Published or ReportStatus.ClaimInProgress => PublicAccessOutcome.Allowed,
            ReportStatus.Resolved or ReportStatus.Withdrawn or ReportStatus.RemovedByAdmin
                => PublicAccessOutcome.Unavailable,
            _ => PublicAccessOutcome.NotFound,
        };

    private enum PublicAccessOutcome
    {
        Allowed,
        NotFound,
        Unavailable,
    }

    private PublicReportSummaryResponse ToPublicSummary(Report report)
    {
        var firstPhoto = report.Photos
            .OrderBy(photo => photo.SortOrder)
            .FirstOrDefault();

        return new PublicReportSummaryResponse
        {
            Id = report.Id,
            Type = ReportApiStrings.ToType(report.Type),
            Status = ReportApiStrings.ToStatus(report.Status),
            Title = report.Title,
            CategoryCode = report.Category.Code,
            GovernorateCode = report.Governorate.Code,
            PublishedAt = report.PublishedAt,
            HasReward = report.HasReward,
            RewardAmount = report.RewardAmount,
            ReporterDisplayName = report.Reporter.DisplayName ?? string.Empty,
            ThumbnailUrl = ReportPhotoUrlMapper.ToThumbnailUrl(
                bucketStorage,
                report.Category.PhotosPrivate,
                firstPhoto?.ThumbnailStorageKey),
            AreaText = report.AreaText,
        };
    }
}
