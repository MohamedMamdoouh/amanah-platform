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
            // reports.CategoryId → categories is Restrict. A blind DELETE of "keys"
            // aborts AutoMigrate (and process startup) whenever any keys report exists.
            // Soft-retire when referenced; hard-delete only when safe. Park retired
            // keys at SortOrder 1000 so the compacting UPDATE cannot collide with bags.
            migrationBuilder.Sql("""
                UPDATE category_field_definitions
                SET "Type" = 'Text'
                WHERE "Type" = 'Integer';

                UPDATE categories
                SET "Active" = false,
                    "SortOrder" = 1000
                WHERE "Code" = 'keys'
                  AND EXISTS (
                      SELECT 1
                      FROM reports
                      WHERE reports."CategoryId" = categories."Id"
                  );

                DELETE FROM categories
                WHERE "Code" = 'keys'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM reports
                      WHERE reports."CategoryId" = categories."Id"
                  );

                UPDATE categories
                SET "SortOrder" = "SortOrder" - 1
                WHERE "SortOrder" > 4
                  AND "SortOrder" < 1000;
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
