using System.Net;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Auth;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Lifecycle;

public sealed class HousekeepingJobsWebApplicationFactory : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Lifecycle:RetentionDays", "30");
        builder.UseSetting("Lifecycle:NotificationRetentionDays", "7");
    }
}

public class HousekeepingJobsTests(HousekeepingJobsWebApplicationFactory factory)
    : IClassFixture<HousekeepingJobsWebApplicationFactory>
{
    [Fact]
    public async Task NotificationCleanup_removes_notifications_older_than_retention_window()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var staleId = Guid.NewGuid();
        var freshId = Guid.NewGuid();
        var userId = context.Session.User.Id;
        var now = DateTimeOffset.UtcNow;
        const string payload =
            """{"type":"ReportApproved","createdAt":"2026-01-01T00:00:00Z","deepLink":"/my/reports"}""";

        context.DbContext.Notifications.AddRange(
            new Notification
            {
                Id = staleId,
                UserId = userId,
                Type = "ReportApproved",
                PayloadJson = payload,
                IsRead = false,
                CreatedAt = now.AddDays(-8),
            },
            new Notification
            {
                Id = freshId,
                UserId = userId,
                Type = "ReportApproved",
                PayloadJson = payload,
                IsRead = true,
                CreatedAt = now.AddDays(-5),
            });
        await context.DbContext.SaveChangesAsync();

        var response = await RunJobAsync(context, "NotificationCleanup");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.False(await context.DbContext.Notifications.AnyAsync(item => item.Id == staleId));
        Assert.True(await context.DbContext.Notifications.AnyAsync(item => item.Id == freshId));
    }

    [Fact]
    public async Task OtpCleanup_removes_codes_expired_more_than_24_hours_ago()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var staleId = Guid.NewGuid();
        var freshId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.DbContext.OtpCodes.AddRange(
            new OtpCode
            {
                Id = staleId,
                Destination = "+201011111111",
                Channel = AuthIdentifierChannel.Phone,
                CodeHash = "stale-hash",
                ExpiresAt = now.AddHours(-25),
                CreatedAt = now.AddHours(-26),
            },
            new OtpCode
            {
                Id = freshId,
                Destination = "+201022222222",
                Channel = AuthIdentifierChannel.Phone,
                CodeHash = "fresh-hash",
                ExpiresAt = now.AddHours(-23),
                CreatedAt = now.AddHours(-24),
            });
        await context.DbContext.SaveChangesAsync();

        var response = await RunJobAsync(context, "OtpCleanup");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.False(await context.DbContext.OtpCodes.AnyAsync(item => item.Id == staleId));
        Assert.True(await context.DbContext.OtpCodes.AnyAsync(item => item.Id == freshId));
    }

    [Fact]
    public async Task SessionCleanup_removes_expired_and_revoked_refresh_tokens_past_retention_window()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var userId = context.Session.User.Id;
        var staleExpiredId = Guid.NewGuid();
        var staleRevokedId = Guid.NewGuid();
        var staleRevokedFutureExpiryId = Guid.NewGuid();
        var freshExpiredId = Guid.NewGuid();
        var freshRevokedId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.DbContext.RefreshTokens.AddRange(
            new RefreshToken
            {
                Id = staleExpiredId,
                UserId = userId,
                TokenHash = RefreshTokenHasher.Hash("stale-expired"),
                ExpiresAt = now.AddDays(-31),
                IsRevoked = false,
                CreatedAt = now.AddDays(-60),
            },
            new RefreshToken
            {
                Id = staleRevokedId,
                UserId = userId,
                TokenHash = RefreshTokenHasher.Hash("stale-revoked"),
                ExpiresAt = now.AddDays(-31),
                IsRevoked = true,
                RevokedAt = now.AddDays(-31),
                CreatedAt = now.AddDays(-60),
            },
            new RefreshToken
            {
                Id = staleRevokedFutureExpiryId,
                UserId = userId,
                TokenHash = RefreshTokenHasher.Hash("stale-revoked-future-expiry"),
                ExpiresAt = now.AddDays(7),
                IsRevoked = true,
                RevokedAt = now.AddDays(-31),
                CreatedAt = now.AddDays(-60),
            },
            new RefreshToken
            {
                Id = freshExpiredId,
                UserId = userId,
                TokenHash = RefreshTokenHasher.Hash("fresh-expired"),
                ExpiresAt = now.AddDays(-29),
                IsRevoked = false,
                CreatedAt = now.AddDays(-60),
            },
            new RefreshToken
            {
                Id = freshRevokedId,
                UserId = userId,
                TokenHash = RefreshTokenHasher.Hash("fresh-revoked"),
                ExpiresAt = now.AddDays(7),
                IsRevoked = true,
                RevokedAt = now.AddDays(-5),
                CreatedAt = now.AddDays(-60),
            },
            new RefreshToken
            {
                Id = activeId,
                UserId = userId,
                TokenHash = RefreshTokenHasher.Hash("active"),
                ExpiresAt = now.AddDays(7),
                IsRevoked = false,
                CreatedAt = now,
            });
        await context.DbContext.SaveChangesAsync();

        var response = await RunJobAsync(context, "SessionCleanup");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.False(await context.DbContext.RefreshTokens.AnyAsync(item => item.Id == staleExpiredId));
        Assert.False(await context.DbContext.RefreshTokens.AnyAsync(item => item.Id == staleRevokedId));
        Assert.False(await context.DbContext.RefreshTokens.AnyAsync(item => item.Id == staleRevokedFutureExpiryId));
        Assert.True(await context.DbContext.RefreshTokens.AnyAsync(item => item.Id == freshExpiredId));
        Assert.True(await context.DbContext.RefreshTokens.AnyAsync(item => item.Id == freshRevokedId));
        Assert.True(await context.DbContext.RefreshTokens.AnyAsync(item => item.Id == activeId));
    }

    [Fact]
    public async Task OtpSmsOutboxCleanup_removes_old_processed_rows_and_keeps_pending()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var staleSentId = Guid.NewGuid();
        var staleFailedId = Guid.NewGuid();
        var freshSentId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.DbContext.OtpSmsOutboxMessages.AddRange(
            new OtpSmsOutboxMessage
            {
                Id = staleSentId,
                Phone = "+201011111111",
                ProtectedPayload = "payload-1",
                Status = OtpSmsOutboxStatus.Sent,
                CreatedAt = now.AddDays(-40),
                ProcessedAt = now.AddDays(-31),
            },
            new OtpSmsOutboxMessage
            {
                Id = staleFailedId,
                Phone = "+201011111112",
                ProtectedPayload = "payload-2",
                Status = OtpSmsOutboxStatus.Failed,
                CreatedAt = now.AddDays(-40),
                ProcessedAt = now.AddDays(-31),
                LastError = "provider timeout",
            },
            new OtpSmsOutboxMessage
            {
                Id = freshSentId,
                Phone = "+201011111113",
                ProtectedPayload = "payload-3",
                Status = OtpSmsOutboxStatus.Sent,
                CreatedAt = now.AddDays(-10),
                ProcessedAt = now.AddDays(-5),
            },
            new OtpSmsOutboxMessage
            {
                Id = pendingId,
                Phone = "+201011111114",
                ProtectedPayload = "payload-4",
                Status = OtpSmsOutboxStatus.Pending,
                CreatedAt = now.AddDays(-40),
            });
        await context.DbContext.SaveChangesAsync();

        var response = await RunJobAsync(context, "OtpSmsOutboxCleanup");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.False(await context.DbContext.OtpSmsOutboxMessages.AnyAsync(item => item.Id == staleSentId));
        Assert.False(await context.DbContext.OtpSmsOutboxMessages.AnyAsync(item => item.Id == staleFailedId));
        Assert.True(await context.DbContext.OtpSmsOutboxMessages.AnyAsync(item => item.Id == freshSentId));
        Assert.True(await context.DbContext.OtpSmsOutboxMessages.AnyAsync(item => item.Id == pendingId));
    }

    [Fact]
    public async Task AdminAlertEmailOutboxCleanup_removes_old_processed_rows_and_keeps_pending()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await SeedMinimalReportAsync(context);
        var staleSentId = Guid.NewGuid();
        var staleFailedId = Guid.NewGuid();
        var freshSentId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        context.DbContext.AdminAlertEmailOutboxMessages.AddRange(
            new AdminAlertEmailOutboxMessage
            {
                Id = staleSentId,
                ReportId = reportId,
                ReportType = ReportType.Lost.ToString(),
                CategoryCode = "phones",
                Status = AdminAlertEmailOutboxStatus.Sent,
                CreatedAt = now.AddDays(-40),
                ProcessedAt = now.AddDays(-31),
            },
            new AdminAlertEmailOutboxMessage
            {
                Id = staleFailedId,
                ReportId = reportId,
                ReportType = ReportType.Lost.ToString(),
                CategoryCode = "phones",
                Status = AdminAlertEmailOutboxStatus.Failed,
                CreatedAt = now.AddDays(-40),
                ProcessedAt = now.AddDays(-31),
                LastError = "smtp error",
            },
            new AdminAlertEmailOutboxMessage
            {
                Id = freshSentId,
                ReportId = reportId,
                ReportType = ReportType.Lost.ToString(),
                CategoryCode = "phones",
                Status = AdminAlertEmailOutboxStatus.Sent,
                CreatedAt = now.AddDays(-10),
                ProcessedAt = now.AddDays(-5),
            },
            new AdminAlertEmailOutboxMessage
            {
                Id = pendingId,
                ReportId = reportId,
                ReportType = ReportType.Lost.ToString(),
                CategoryCode = "phones",
                Status = AdminAlertEmailOutboxStatus.Pending,
                CreatedAt = now.AddDays(-40),
            });
        await context.DbContext.SaveChangesAsync();

        var response = await RunJobAsync(context, "AdminAlertEmailOutboxCleanup");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.False(await context.DbContext.AdminAlertEmailOutboxMessages.AnyAsync(item => item.Id == staleSentId));
        Assert.False(await context.DbContext.AdminAlertEmailOutboxMessages.AnyAsync(item => item.Id == staleFailedId));
        Assert.True(await context.DbContext.AdminAlertEmailOutboxMessages.AnyAsync(item => item.Id == freshSentId));
        Assert.True(await context.DbContext.AdminAlertEmailOutboxMessages.AnyAsync(item => item.Id == pendingId));
    }

    private static async Task<Guid> SeedMinimalReportAsync(ReportTestContext context)
    {
        var reportId = Guid.NewGuid();
        var category = await context.DbContext.Categories.SingleAsync(item => item.Code == "phones");
        var governorate = await context.DbContext.Governorates.SingleAsync(item => item.Code == "cairo");

        context.DbContext.Reports.Add(new Report
        {
            Id = reportId,
            ReporterId = context.Session.User.Id,
            Type = ReportType.Lost,
            CategoryId = category.Id,
            Title = "Housekeeping outbox test",
            Description = "Description long enough for validation",
            DateLostOrFound = DateOnly.FromDateTime(DateTime.UtcNow),
            GovernorateId = governorate.Id,
            Status = ReportStatus.PendingReview,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await context.DbContext.SaveChangesAsync();

        return reportId;
    }

    private static async Task<HttpResponseMessage> RunJobAsync(ReportTestContext context, string jobName)
    {
        await HttpTestHelpers.LoginAsAdminAsync(context);
        return await context.Client.PostAsync($"/api/v1/admin/test/run-job/{jobName}", null);
    }
}
