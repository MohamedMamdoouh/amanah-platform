using Amanah.Api.Data;
using Amanah.Api.Utilities.Common;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Claims;

public sealed class ClaimQuotaService(AppDbContext dbContext, TimeProvider timeProvider) : IClaimQuotaService
{
    public const int DailyQuotaLimit = 5;

    public async Task<ClaimQuotaCheckResult> CheckDailySubmissionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var dailyCount = await CountClaimsSubmittedTodayAsync(userId, cancellationToken);
        if (dailyCount < DailyQuotaLimit)
        {
            return new ClaimQuotaCheckResult(false);
        }

        return new ClaimQuotaCheckResult(true, SecondsUntilNextCairoMidnight());
    }

    private async Task<int> CountClaimsSubmittedTodayAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var dayStart = CairoTime.CairoDayStartUtc(now);
        var nextDayStart = dayStart.AddDays(1);

        return await dbContext.Claims
            .AsNoTracking()
            .CountAsync(
                claim => claim.ClaimantId == userId
                    && claim.SubmittedAt >= dayStart
                    && claim.SubmittedAt < nextDayStart,
                cancellationToken);
    }

    private int SecondsUntilNextCairoMidnight()
    {
        var now = timeProvider.GetUtcNow();
        var dayStart = CairoTime.CairoDayStartUtc(now);
        var nextDayStart = dayStart.AddDays(1);
        var seconds = (int)Math.Ceiling((nextDayStart - now).TotalSeconds);

        return Math.Max(seconds, 1);
    }
}
