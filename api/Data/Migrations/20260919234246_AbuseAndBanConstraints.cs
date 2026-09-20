using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AbuseAndBanConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_abuse_reports_ReporterId",
                table: "abuse_reports");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BannedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_abuse_reports_ReporterId_ReportId",
                table: "abuse_reports",
                columns: new[] { "ReporterId", "ReportId" },
                unique: true,
                filter: "\"Status\" = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_abuse_reports_Status_CreatedAt",
                table: "abuse_reports",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_abuse_reports_ReporterId_ReportId",
                table: "abuse_reports");

            migrationBuilder.DropIndex(
                name: "IX_abuse_reports_Status_CreatedAt",
                table: "abuse_reports");

            migrationBuilder.DropColumn(
                name: "BannedAt",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_abuse_reports_ReporterId",
                table: "abuse_reports",
                column: "ReporterId");
        }
    }
}
