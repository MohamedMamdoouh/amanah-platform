using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Auth;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Admin;

public sealed class AdminUserLookupService(AppDbContext dbContext)
{
    private const int SearchResultLimit = 50;

    public async Task<Result<AdminUserListResponse>> SearchAsync(
        AdminUserSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var searchBy = (query.SearchBy ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(searchBy) || !AdminUserSearchBy.All.Contains(searchBy))
        {
            return ResultError.BadRequest(
                "Search mode is required.",
                errors: new Dictionary<string, string[]>
                {
                    ["searchBy"] = ["Choose name or phone search."],
                });
        }

        var trimmed = (query.Query ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return ResultError.BadRequest(
                "Search query is required.",
                errors: new Dictionary<string, string[]>
                {
                    ["query"] = ["Search query is required."],
                });
        }

        IQueryable<User> usersQuery = dbContext.Users.AsNoTracking();

        if (string.Equals(searchBy, AdminUserSearchBy.Phone, StringComparison.OrdinalIgnoreCase))
        {
            if (!PhoneNormalizer.TryNormalize(trimmed, out var normalizedPhone))
            {
                return ResultError.BadRequest(
                    "Phone number format is invalid.",
                    ErrorCodes.InvalidPhone,
                    errors: new Dictionary<string, string[]>
                    {
                        ["query"] = ["Phone number format is invalid."],
                    });
            }

            usersQuery = usersQuery.Where(user => user.NormalizedPhone == normalizedPhone);
        }
        else
        {
            var pattern = $"%{trimmed}%";
            usersQuery = usersQuery.Where(user =>
                user.DisplayName != null
                && EF.Functions.ILike(user.DisplayName, pattern));
        }

        var items = await usersQuery
            .OrderBy(user => user.DisplayName)
            .Take(SearchResultLimit)
            .Select(user => new AdminUserSummaryResponse
            {
                Id = user.Id,
                DisplayName = user.DisplayName ?? string.Empty,
            })
            .ToListAsync(cancellationToken);

        return new AdminUserListResponse { Items = items };
    }

    public async Task<Result<AdminUserDetailResponse>> GetDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(existing => existing.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultError.NotFound("User not found.");
        }

        var reportsCount = await dbContext.Reports
            .AsNoTracking()
            .CountAsync(report => report.ReporterId == userId, cancellationToken);

        return new AdminUserDetailResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName ?? string.Empty,
            NormalizedPhone = user.NormalizedPhone,
            Role = user.Role.ToString(),
            IsBanned = user.IsBanned,
            BanReason = user.BanReason,
            BannedAt = user.BannedAt,
            ReportsCount = reportsCount,
            CreatedAt = user.CreatedAt,
        };
    }
}
