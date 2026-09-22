using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Amanah.Api.Tests.Enforcement;

public sealed class UserBanRaceWebApplicationFactory : ApiWebApplicationFactory
{
    public BanApproveRaceCommandInterceptor RaceCommandInterceptor { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        RaceCommandInterceptor = new BanApproveRaceCommandInterceptor(ConnectionString);

        builder.ConfigureTestServices(services =>
        {
            services.ConfigureDbContext<AppDbContext>((_, options) =>
            {
                options.AddInterceptors(RaceCommandInterceptor);
            });
        });
    }
}

public class UserBanRaceTests(UserBanRaceWebApplicationFactory factory)
    : IClassFixture<UserBanRaceWebApplicationFactory>
{
    [Fact]
    public async Task Ban_aborts_when_claim_is_approved_after_report_was_loaded()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var claimId = await ClaimTestHelpers.SeedPendingClaimAsync(
            context,
            reportId,
            claimant.User.Id);
        var reporterId = context.Session.User.Id;

        await HttpTestHelpers.LoginAsAdminAsync(context);
        factory.RaceCommandInterceptor.TargetReportId = reportId;
        factory.RaceCommandInterceptor.TargetClaimId = claimId;
        factory.RaceCommandInterceptor.Armed = true;

        var racedBan = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{reporterId}/ban",
            new BanUserRequest { Reason = "Race with approve" });

        Assert.Equal(HttpStatusCode.Conflict, racedBan.StatusCode);
        var racedError = await HttpTestHelpers.ReadErrorAsync(racedBan);
        Assert.Equal(ErrorCodes.Conflict, racedError?.Code);

        context.DbContext.ChangeTracker.Clear();
        var userAfterRace = await context.DbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.Id == reporterId);
        var reportAfterRace = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(report => report.Id == reportId);
        var claimAfterRace = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim => claim.Id == claimId);

        Assert.False(userAfterRace.IsBanned);
        Assert.Equal(ReportStatus.ClaimInProgress, reportAfterRace.Status);
        Assert.Equal(ClaimStatus.Approved, claimAfterRace.Status);

        var retryBan = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/users/{reporterId}/ban",
            new BanUserRequest { Reason = "Race with approve" });
        Assert.Equal(HttpStatusCode.OK, retryBan.StatusCode);

        context.DbContext.ChangeTracker.Clear();
        var userAfterRetry = await context.DbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.Id == reporterId);
        var reportAfterRetry = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(report => report.Id == reportId);
        var claimAfterRetry = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(claim => claim.Id == claimId);

        Assert.True(userAfterRetry.IsBanned);
        Assert.Equal(ReportStatus.Withdrawn, reportAfterRetry.Status);
        Assert.Equal(ClaimStatus.Cancelled, claimAfterRetry.Status);
    }
}

public sealed class BanApproveRaceCommandInterceptor(string connectionString) : DbCommandInterceptor
{
    public Guid? TargetReportId { get; set; }

    public Guid? TargetClaimId { get; set; }

    public bool Armed { get; set; }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        MaybeApproveClaim(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void MaybeApproveClaim(DbCommand command)
    {
        if (!Armed
            || TargetReportId is null
            || TargetClaimId is null
            || !command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Armed = false;

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        using (var reportCommand = new NpgsqlCommand(
                   """
                   UPDATE reports
                   SET "Status" = 'ClaimInProgress', "UpdatedAt" = NOW()
                   WHERE "Id" = @reportId AND "Status" = 'Published'
                   """,
                   connection,
                   transaction))
        {
            reportCommand.Parameters.AddWithValue("reportId", TargetReportId.Value);
            if (reportCommand.ExecuteNonQuery() != 1)
            {
                throw new InvalidOperationException("Expected to approve the published report row.");
            }
        }

        using (var claimCommand = new NpgsqlCommand(
                   """
                   UPDATE claims
                   SET "Status" = 'Approved', "ReviewedAt" = NOW(), "ReviewerDecision" = 'approved'
                   WHERE "Id" = @claimId AND "Status" = 'Pending'
                   """,
                   connection,
                   transaction))
        {
            claimCommand.Parameters.AddWithValue("claimId", TargetClaimId.Value);
            if (claimCommand.ExecuteNonQuery() != 1)
            {
                throw new InvalidOperationException("Expected to approve the pending claim row.");
            }
        }

        transaction.Commit();
    }
}
