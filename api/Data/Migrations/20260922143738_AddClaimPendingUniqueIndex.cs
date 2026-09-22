using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimPendingUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_claims_ReportId",
                table: "claims");

            migrationBuilder.CreateIndex(
                name: "IX_claims_ReportId_ClaimantId",
                table: "claims",
                columns: new[] { "ReportId", "ClaimantId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_claims_ReportId_ClaimantId",
                table: "claims");

            migrationBuilder.CreateIndex(
                name: "IX_claims_ReportId",
                table: "claims",
                column: "ReportId");
        }
    }
}
