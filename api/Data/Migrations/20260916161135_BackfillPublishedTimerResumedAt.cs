using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPublishedTimerResumedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pre-timer publishes only set PublishedAt. Treat that as the active segment
            // start so cumulative expiry does not reset to zero for existing listings.
            migrationBuilder.Sql(
                """
                UPDATE reports
                SET "PublishedTimerResumedAt" = "PublishedAt"
                WHERE "Status" = 'Published'
                  AND "PublishedAt" IS NOT NULL
                  AND "PublishedTimerResumedAt" IS NULL
                  AND "PublishedSecondsElapsed" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE reports
                SET "PublishedTimerResumedAt" = NULL
                WHERE "Status" = 'Published'
                  AND "PublishedTimerResumedAt" IS NOT NULL
                  AND "PublishedAt" IS NOT NULL
                  AND "PublishedTimerResumedAt" = "PublishedAt"
                  AND "PublishedSecondsElapsed" = 0;
                """);
        }
    }
}
