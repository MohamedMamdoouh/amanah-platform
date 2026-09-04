using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportHeldLocationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeldLocationDetail",
                table: "reports",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeldLocationKind",
                table: "reports",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "ItemHeldLocation",
                table: "reports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItemHeldLocation",
                table: "reports",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "HeldLocationDetail",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "HeldLocationKind",
                table: "reports");
        }
    }
}
