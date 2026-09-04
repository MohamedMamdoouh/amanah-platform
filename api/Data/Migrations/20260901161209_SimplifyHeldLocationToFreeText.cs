using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyHeldLocationToFreeText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeldLocationKind",
                table: "reports");

            migrationBuilder.RenameColumn(
                name: "HeldLocationDetail",
                table: "reports",
                newName: "HeldLocation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HeldLocation",
                table: "reports",
                newName: "HeldLocationDetail");

            migrationBuilder.AddColumn<string>(
                name: "HeldLocationKind",
                table: "reports",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}
