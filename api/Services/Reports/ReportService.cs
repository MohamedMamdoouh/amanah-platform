using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Observability;
using Amanah.Api.Services.Storage;
using Amanah.Api.Utilities.Common;
using Amanah.Api.Utilities.Reports;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Reports;
using Amanah.Contracts.Responses.Reports;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Reports;

public sealed class ReportService(
    AppDbContext dbContext,
    IReportQuotaService quotaService,
    ReportPhotoAttachService photoAttachService,
    IBucketStorage bucketStorage,
    TimeProvider timeProvider,
    AppMetrics metrics)
{
    private const int MaxPhotos = 5;
    private const int MaxResubmissions = 3;

    private static readonly ReportStatus[] Phase02ReadableStatuses =
    [
        ReportStatus.PendingReview,
        ReportStatus.Rejected,
    ];

    public async Task<Result<CreateReportResponse>> CreateAsync(
        Guid reporterId,
        CreateReportRequest request,
        IReadOnlyList<IFormFile> photos,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRequest(request);
        if (!TryParseReportType(normalized.Type, out var reportType))
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                errors: new Dictionary<string, string[]>
                {
                    ["type"] = ["Report type must be lost or found."],
                });
        }

        var category = await dbContext.Categories
            .AsNoTracking()
            .Include(category => category.FieldDefinitions)
            .SingleOrDefaultAsync(
                category => category.Code == normalized.CategoryCode,
                cancellationToken);

        var governorate = await dbContext.Governorates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                governorate => governorate.Code == normalized.GovernorateCode,
                cancellationToken);

        if (category is null || !category.Active)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                errors: new Dictionary<string, string[]>
                {
                    ["categoryCode"] = ["Category is invalid."],
                });
        }

        if (governorate is null)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                errors: new Dictionary<string, string[]>
                {
                    ["governorateCode"] = ["Governorate is invalid."],
                });
        }

        var dateError = ReportDateValidator.ValidateDateLostOrFound(normalized.DateLostOrFound);
        var errors = MergeErrors(
            ReportContentValidator.Validate(
                reportType == ReportType.Found,
                normalized.Title,
                normalized.Description,
                normalized.HiddenDetail,
                normalized.AreaText,
                normalized.HasReward,
                normalized.RewardAmount,
                normalized.HeldLocation),
            dateError is null
                ? []
                : new Dictionary<string, string[]>
                {
                    [ReportDateValidator.FieldName] = [dateError],
                },
            CategoryFieldValidator.Validate(
                category.FieldDefinitions,
                normalized.CategoryFields),
            ContactInfoValidator.ScanPublicFields(
                normalized.Title,
                normalized.Description,
                normalized.AreaText,
                normalized.HeldLocation,
                normalized.CategoryFields));

        if (errors.Count > 0)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                errors: errors);
        }

        var quotaResult = await quotaService.CheckNewSubmissionAsync(reporterId, cancellationToken: cancellationToken);
        if (quotaResult.Kind == QuotaFailureKind.DailyQuota)
        {
            return ResultError.TooManyRequests(
                "You have reached the daily limit of 3 new reports. Try again after midnight (Cairo time).",
                quotaResult.RetryAfterSeconds ?? 1,
                ErrorCodes.ReportDailyQuota);
        }

        if (quotaResult.Kind == QuotaFailureKind.OpenCap)
        {
            return ResultError.Create(
                ErrorCodes.ReportOpenCap,
                "You already have 5 open reports. Close or withdraw one before submitting a new report.",
                StatusCodes.Status429TooManyRequests);
        }

        return await CreateReportCoreAsync(
            reporterId,
            normalized,
            reportType,
            category,
            governorate,
            photos,
            cancellationToken);
    }

    private async Task<Result<CreateReportResponse>> CreateReportCoreAsync(
        Guid reporterId,
        NormalizedCreateReportRequest normalized,
        ReportType reportType,
        Category category,
        Governorate governorate,
        IReadOnlyList<IFormFile> photos,
        CancellationToken cancellationToken)
    {
        var categoryFieldValues = new List<string>();
        var now = timeProvider.GetUtcNow();
        var report = new Report
        {
            Id = Guid.NewGuid(),
            ReporterId = reporterId,
            Type = reportType,
            CategoryId = category.Id,
            GovernorateId = governorate.Id,
            Title = normalized.Title,
            Description = normalized.Description,
            DateLostOrFound = normalized.DateLostOrFound,
            AreaText = normalized.AreaText,
            HeldLocation = reportType == ReportType.Found ? normalized.HeldLocation : null,
            Status = ReportStatus.PendingReview,
            HasReward = normalized.HasReward,
            RewardAmount = normalized.HasReward ? normalized.RewardAmount : null,
            HiddenDetail = normalized.HiddenDetail,
            ResubmissionCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var definition in category.FieldDefinitions)
        {
            if (!normalized.CategoryFields.TryGetValue(definition.FieldKey, out var value)
                || value.Length == 0)
            {
                continue;
            }

            categoryFieldValues.Add(value);
            report.CategoryFields.Add(new CategoryField
            {
                Id = Guid.NewGuid(),
                FieldKey = definition.FieldKey,
                Value = value,
            });
        }

        report.NormalizedSearchText = SearchTextBuilder.Build(
            normalized.Title,
            normalized.Description,
            normalized.AreaText,
            categoryFieldValues);

        var photoError = await photoAttachService.AttachAsync(
            report,
            photos,
            category.PhotosPrivate,
            cancellationToken);

        if (photoError is not null)
        {
            return photoError;
        }

        dbContext.Reports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);

        metrics.RecordReportSubmitted();

        return new CreateReportResponse
        {
            Id = report.Id,
            Status = ToApiStatus(report.Status),
        };
    }

    public async Task<Result<ReportListResponse>> GetMineAsync(
        Guid reporterId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(status, "closed", StringComparison.OrdinalIgnoreCase))
        {
            var closedReports = await dbContext.Reports
                .AsNoTracking()
                .Include(report => report.Category)
                .Include(report => report.Governorate)
                .Where(report => report.ReporterId == reporterId
                    && ClosedStatuses.Contains(report.Status))
                .OrderByDescending(report => report.CreatedAt)
                .ToListAsync(cancellationToken);

            return new ReportListResponse
            {
                Items = closedReports.Select(ToSummary).ToList(),
            };
        }

        var statusFilter = ParseStatusFilter(status);
        if (!string.IsNullOrWhiteSpace(status) && statusFilter is null)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                errors: new Dictionary<string, string[]>
                {
                    ["status"] = ["Status filter is invalid."],
                });
        }

        var query = dbContext.Reports
            .AsNoTracking()
            .Include(report => report.Category)
            .Include(report => report.Governorate)
            .Where(report => report.ReporterId == reporterId);

        if (statusFilter is ReportStatus filter)
        {
            query = query.Where(report => report.Status == filter);
        }

        var reports = await query
            .OrderByDescending(report => report.CreatedAt)
            .ToListAsync(cancellationToken);

        return new ReportListResponse
        {
            Items = reports.Select(ToSummary).ToList(),
        };
    }

    public async Task<Result<ReportDetailResponse>> GetByIdAsync(
        Guid reportId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.Reports
            .AsNoTracking()
            .Include(report => report.Category)
            .Include(report => report.Governorate)
            .Include(report => report.CategoryFields)
            .Include(report => report.Photos)
            .SingleOrDefaultAsync(report => report.Id == reportId, cancellationToken);

        if (report is null)
        {
            return ResultError.NotFound("Report not found.");
        }

        var isReporter = report.ReporterId == userId;
        var isAdmin = role == UserRole.Admin;

        if (!isReporter && !isAdmin)
        {
            return ResultError.NotFound("Report not found.");
        }

        if (!isReporter && !Phase02ReadableStatuses.Contains(report.Status))
        {
            return ResultError.NotFound("Report not found.");
        }

        ModerationAction? latestRejection = null;
        if (report.Status == ReportStatus.Rejected)
        {
            latestRejection = await dbContext.ModerationActions
                .AsNoTracking()
                .Where(action => action.ReportId == reportId
                    && action.Decision == ModerationDecision.Reject)
                .OrderByDescending(action => action.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return isReporter
            ? ToReporterDetail(report, latestRejection)
            : ToAdminDetail(report, latestRejection);
    }

    public async Task<Result> WithdrawAsync(
        Guid reportId,
        Guid reporterId,
        WithdrawReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.Reports
            .SingleOrDefaultAsync(
                report => report.Id == reportId && report.ReporterId == reporterId,
                cancellationToken);

        if (report is null)
        {
            return ResultError.NotFound("Report not found.");
        }

        if (report.Status != ReportStatus.PendingReview)
        {
            return ResultError.Conflict("Only pending reports can be withdrawn.");
        }

        report.Status = ReportStatus.Withdrawn;
        report.WithdrawalReason = request.Reason;
        report.UpdatedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> UpdateAsync(
        Guid reportId,
        Guid reporterId,
        UpdateReportRequest request,
        IReadOnlyList<IFormFile> photos,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.Reports
            .Include(report => report.Category)
            .Include(report => report.CategoryFields)
            .Include(report => report.Photos)
            .SingleOrDefaultAsync(
                report => report.Id == reportId && report.ReporterId == reporterId,
                cancellationToken);

        if (report is null)
        {
            return ResultError.NotFound("Report not found.");
        }

        if (report.Status != ReportStatus.Rejected)
        {
            return ResultError.Conflict("Only rejected reports can be edited.");
        }

        var normalized = NormalizeUpdateRequest(request);
        var validation = await ValidateEditableContentAsync(
            report.Type,
            normalized,
            cancellationToken);

        if (validation.Error is not null)
        {
            return validation.Error;
        }

        var category = validation.Category!;
        var governorate = validation.Governorate!;
        var previousPhotosPrivate = report.Category.PhotosPrivate;
        var now = timeProvider.GetUtcNow();

        report.CategoryId = category.Id;
        report.GovernorateId = governorate.Id;
        report.Title = normalized.Title;
        report.Description = normalized.Description;
        report.DateLostOrFound = normalized.DateLostOrFound;
        report.AreaText = normalized.AreaText;
        report.HeldLocation = report.Type == ReportType.Found ? normalized.HeldLocation : null;
        report.HasReward = normalized.HasReward;
        report.RewardAmount = normalized.HasReward ? normalized.RewardAmount : null;
        report.HiddenDetail = normalized.HiddenDetail;
        report.UpdatedAt = now;

        ReplaceCategoryFields(report, category, normalized);

        report.NormalizedSearchText = SearchTextBuilder.Build(
            normalized.Title,
            normalized.Description,
            normalized.AreaText,
            report.CategoryFields.Select(field => field.Value).ToList());

        if (photos.Count > 0 && report.Photos.Count + photos.Count > MaxPhotos)
        {
            return ResultError.BadRequest(
                "Please correct the errors in the form.",
                errors: new Dictionary<string, string[]>
                {
                    ["photos"] = [$"At most {MaxPhotos} photos are allowed."],
                });
        }

        IReadOnlyList<string> stalePhotoKeys = [];
        if (previousPhotosPrivate != category.PhotosPrivate)
        {
            var (privacyError, keysToDelete) = await photoAttachService.SyncPhotoPrivacyAsync(
                report,
                category.PhotosPrivate,
                cancellationToken);

            if (privacyError is not null)
            {
                return privacyError;
            }

            stalePhotoKeys = keysToDelete;
        }

        if (photos.Count > 0)
        {
            var photoError = await photoAttachService.AttachAsync(
                report,
                photos,
                category.PhotosPrivate,
                cancellationToken);

            if (photoError is not null)
            {
                return photoError;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (stalePhotoKeys.Count > 0)
        {
            await bucketStorage.DeleteManyAsync(stalePhotoKeys, cancellationToken);
        }

        return Result.Ok();
    }

    public async Task<Result> ResubmitAsync(
        Guid reportId,
        Guid reporterId,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.Reports
            .Include(report => report.Category)
            .Include(report => report.Governorate)
            .Include(report => report.CategoryFields)
            .SingleOrDefaultAsync(
                report => report.Id == reportId && report.ReporterId == reporterId,
                cancellationToken);

        if (report is null)
        {
            return ResultError.NotFound("Report not found.");
        }

        if (report.Status != ReportStatus.Rejected)
        {
            return ResultError.Conflict("Only rejected reports can be resubmitted.");
        }

        if (report.ResubmissionCount >= MaxResubmissions)
        {
            return ResultError.Conflict(
                "This report has reached the maximum number of resubmissions.",
                ErrorCodes.ReportResubmitCap);
        }

        var normalized = BuildNormalizedFromReport(report);
        var validation = await ValidateEditableContentAsync(
            report.Type,
            normalized,
            cancellationToken);

        if (validation.Error is not null)
        {
            return validation.Error;
        }

        var quotaResult = await quotaService.CheckNewSubmissionAsync(
            reporterId,
            isResubmission: true,
            cancellationToken: cancellationToken);

        if (quotaResult.Kind != QuotaFailureKind.None)
        {
            return ResultError.Conflict("Resubmission could not be completed.");
        }

        var now = timeProvider.GetUtcNow();
        report.Status = ReportStatus.PendingReview;
        report.ResubmissionCount += 1;
        report.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private async Task<(ResultError? Error, Category? Category, Governorate? Governorate)> ValidateEditableContentAsync(
        ReportType reportType,
        NormalizedCreateReportRequest normalized,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .AsNoTracking()
            .Include(category => category.FieldDefinitions)
            .SingleOrDefaultAsync(
                category => category.Code == normalized.CategoryCode,
                cancellationToken);

        var governorate = await dbContext.Governorates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                governorate => governorate.Code == normalized.GovernorateCode,
                cancellationToken);

        if (category is null || !category.Active)
        {
            return (ResultError.BadRequest(
                "Please correct the errors in the form.",
                errors: new Dictionary<string, string[]>
                {
                    ["categoryCode"] = ["Category is invalid."],
                }), null, null);
        }

        if (governorate is null)
        {
            return (ResultError.BadRequest(
                "Please correct the errors in the form.",
                errors: new Dictionary<string, string[]>
                {
                    ["governorateCode"] = ["Governorate is invalid."],
                }), null, null);
        }

        var dateError = ReportDateValidator.ValidateDateLostOrFound(normalized.DateLostOrFound);
        var errors = MergeErrors(
            ReportContentValidator.Validate(
                reportType == ReportType.Found,
                normalized.Title,
                normalized.Description,
                normalized.HiddenDetail,
                normalized.AreaText,
                normalized.HasReward,
                normalized.RewardAmount,
                normalized.HeldLocation),
            dateError is null
                ? []
                : new Dictionary<string, string[]>
                {
                    [ReportDateValidator.FieldName] = [dateError],
                },
            CategoryFieldValidator.Validate(
                category.FieldDefinitions,
                normalized.CategoryFields),
            ContactInfoValidator.ScanPublicFields(
                normalized.Title,
                normalized.Description,
                normalized.AreaText,
                normalized.HeldLocation,
                normalized.CategoryFields));

        if (errors.Count > 0)
        {
            return (ResultError.BadRequest(
                "Please correct the errors in the form.",
                errors: errors), null, null);
        }

        return (null, category, governorate);
    }

    private static NormalizedCreateReportRequest BuildNormalizedFromReport(Report report)
    {
        var categoryFields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in report.CategoryFields)
        {
            categoryFields[field.FieldKey] = field.Value;
        }

        return new NormalizedCreateReportRequest
        {
            Type = ToApiType(report.Type),
            CategoryCode = report.Category.Code,
            Title = report.Title,
            Description = report.Description,
            DateLostOrFound = report.DateLostOrFound,
            GovernorateCode = report.Governorate.Code,
            AreaText = report.AreaText,
            HeldLocation = report.HeldLocation,
            HasReward = report.HasReward,
            RewardAmount = report.RewardAmount,
            HiddenDetail = report.HiddenDetail,
            CategoryFields = categoryFields,
        };
    }

    private void ReplaceCategoryFields(
        Report report,
        Category category,
        NormalizedCreateReportRequest normalized)
    {
        if (report.CategoryFields.Count > 0)
        {
            dbContext.CategoryFields.RemoveRange(report.CategoryFields);
            report.CategoryFields.Clear();
        }

        foreach (var definition in category.FieldDefinitions)
        {
            if (!normalized.CategoryFields.TryGetValue(definition.FieldKey, out var value)
                || value.Length == 0)
            {
                continue;
            }

            report.CategoryFields.Add(new CategoryField
            {
                Id = Guid.NewGuid(),
                FieldKey = definition.FieldKey,
                Value = value,
            });
        }
    }

    private static NormalizedCreateReportRequest NormalizeUpdateRequest(UpdateReportRequest request)
    {
        var categoryFields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (fieldKey, value) in request.CategoryFields ?? [])
        {
            categoryFields[fieldKey] = TextNormalizer.Normalize(value);
        }

        return new NormalizedCreateReportRequest
        {
            Type = string.Empty,
            CategoryCode = TextNormalizer.Normalize(request.CategoryCode),
            Title = TextNormalizer.Normalize(request.Title),
            Description = TextNormalizer.Normalize(request.Description),
            DateLostOrFound = request.DateLostOrFound,
            GovernorateCode = TextNormalizer.Normalize(request.GovernorateCode),
            AreaText = string.IsNullOrWhiteSpace(request.AreaText)
                ? null
                : TextNormalizer.Normalize(request.AreaText),
            HeldLocation = string.IsNullOrWhiteSpace(request.HeldLocation)
                ? null
                : TextNormalizer.Normalize(request.HeldLocation),
            HasReward = request.HasReward,
            RewardAmount = request.RewardAmount,
            HiddenDetail = TextNormalizer.Normalize(request.HiddenDetail),
            CategoryFields = categoryFields,
        };
    }

    private static readonly ReportStatus[] ClosedStatuses =
    [
        ReportStatus.Resolved,
        ReportStatus.Withdrawn,
        ReportStatus.RemovedByAdmin,
    ];

    private static NormalizedCreateReportRequest NormalizeRequest(CreateReportRequest request)
    {
        var categoryFields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (fieldKey, value) in request.CategoryFields ?? [])
        {
            categoryFields[fieldKey] = TextNormalizer.Normalize(value);
        }

        return new NormalizedCreateReportRequest
        {
            Type = request.Type.Trim().ToLowerInvariant(),
            CategoryCode = TextNormalizer.Normalize(request.CategoryCode),
            Title = TextNormalizer.Normalize(request.Title),
            Description = TextNormalizer.Normalize(request.Description),
            DateLostOrFound = request.DateLostOrFound,
            GovernorateCode = TextNormalizer.Normalize(request.GovernorateCode),
            AreaText = string.IsNullOrWhiteSpace(request.AreaText)
                ? null
                : TextNormalizer.Normalize(request.AreaText),
            HeldLocation = string.IsNullOrWhiteSpace(request.HeldLocation)
                ? null
                : TextNormalizer.Normalize(request.HeldLocation),
            HasReward = request.HasReward,
            RewardAmount = request.RewardAmount,
            HiddenDetail = TextNormalizer.Normalize(request.HiddenDetail),
            CategoryFields = categoryFields,
        };
    }

    private static Dictionary<string, string[]> MergeErrors(
        params Dictionary<string, string[]>[] sources)
    {
        var merged = new Dictionary<string, string[]>();

        foreach (var source in sources)
        {
            foreach (var (fieldKey, messages) in source)
            {
                if (merged.TryGetValue(fieldKey, out var existing))
                {
                    merged[fieldKey] = [.. existing, .. messages];
                }
                else
                {
                    merged[fieldKey] = messages;
                }
            }
        }

        return merged;
    }

    private static ReportSummaryResponse ToSummary(Report report) =>
        new()
        {
            Id = report.Id,
            Type = ToApiType(report.Type),
            Status = ToApiStatus(report.Status),
            Title = report.Title,
            CategoryCode = report.Category.Code,
            GovernorateCode = report.Governorate.Code,
            CreatedAt = report.CreatedAt,
            HasReward = report.HasReward,
            RewardAmount = report.RewardAmount,
        };

    private ReportDetailResponse ToReporterDetail(Report report, ModerationAction? latestRejection) =>
        BuildDetail(report, includeHiddenDetail: true, latestRejection);

    private ReportDetailResponse ToAdminDetail(Report report, ModerationAction? latestRejection) =>
        BuildDetail(report, includeHiddenDetail: false, latestRejection);

    private ReportDetailResponse BuildDetail(
        Report report,
        bool includeHiddenDetail,
        ModerationAction? latestRejection) =>
        new()
        {
            Id = report.Id,
            Type = ToApiType(report.Type),
            Status = ToApiStatus(report.Status),
            Title = report.Title,
            CategoryCode = report.Category.Code,
            GovernorateCode = report.Governorate.Code,
            CreatedAt = report.CreatedAt,
            HasReward = report.HasReward,
            RewardAmount = report.RewardAmount,
            Description = report.Description,
            DateLostOrFound = report.DateLostOrFound,
            AreaText = report.AreaText,
            HeldLocation = report.HeldLocation,
            CategoryFields = report.CategoryFields
                .OrderBy(field => field.FieldKey)
                .ToDictionary(field => field.FieldKey, field => field.Value),
            HiddenDetail = includeHiddenDetail ? report.HiddenDetail : null,
            WithdrawalReason = report.Status == ReportStatus.Withdrawn
                ? report.WithdrawalReason
                : null,
            RejectionReasonCode = latestRejection?.ReasonCode,
            RejectionNote = latestRejection?.Note,
            Photos = report.Photos
                .OrderBy(photo => photo.SortOrder)
                .Select(photo => new ReportPhotoResponse
                {
                    Id = photo.Id,
                    ThumbnailUrl = report.Category.PhotosPrivate || photo.ThumbnailStorageKey is null
                        ? null
                        : bucketStorage.GetPublicUrl(photo.ThumbnailStorageKey),
                    SortOrder = photo.SortOrder,
                })
                .ToList(),
        };

    private static string ToApiType(ReportType type) =>
        type switch
        {
            ReportType.Lost => "lost",
            ReportType.Found => "found",
            _ => type.ToString().ToLowerInvariant(),
        };

    private static string ToApiStatus(ReportStatus status) =>
        status switch
        {
            ReportStatus.PendingReview => "pending_review",
            ReportStatus.Rejected => "rejected",
            ReportStatus.Published => "published",
            ReportStatus.ClaimInProgress => "claim_in_progress",
            ReportStatus.Resolved => "resolved",
            ReportStatus.Withdrawn => "withdrawn",
            ReportStatus.RemovedByAdmin => "removed_by_admin",
            _ => status.ToString().ToLowerInvariant(),
        };

    private static bool TryParseReportType(string type, out ReportType reportType)
    {
        switch (type)
        {
            case "lost":
                reportType = ReportType.Lost;
                return true;
            case "found":
                reportType = ReportType.Found;
                return true;
            default:
                reportType = default;
                return false;
        }
    }

    private static ReportStatus? ParseStatusFilter(string? status) =>
        status switch
        {
            null or "" or "pending_review" => ReportStatus.PendingReview,
            "rejected" => ReportStatus.Rejected,
            "published" => ReportStatus.Published,
            "claim_in_progress" => ReportStatus.ClaimInProgress,
            "resolved" => ReportStatus.Resolved,
            "withdrawn" => ReportStatus.Withdrawn,
            "removed_by_admin" => ReportStatus.RemovedByAdmin,
            _ => null,
        };

    private sealed class NormalizedCreateReportRequest
    {
        public required string Type { get; init; }

        public required string CategoryCode { get; init; }

        public required string Title { get; init; }

        public required string Description { get; init; }

        public DateOnly DateLostOrFound { get; init; }

        public required string GovernorateCode { get; init; }

        public string? AreaText { get; init; }

        public string? HeldLocation { get; init; }

        public bool HasReward { get; init; }

        public int? RewardAmount { get; init; }

        public required string HiddenDetail { get; init; }

        public Dictionary<string, string> CategoryFields { get; init; } = [];
    }
}
