using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TextOnlyCategoryFieldsAndRemoveKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE category_field_definitions
                SET "Type" = 'Text'
                WHERE "Type" = 'Integer';

                DELETE FROM categories
                WHERE "Code" = 'keys';

                UPDATE categories
                SET "SortOrder" = "SortOrder" - 1
                WHERE "SortOrder" > 4;
                """);

            migrationBuilder.DropColumn(
                name: "MaxInt",
                table: "category_field_definitions");

            migrationBuilder.DropColumn(
                name: "MinInt",
                table: "category_field_definitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxInt",
                table: "category_field_definitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinInt",
                table: "category_field_definitions",
                type: "integer",
                nullable: true);
        }
    }
}
