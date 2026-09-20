using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameAbuseReportReporterToAbuseReporter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_abuse_reports_users_ReporterId",
                table: "abuse_reports");

            migrationBuilder.RenameColumn(
                name: "ReporterId",
                table: "abuse_reports",
                newName: "AbuseReporterId");

            migrationBuilder.RenameIndex(
                name: "IX_abuse_reports_ReporterId_ReportId",
                table: "abuse_reports",
                newName: "IX_abuse_reports_AbuseReporterId_ReportId");

            migrationBuilder.AddForeignKey(
                name: "FK_abuse_reports_users_AbuseReporterId",
                table: "abuse_reports",
                column: "AbuseReporterId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_abuse_reports_users_AbuseReporterId",
                table: "abuse_reports");

            migrationBuilder.RenameColumn(
                name: "AbuseReporterId",
                table: "abuse_reports",
                newName: "ReporterId");

            migrationBuilder.RenameIndex(
                name: "IX_abuse_reports_AbuseReporterId_ReportId",
                table: "abuse_reports",
                newName: "IX_abuse_reports_ReporterId_ReportId");

            migrationBuilder.AddForeignKey(
                name: "FK_abuse_reports_users_ReporterId",
                table: "abuse_reports",
                column: "ReporterId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
