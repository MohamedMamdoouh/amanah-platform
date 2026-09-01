using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryFieldTextFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TextFormat",
                table: "category_field_definitions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TextFormat",
                table: "category_field_definitions");
        }
    }
}
